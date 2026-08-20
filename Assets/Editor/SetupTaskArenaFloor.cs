using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HunterVsHider.EditorScripts
{
    [InitializeOnLoad]
    public class SetupTaskArenaFloor
    {
        static SetupTaskArenaFloor()
        {
            EditorApplication.delayCall += ExecuteSetup;
        }

        [MenuItem("Tools/Hunter v Hider/Scale and Style Arena Floor")]
        public static void ExecuteSetup()
        {
            if (Application.isPlaying) return;

            Debug.Log("[SetupTaskArenaFloor] Starting Arena Floor Scaling and Tactical Styling...");

            // 1. Create or Update Mat_TacticalFloor
            string matPath = "Assets/_Project/Materials/Mat_TacticalFloor.mat";
            Directory.CreateDirectory("Assets/_Project/Materials");

            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (floorMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                floorMat = new Material(shader);
                floorMat.color = new Color(0.031f, 0.043f, 0.071f, 1f); // Near-black tactical shade (#080B12)
                if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0.15f);
                AssetDatabase.CreateAsset(floorMat, matPath);
                Debug.Log($"[SetupTaskArenaFloor] Created Material: {matPath}");
            }
            else
            {
                floorMat.color = new Color(0.031f, 0.043f, 0.071f, 1f); // #080B12
                if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0.15f);
                EditorUtility.SetDirty(floorMat);
            }

            // 2. Open Tactical_Main Scene
            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[SetupTaskArenaFloor] Could not open scene {scenePath}!");
                return;
            }

            bool anyChanged = false;

            GameObject combatArena = GameObject.Find("Zone_CombatArena");
            if (combatArena == null)
            {
                combatArena = new GameObject("Zone_CombatArena");
                combatArena.transform.position = Vector3.zero;
                combatArena.transform.rotation = Quaternion.identity;
                combatArena.transform.localScale = Vector3.one;
                anyChanged = true;
            }

            // 3. Locate or Create the Floor inside Zone_CombatArena
            GameObject floorObj = null;

            // Check children of Zone_CombatArena for existing floor
            for (int i = 0; i < combatArena.transform.childCount; i++)
            {
                Transform child = combatArena.transform.GetChild(i);
                string childName = child.name.ToLower();
                if (childName.Contains("floor") || childName.Contains("ground") || childName.Contains("plane"))
                {
                    floorObj = child.gameObject;
                    break;
                }
            }

            // Check scene-level for floor if not parented
            if (floorObj == null)
            {
                GameObject[] rootObjs = activeScene.GetRootGameObjects();
                foreach (var obj in rootObjs)
                {
                    string objName = obj.name.ToLower();
                    if (objName == "floor" || objName == "ground" || objName == "arena_floor" || objName == "floor_combatarena")
                    {
                        floorObj = obj;
                        floorObj.transform.SetParent(combatArena.transform, true);
                        anyChanged = true;
                        break;
                    }
                }
            }

            // If still null, create a 300x300 Cube Floor
            if (floorObj == null)
            {
                floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floorObj.name = "Floor_CombatArena";
                floorObj.transform.SetParent(combatArena.transform, false);
                anyChanged = true;
                Debug.Log("[SetupTaskArenaFloor] Created new Floor_CombatArena Cube primitive.");
            }

            // 4. Determine Mesh Type and Apply 300x300 Meter Scaling
            MeshFilter mf = floorObj.GetComponent<MeshFilter>();
            bool isPlane = (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.ToLower().Contains("plane"));

            if (isPlane)
            {
                // Unity Plane is 10x10 units base -> Scale 30 yields 300x300 meters
                floorObj.transform.localPosition = new Vector3(0f, 0f, 0f);
                floorObj.transform.localRotation = Quaternion.identity;
                floorObj.transform.localScale = new Vector3(30f, 1f, 30f);
            }
            else
            {
                // Unity Cube is 1x1x1 unit base -> Scale 300x1x300 yields 300x300 meters
                // Top surface at Y=0 (baseline) with 1m thickness
                floorObj.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                floorObj.transform.localRotation = Quaternion.identity;
                floorObj.transform.localScale = new Vector3(300f, 1f, 300f);
            }

            // 5. Apply Material, Collider, and Layer
            Renderer rend = floorObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = floorMat;
            }

            Collider col = floorObj.GetComponent<Collider>();
            if (col == null)
            {
                if (isPlane) floorObj.AddComponent<MeshCollider>();
                else floorObj.AddComponent<BoxCollider>();
            }

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer != -1)
            {
                floorObj.layer = groundLayer;
            }

            EditorUtility.SetDirty(floorObj);
            anyChanged = true;

            // 6. Save Scene
            if (anyChanged)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[SetupTaskArenaFloor] Successfully scaled floor '{floorObj.name}' to 300x300 meters with Mat_TacticalFloor ({matPath}) and saved Tactical_Main.");
            }

            EditorApplication.delayCall -= ExecuteSetup;
        }
    }
}
