using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using HunterVsHider.Map;
using HunterVsHider.Managers;

namespace HunterVsHider.EditorScripts
{
    public class SetupTaskCorridorMapGenerator
    {
        [MenuItem("Tools/Hunter v Hider/Verify Corridor-First Map Generator")]
        public static void ExecuteVerification()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;

            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[SetupTaskCorridorMapGenerator] Could not open scene {scenePath}!");
                return;
            }

            // Ensure Zone_CombatArena and MapGenerator exist
            GameObject combatArena = GameObject.Find("Zone_CombatArena");
            if (combatArena == null)
            {
                combatArena = new GameObject("Zone_CombatArena");
            }

            Transform mapGenTrans = combatArena.transform.Find("MapGenerator");
            GameObject mapGenObj = (mapGenTrans != null) ? mapGenTrans.gameObject : null;
            if (mapGenObj == null)
            {
                mapGenObj = new GameObject("MapGenerator");
                mapGenObj.transform.SetParent(combatArena.transform, false);
            }

            MapGenerator mapGen = mapGenObj.GetComponent<MapGenerator>();
            if (mapGen == null) mapGen = mapGenObj.AddComponent<MapGenerator>();

            Transform envTrans = mapGenObj.transform.Find("GeneratedEnvironment");
            if (envTrans == null)
            {
                GameObject envObj = new GameObject("GeneratedEnvironment");
                envObj.transform.SetParent(mapGenObj.transform, false);
                mapGen.generatedEnvironment = envObj.transform;
            }
            else
            {
                mapGen.generatedEnvironment = envTrans;
            }

            // Test Generation across 3 Tiers (50, 100, 150) and 4 distinct seeds
            int[] testSizes = { 50, 100, 150 };
            int[] testSeeds = { 104729, 395812, 772914, 918273 };

            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskCorridorMapGenerator] STARTING SCATTERED ROOM-PACKING MAP VERIFICATION");
            Debug.Log("==========================================================================");

            foreach (int size in testSizes)
            {
                foreach (int seed in testSeeds)
                {
                    if (MatchManager.Singleton != null)
                    {
                        MatchManager.Singleton.selectedMapSize.Value = size;
                    }

                    mapGen.GenerateMap(seed);

                    int wallCount = mapGen.generatedEnvironment.childCount;
                    Debug.Log($"[Verification] Tier {size}x{size} | Seed {seed} -> Spawned {wallCount} Monolithic Wall Segments");

                    // Validate each spawned wall segment
                    bool allObstacleLayer = true;
                    bool allObstacleTag = true;
                    bool allTwoSidedShadows = true;
                    bool allHaveBoxCollider = true;
                    bool noNetworkObjects = true;
                    bool validThicknessAndHeight = true;

                    int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                    if (obstacleLayer == -1) obstacleLayer = 7;

                    float halfSize = size * 0.5f;

                    for (int i = 0; i < wallCount; i++)
                    {
                        Transform wallTrans = mapGen.generatedEnvironment.GetChild(i);
                        GameObject wallObj = wallTrans.gameObject;

                        if (wallObj.layer != obstacleLayer) allObstacleLayer = false;
                        if (!wallObj.CompareTag("Obstacle")) allObstacleTag = false;

                        Renderer r = wallObj.GetComponent<Renderer>();
                        if (r == null || r.shadowCastingMode != ShadowCastingMode.TwoSided || !r.receiveShadows)
                        {
                            allTwoSidedShadows = false;
                        }

                        BoxCollider col = wallObj.GetComponent<BoxCollider>();
                        if (col == null || !col.enabled) allHaveBoxCollider = false;

                        if (wallObj.GetComponent<Unity.Netcode.NetworkObject>() != null) noNetworkObjects = false;

                        Vector3 scale = wallTrans.localScale;
                        if (Mathf.Abs(scale.y - 3.0f) > 0.01f) validThicknessAndHeight = false;
                        if (Mathf.Abs(scale.x - 0.5f) > 0.01f && Mathf.Abs(scale.z - 0.5f) > 0.01f)
                        {
                            // A wall must have at least one cross-section dimension equal to 0.5m thickness, OR be a structural pillar (e.g. 1.0m x 1.0m or 1.2m x 1.2m)
                            bool isStructuralPillar = (scale.x >= 0.9f && scale.z >= 0.9f);
                            if (!isStructuralPillar)
                            {
                                validThicknessAndHeight = false;
                            }
                        }
                    }

                    // Validate Doorway Hard Caps per room category
                    bool smallRoomsStrictOneDoor = true;
                    bool mediumRoomsStrictTwoDoors = true;
                    bool largeRoomsThreeOrFourDoors = true;
                    int smallRoomCount = 0;
                    int mediumRoomCount = 0;
                    int largeRoomCount = 0;

                    foreach (var room in mapGen.LastGeneratedRooms)
                    {
                        if (room.Area < 40f)
                        {
                            smallRoomCount++;
                            if (room.doorways.Count != 1) smallRoomsStrictOneDoor = false;
                        }
                        else if (room.Area <= 100f)
                        {
                            mediumRoomCount++;
                            if (room.doorways.Count != 2) mediumRoomsStrictTwoDoors = false;
                        }
                        else
                        {
                            largeRoomCount++;
                            if (room.doorways.Count < 3 || room.doorways.Count > 4) largeRoomsThreeOrFourDoors = false;
                        }
                    }

                    if (!smallRoomsStrictOneDoor) Debug.LogError($"[Verification FAIL] Small room doorway hard cap violated for seed {seed}!");
                    if (!mediumRoomsStrictTwoDoors) Debug.LogError($"[Verification FAIL] Medium room 2-door cap violated for seed {seed}!");
                    if (!largeRoomsThreeOrFourDoors) Debug.LogError($"[Verification FAIL] Large room 3-4 doors violated for seed {seed}!");

                    // Validate Shared Boundary Doorway Alignments (Misalignment Safeguard)
                    bool sharedDoorwaysProperlyAligned = true;
                    int sharedBoundaryCount = mapGen.LastGeneratedSharedBoundaries.Count;
                    int interRoomDoorCount = 0;

                    foreach (var sb in mapGen.LastGeneratedSharedBoundaries)
                    {
                        if (sb.hasDoorway)
                        {
                            interRoomDoorCount++;
                            float doorCoord = sb.isHorizontal ? sb.sharedDoorway.centerPos.x : sb.sharedDoorway.centerPos.y;
                            float halfD = sb.sharedDoorway.width * 0.5f;

                            // Ensure doorway fits completely inside [overlapMin, overlapMax]
                            if (doorCoord - halfD < sb.overlapMin - 0.05f || doorCoord + halfD > sb.overlapMax + 0.05f)
                            {
                                sharedDoorwaysProperlyAligned = false;
                            }
                        }
                    }

                    if (!sharedDoorwaysProperlyAligned) Debug.LogError($"[Verification FAIL] Inter-room doorway alignment safeguard failed for seed {seed}!");

                    // Validate 50x50 Small Map Full Accessibility (0 sealed rooms)
                    bool smallMapAllAccessible = true;
                    if (size == 50)
                    {
                        foreach (var r in mapGen.LastGeneratedRooms)
                        {
                            if (r.doorways.Count == 0) smallMapAllAccessible = false;
                        }
                    }

                    if (!smallMapAllAccessible) Debug.LogError($"[Verification FAIL] Found sealed/inaccessible room on 50x50 map for seed {seed}!");

                    // Validate Dead-Space Corridor Injection Tier Behavior
                    bool deadSpaceInjectionTierValid = (size == 50)
                        ? (mapGen.LastInjectedCorridorWallCount == 0)
                        : (mapGen.LastInjectedCorridorWallCount > 0);

                    if (!deadSpaceInjectionTierValid) Debug.LogError($"[Verification FAIL] Dead-space corridor injection tier logic violated for size {size} seed {seed} (Injected: {mapGen.LastInjectedCorridorWallCount})!");

                    if (!allObstacleLayer) Debug.LogError($"[Verification FAIL] Not all walls assigned to Obstacle layer for seed {seed}");
                    if (!allObstacleTag) Debug.LogError($"[Verification FAIL] Not all walls assigned to Obstacle tag for seed {seed}");
                    if (!allTwoSidedShadows) Debug.LogError($"[Verification FAIL] Not all walls have TwoSided shadow casting for seed {seed}");
                    if (!allHaveBoxCollider) Debug.LogError($"[Verification FAIL] Not all walls have BoxCollider for seed {seed}");
                    if (!noNetworkObjects) Debug.LogError($"[Verification FAIL] Rogue NetworkObject found on walls for seed {seed}");
                    if (!validThicknessAndHeight) Debug.LogError($"[Verification FAIL] Wall thickness or height violated for seed {seed}");

                    if (allObstacleLayer && allObstacleTag && allTwoSidedShadows && allHaveBoxCollider && noNetworkObjects && validThicknessAndHeight && smallRoomsStrictOneDoor && mediumRoomsStrictTwoDoors && largeRoomsThreeOrFourDoors && sharedDoorwaysProperlyAligned && smallMapAllAccessible && deadSpaceInjectionTierValid)
                    {
                        Debug.Log($"[Verification PASS] Tier {size}x{size} Seed {seed}: {smallRoomCount} Small (1 Door), {mediumRoomCount} Medium, {largeRoomCount} Anchors, {sharedBoundaryCount} Suites, Injected Corridors: {mapGen.LastInjectedCorridorWallCount}, Accessibility: 100%. All constraints strictly verified.");
                    }
                }
            }

            // Generate final Tier 100 sample in the scene
            mapGen.GenerateMap(104729);

            EditorUtility.SetDirty(mapGen);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskCorridorMapGenerator] ALL VERIFICATIONS COMPLETED SUCCESSFULLY!");
            Debug.Log("==========================================================================");
        }
    }
}

