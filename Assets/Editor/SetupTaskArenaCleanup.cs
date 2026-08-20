using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HunterVsHider.Map;
using HunterVsHider.Managers;

namespace HunterVsHider.EditorScripts
{
    public class SetupTaskArenaCleanup
    {
        [MenuItem("Tools/Hunter v Hider/Cleanup Arena and Create Tactical Wall Prefab")]
        public static void ExecuteSetup()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;
            if (Application.isPlaying) return;

            Debug.Log("[SetupTaskArenaCleanup] Starting Arena Cleanup and Prefab creation...");

            // 1. Create or Load Mat_TacticalWall
            string matPath = "Assets/_Project/Materials/Mat_TacticalWall.mat";
            Directory.CreateDirectory("Assets/_Project/Materials");
            Directory.CreateDirectory("Assets/_Project/Prefabs");

            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (wallMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                wallMat = new Material(shader);
                wallMat.color = new Color(0.075f, 0.098f, 0.141f, 1f); // Dark tactical navy/slate (#131924)
                if (wallMat.HasProperty("_Smoothness")) wallMat.SetFloat("_Smoothness", 0.25f);
                AssetDatabase.CreateAsset(wallMat, matPath);
                Debug.Log($"[SetupTaskArenaCleanup] Created Material: {matPath}");
            }
            else
            {
                wallMat.color = new Color(0.075f, 0.098f, 0.141f, 1f);
                EditorUtility.SetDirty(wallMat);
            }

            // 2. Create or Update Prefab_TacticalWall
            string prefabPath = "Assets/_Project/Prefabs/Prefab_TacticalWall.prefab";
            GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (wallPrefab == null)
            {
                GameObject tempWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tempWall.name = "Prefab_TacticalWall";
                
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer == -1) obstacleLayer = 0;
                tempWall.layer = obstacleLayer;
                tempWall.tag = "Obstacle";

                Renderer rend = tempWall.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = wallMat;

                BoxCollider boxCol = tempWall.GetComponent<BoxCollider>();
                if (boxCol == null) tempWall.AddComponent<BoxCollider>();

                // Ensure NO NetworkObject
                var netObj = tempWall.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null) Object.DestroyImmediate(netObj);

                wallPrefab = PrefabUtility.SaveAsPrefabAsset(tempWall, prefabPath);
                Object.DestroyImmediate(tempWall);
                Debug.Log($"[SetupTaskArenaCleanup] Created Prefab: {prefabPath}");
            }

            // 3. Clean Tactical_Main Scene
            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[SetupTaskArenaCleanup] Could not open scene {scenePath}!");
                return;
            }

            bool anyChanged = false;

            GameObject combatArena = GameObject.Find("Zone_CombatArena");
            if (combatArena == null)
            {
                combatArena = new GameObject("Zone_CombatArena");
                anyChanged = true;
            }

            // Clean all prototype static geometry under Zone_CombatArena
            // Keep ONLY: MapGenerator and Floor/Ground objects
            for (int i = combatArena.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = combatArena.transform.GetChild(i);
                string childName = child.name.ToLower();

                if (childName.Contains("mapgenerator") || childName.Contains("floor") || childName.Contains("ground") || childName.Contains("grid"))
                {
                    continue;
                }

                Debug.Log($"[SetupTaskArenaCleanup] Removing prototype arena geometry: {child.name}");
                Object.DestroyImmediate(child.gameObject);
                anyChanged = true;
            }

            // Ensure MapGenerator exists and has wallPrefab & wallMaterial wired
            Transform mapGenTrans = combatArena.transform.Find("MapGenerator");
            GameObject mapGenObj = (mapGenTrans != null) ? mapGenTrans.gameObject : null;
            if (mapGenObj == null)
            {
                mapGenObj = new GameObject("MapGenerator");
                mapGenObj.transform.SetParent(combatArena.transform, false);
                mapGenObj.transform.localPosition = Vector3.zero;
                mapGenObj.transform.localRotation = Quaternion.identity;
                mapGenObj.transform.localScale = Vector3.one;
                anyChanged = true;
            }

            MapGenerator mapGen = mapGenObj.GetComponent<MapGenerator>();
            if (mapGen == null)
            {
                mapGen = mapGenObj.AddComponent<MapGenerator>();
                anyChanged = true;
            }

            mapGen.wallPrefab = wallPrefab;
            mapGen.wallMaterial = wallMat;

            // Clear any previously spawned runtime/editor geometry under GeneratedEnvironment
            Transform envTrans = mapGenObj.transform.Find("GeneratedEnvironment");
            if (envTrans != null)
            {
                for (int i = envTrans.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(envTrans.GetChild(i).gameObject);
                    anyChanged = true;
                }
                mapGen.generatedEnvironment = envTrans;
            }
            else
            {
                GameObject envObj = new GameObject("GeneratedEnvironment");
                envObj.transform.SetParent(mapGenObj.transform, false);
                envObj.transform.localPosition = Vector3.zero;
                envObj.transform.localRotation = Quaternion.identity;
                envObj.transform.localScale = Vector3.one;
                mapGen.generatedEnvironment = envObj.transform;
                anyChanged = true;
            }

            EditorUtility.SetDirty(mapGen);

            if (anyChanged)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log("[SetupTaskArenaCleanup] Successfully cleaned Zone_CombatArena and saved Tactical_Main scene.");
            }

            EditorApplication.delayCall -= ExecuteSetup;
        }
    }
}
