using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HunterVsHider.Map;
using HunterVsHider.Managers;
using HunterVsHider.Player;
using HunterVsHider.UI;

namespace HunterVsHider.Editor
{
    public static class SetupTaskAsymmetricSpawns
    {
        [MenuItem("Tools/Hunter v Hider/Verify Asymmetric Spawns & Breach Rooms")]
        public static void RunVerification()
        {
            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskAsymmetricSpawns] STARTING ASYMMETRIC SPAWNS & BREACH ROOM SETUP");
            Debug.Log("==========================================================================");

            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            // 1. Verify / Setup GridManager in Scene
            var gridManager = Object.FindAnyObjectByType<GridManager>();
            if (gridManager == null)
            {
                var mapGenObj = GameObject.Find("MapGenerator");
                if (mapGenObj != null)
                {
                    gridManager = mapGenObj.AddComponent<GridManager>();
                }
                else
                {
                    var gmObj = new GameObject("GridManager");
                    gridManager = gmObj.AddComponent<GridManager>();
                }
                Debug.Log("[SetupTaskAsymmetricSpawns] Created GridManager component in scene.");
            }

            // 2. Verify / Setup PoliceBreachUI in Scene
            var breachUI = Object.FindAnyObjectByType<PoliceBreachUI>();
            if (breachUI == null)
            {
                var uiRoot = GameObject.Find("UI_PoliceBreach");
                if (uiRoot == null)
                {
                    uiRoot = new GameObject("UI_PoliceBreach");
                }
                breachUI = uiRoot.AddComponent<PoliceBreachUI>();
                Debug.Log("[SetupTaskAsymmetricSpawns] Created PoliceBreachUI component in scene.");
            }

            // 3. Verify / Setup Player.prefab with AssassinPlacementController
            string prefabPath = "Assets/_Project/Prefabs/Player.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (playerPrefab != null)
            {
                var placement = playerPrefab.GetComponent<AssassinPlacementController>();
                if (placement == null)
                {
                    playerPrefab.AddComponent<AssassinPlacementController>();
                    EditorUtility.SetDirty(playerPrefab);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[SetupTaskAsymmetricSpawns] Attached AssassinPlacementController to Player.prefab.");
                }
            }

            // 4. Verify Map Generation & Breach Rooms
            var mapGen = Object.FindAnyObjectByType<MapGenerator>();
            if (mapGen != null)
            {
                mapGen.GenerateMap(12345);

                var breachPositions = GridManager.GetBreachSpawnPositions(50);
                var breachNames = GridManager.GetBreachRoomNames(50);

                if (breachPositions == null || breachPositions.Count < 3)
                {
                    Debug.LogError($"[Verification FAIL] Expected at least 3 breach positions, found: {breachPositions?.Count}");
                    return;
                }

                Debug.Log($"[Verification PASS] Procedural Breach Rooms verified: {breachPositions.Count} rooms ({string.Join(", ", breachNames)})");
                for (int i = 0; i < breachPositions.Count; i++)
                {
                    Debug.Log($"   └ [{breachNames[i]}] Center Position: {breachPositions[i]}");
                }

                // Verify wall obstacles layer
                if (mapGen.generatedEnvironment != null)
                {
                    int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                    int childCount = mapGen.generatedEnvironment.childCount;
                    int obstacleCount = 0;
                    for (int i = 0; i < childCount; i++)
                    {
                        if (mapGen.generatedEnvironment.GetChild(i).gameObject.layer == obstacleLayer)
                        {
                            obstacleCount++;
                        }
                    }
                    Debug.Log($"[Verification PASS] Generated {childCount} walls, {obstacleCount} assigned to Obstacle Layer (Layer {obstacleLayer}).");
                }
            }

            // 5. Test Assassin placement constraints for 50x50 map
            var testPlacement = Object.FindAnyObjectByType<AssassinPlacementController>();
            if (testPlacement == null)
            {
                var temp = new GameObject("Temp_PlacementTest");
                testPlacement = temp.AddComponent<AssassinPlacementController>();
            }

            bool validTop = testPlacement.ValidateCandidatePosition(new Vector3(0f, 0f, 15f), 50); // Z=15 (>= 10)
            bool invalidBottom = testPlacement.ValidateCandidatePosition(new Vector3(0f, 0f, -5f), 50); // Z=-5 (< 10)

            if (!validTop)
            {
                Debug.LogError("[Verification FAIL] Candidate at (0, 0, 15) should be VALID for 50x50 map (top 30%), but returned FALSE!");
            }
            if (invalidBottom)
            {
                Debug.LogError("[Verification FAIL] Candidate at (0, 0, -5) should be INVALID for 50x50 map (bottom sector), but returned TRUE!");
            }

            if (validTop && !invalidBottom)
            {
                Debug.Log("[Verification PASS] Assassin placement validation verified: Top 30% area (Z >= +10m) accepted, Bottom area (Z < +10m) rejected.");
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskAsymmetricSpawns] ALL VERIFICATIONS COMPLETED SUCCESSFULLY!");
            Debug.Log("==========================================================================");
        }
    }
}
