using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HunterVsHider.Map;

namespace HunterVsHider.EditorScripts
{
    public class SetupTaskShaderRecovery
    {
        [MenuItem("Tools/Hunter v Hider/Recover Shaders and Apply Gray-Box Theme")]
        public static void ExecuteSetup()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;
            if (Application.isPlaying) return;

            Debug.Log("[SetupTaskShaderRecovery] Starting Shader Recovery & Gray-Box Styling...");

            Directory.CreateDirectory("Assets/_Project/Materials");
            Directory.CreateDirectory("Assets/_Project/Prefabs");

            Shader standardShader = Shader.Find("Standard");
            if (standardShader == null)
            {
                standardShader = Shader.Find("Universal Render Pipeline/Lit");
            }

            // 1. Recover Mat_TacticalFloor (Dark Grey #333333, Smoothness = 0)
            string floorMatPath = "Assets/_Project/Materials/Mat_TacticalFloor.mat";
            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(floorMatPath);
            if (floorMat == null)
            {
                floorMat = new Material(standardShader);
                AssetDatabase.CreateAsset(floorMat, floorMatPath);
            }
            else
            {
                floorMat.shader = standardShader;
            }

            Color darkGreyColor = new Color(0.2f, 0.2f, 0.2f, 1f); // #333333
            floorMat.color = darkGreyColor;
            if (floorMat.HasProperty("_Color")) floorMat.SetColor("_Color", darkGreyColor);
            if (floorMat.HasProperty("_BaseColor")) floorMat.SetColor("_BaseColor", darkGreyColor);
            if (floorMat.HasProperty("_Glossiness")) floorMat.SetFloat("_Glossiness", 0f);
            if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0f);
            if (floorMat.HasProperty("_Metallic")) floorMat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(floorMat);
            Debug.Log($"[SetupTaskShaderRecovery] Updated Mat_TacticalFloor with shader '{floorMat.shader.name}' and color #333333.");

            // 2. Recover Mat_TacticalWall (Lighter Grey #888888, Smoothness = 0)
            string wallMatPath = "Assets/_Project/Materials/Mat_TacticalWall.mat";
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(wallMatPath);
            if (wallMat == null)
            {
                wallMat = new Material(standardShader);
                AssetDatabase.CreateAsset(wallMat, wallMatPath);
            }
            else
            {
                wallMat.shader = standardShader;
            }

            Color lightGreyColor = new Color(0.533f, 0.533f, 0.533f, 1f); // #888888
            wallMat.color = lightGreyColor;
            if (wallMat.HasProperty("_Color")) wallMat.SetColor("_Color", lightGreyColor);
            if (wallMat.HasProperty("_BaseColor")) wallMat.SetColor("_BaseColor", lightGreyColor);
            if (wallMat.HasProperty("_Glossiness")) wallMat.SetFloat("_Glossiness", 0f);
            if (wallMat.HasProperty("_Smoothness")) wallMat.SetFloat("_Smoothness", 0f);
            if (wallMat.HasProperty("_Metallic")) wallMat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(wallMat);
            Debug.Log($"[SetupTaskShaderRecovery] Updated Mat_TacticalWall with shader '{wallMat.shader.name}' and color #888888.");

            // 3. Update Prefab_TacticalWall
            string prefabPath = "Assets/_Project/Prefabs/Prefab_TacticalWall.prefab";
            GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (wallPrefab != null)
            {
                Renderer rend = wallPrefab.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = wallMat;
                    EditorUtility.SetDirty(wallPrefab);
                }
            }
            else
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

                wallPrefab = PrefabUtility.SaveAsPrefabAsset(tempWall, prefabPath);
                Object.DestroyImmediate(tempWall);
            }
            Debug.Log($"[SetupTaskShaderRecovery] Configured Prefab_TacticalWall with Mat_TacticalWall.");

            // 4. Update Tactical_Main Scene Objects
            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (activeScene.IsValid())
            {
                GameObject combatArena = GameObject.Find("Zone_CombatArena");
                if (combatArena != null)
                {
                    // Find floor and assign floorMat
                    for (int i = 0; i < combatArena.transform.childCount; i++)
                    {
                        Transform child = combatArena.transform.GetChild(i);
                        string cName = child.name.ToLower();
                        if (cName.Contains("floor") || cName.Contains("ground") || cName.Contains("plane"))
                        {
                            Renderer rend = child.GetComponent<Renderer>();
                            if (rend != null) rend.sharedMaterial = floorMat;
                            EditorUtility.SetDirty(child.gameObject);
                        }
                    }

                    // Find MapGenerator and assign wallPrefab and wallMaterial
                    Transform mapGenTrans = combatArena.transform.Find("MapGenerator");
                    if (mapGenTrans != null)
                    {
                        MapGenerator mapGen = mapGenTrans.GetComponent<MapGenerator>();
                        if (mapGen != null)
                        {
                            mapGen.wallPrefab = wallPrefab;
                            mapGen.wallMaterial = wallMat;
                            EditorUtility.SetDirty(mapGen);
                        }
                    }

                    EditorSceneManager.MarkSceneDirty(activeScene);
                    EditorSceneManager.SaveScene(activeScene);
                    Debug.Log("[SetupTaskShaderRecovery] Applied updated materials and saved Tactical_Main scene.");
                }
            }

            AssetDatabase.SaveAssets();
            EditorApplication.delayCall -= ExecuteSetup;
        }
    }
}
