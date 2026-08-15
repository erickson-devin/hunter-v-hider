using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HunterVsHider.Vision;
using HunterVsHider.Player;
using HunterVsHider.Gameplay;
using HunterVsHider.Cameras;

namespace HunterVsHider.EditorScripts
{
    [InitializeOnLoad]
    public static class SetupFoWSystem
    {
        static SetupFoWSystem()
        {
            EditorApplication.delayCall += ExecuteSetup;
        }

        [InitializeOnLoadMethod]
        public static void OnInit()
        {
            EditorApplication.delayCall += ExecuteSetup;
        }

        [MenuItem("HunterVsHider/Setup FoW & Vision System")]
        public static void ExecuteSetup()
        {
            if (Application.isPlaying) return;

            Debug.Log("[SetupFoWSystem] Starting complete setup of 2.5D Vision & Team Fog of War system...");

            // 1. Ensure TagManager Layers exist: ObstacleHigh and ObstacleLow
            EnsureLayer("ObstacleHigh", 11);
            EnsureLayer("ObstacleLow", 12);
            EnsureLayer("Obstacle", 7);

            // 2. Setup Materials
            Material blitMat = EnsureMaterial("Assets/Materials/Mat_FogOfWarBlit.mat", "Custom/FogOfWarBlit", mat =>
            {
                mat.SetColor("_UnexploredColor", new Color(0.039f, 0.039f, 0.047f, 1.0f)); // #0A0A0C
                mat.SetColor("_ExploredColor", new Color(0.106f, 0.133f, 0.173f, 0.72f)); // #1B222C
                mat.SetFloat("_UnexploredAlpha", 1.0f);
                mat.SetFloat("_ExploredAlpha", 0.72f);
            });

            Material lowCoverMat = EnsureMaterial("Assets/Materials/Mat_Cover_Low.mat", "Standard", mat =>
            {
                mat.color = new Color(0.72f, 0.45f, 0.20f, 1.0f); // Warm tactical orange/brown crate
                mat.SetFloat("_Glossiness", 0.3f);
            });

            Material assassinMat = EnsureMaterial("Assets/Materials/Mat_Assassin.mat", "Standard", mat =>
            {
                mat.color = new Color(0.85f, 0.15f, 0.20f, 1.0f); // Red Assassin
                mat.SetFloat("_Glossiness", 0.5f);
            });

            Material fovMaskMat = EnsureMaterial("Assets/Materials/Mat_FOV_Mask.mat", "Custom/FoW_Mask", null);

            // 3. Open Tactical_Main Scene
            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            bool wasAnotherSceneActive = activeScene.path != scenePath;

            Scene targetScene;
            if (wasAnotherSceneActive)
            {
                targetScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            else
            {
                targetScene = activeScene;
            }

            bool sceneModified = false;

            int highLayer = LayerMask.NameToLayer("ObstacleHigh");
            int lowLayer = LayerMask.NameToLayer("ObstacleLow");

            // 4. Update 3m Walls to ObstacleHigh
            GameObject mapRoot = GameObject.Find("_TacticalMap");
            if (mapRoot != null)
            {
                MeshRenderer[] renderers = mapRoot.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var mr in renderers)
                {
                    if (mr.gameObject.name.StartsWith("Wall_") || mr.gameObject.name.StartsWith("Central_") ||
                        mr.gameObject.name.StartsWith("WestRoom_") || mr.gameObject.name.StartsWith("EastRoom_"))
                    {
                        if (mr.gameObject.layer != highLayer)
                        {
                            mr.gameObject.layer = highLayer;
                            EditorUtility.SetDirty(mr.gameObject);
                            sceneModified = true;
                        }
                    }
                }

                // 5. Add Low Cover Boxes on ObstacleLow (1m height cover)
                Transform lowCoverGroup = mapRoot.transform.Find("LowCoverGroup");
                if (lowCoverGroup == null)
                {
                    GameObject lcg = new GameObject("LowCoverGroup");
                    lcg.transform.SetParent(mapRoot.transform, false);
                    lowCoverGroup = lcg.transform;
                    sceneModified = true;
                }

                // Create 1m cover obstacles
                EnsureLowCoverBox(lowCoverGroup.gameObject, "Cover_CenterCorridor", new Vector3(0f, 0.5f, -8f), new Vector3(3f, 1f, 1f), lowCoverMat, lowLayer, ref sceneModified);
                EnsureLowCoverBox(lowCoverGroup.gameObject, "Cover_CentralRoom", new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 1f), lowCoverMat, lowLayer, ref sceneModified);
                EnsureLowCoverBox(lowCoverGroup.gameObject, "Cover_EastCorridor", new Vector3(10f, 0.5f, 0f), new Vector3(1.5f, 1f, 2f), lowCoverMat, lowLayer, ref sceneModified);
                EnsureLowCoverBox(lowCoverGroup.gameObject, "Cover_WestCorridor", new Vector3(-10f, 0.5f, 0f), new Vector3(1.5f, 1f, 2f), lowCoverMat, lowLayer, ref sceneModified);
            }

            // 6. Setup FogOfWarManager GameObject in Scene
            GameObject fowObj = GameObject.Find("_FogOfWarManager");
            if (fowObj == null)
            {
                fowObj = new GameObject("_FogOfWarManager");
                sceneModified = true;
            }
            FogOfWarManager fowManager = fowObj.GetComponent<FogOfWarManager>();
            if (fowManager == null)
            {
                fowManager = fowObj.AddComponent<FogOfWarManager>();
                sceneModified = true;
            }

            // 7. Setup Main Camera with Depth & FoW_ScreenDarkness
            GameObject mainCamObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCamObj == null) mainCamObj = GameObject.Find("Main Camera");

            if (mainCamObj != null)
            {
                Camera cam = mainCamObj.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.depthTextureMode |= DepthTextureMode.Depth;
                    EditorUtility.SetDirty(cam);
                }

                TacticalCamera tacCam = mainCamObj.GetComponent<TacticalCamera>();
                if (tacCam == null)
                {
                    tacCam = mainCamObj.AddComponent<TacticalCamera>();
                    tacCam.height = 18f;
                    tacCam.pitch = 60f;
                    sceneModified = true;
                }

                Transform screenDarknessTrans = mainCamObj.transform.Find("FoW_ScreenDarkness");
                GameObject screenDarkness;
                if (screenDarknessTrans == null)
                {
                    screenDarkness = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    screenDarkness.name = "FoW_ScreenDarkness";
                    screenDarkness.transform.SetParent(mainCamObj.transform, false);
                    sceneModified = true;
                }
                else
                {
                    screenDarkness = screenDarknessTrans.gameObject;
                }

                screenDarkness.transform.localPosition = new Vector3(0, 0, 1.5f);
                screenDarkness.transform.localRotation = Quaternion.identity;
                screenDarkness.transform.localScale = new Vector3(50, 50, 1);

                Collider col = screenDarkness.GetComponent<Collider>();
                if (col != null) { Object.DestroyImmediate(col); sceneModified = true; }

                MeshRenderer mr = screenDarkness.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != blitMat)
                {
                    mr.sharedMaterial = blitMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    sceneModified = true;
                }
            }

            // 8. Setup Player (Police) Prefab and Scene Instance
            GameObject player = GameObject.Find("Player") ?? GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (mainCamObj != null)
                {
                    TacticalCamera tacCam = mainCamObj.GetComponent<TacticalCamera>();
                    if (tacCam != null && tacCam.target == null)
                    {
                        tacCam.target = player.transform;
                        EditorUtility.SetDirty(tacCam);
                        sceneModified = true;
                    }
                }

                VisionController vc = player.GetComponent<VisionController>();
                if (vc == null)
                {
                    vc = player.AddComponent<VisionController>();
                    sceneModified = true;
                }

                SerializedObject vcSo = new SerializedObject(vc);
                vcSo.FindProperty("eyeOffset").vector3Value = new Vector3(0f, 1.8f, 0f);
                vcSo.FindProperty("viewAngle").floatValue = 90f;
                vcSo.FindProperty("viewDistance").floatValue = 25f;
                vcSo.FindProperty("rayCount").intValue = 120;
                vcSo.FindProperty("obstacleHighLayer").intValue = (1 << highLayer) | (1 << LayerMask.NameToLayer("Obstacle"));
                vcSo.FindProperty("obstacleLowLayer").intValue = (1 << lowLayer);
                vcSo.FindProperty("defaultLowObstacleHeight").floatValue = 1.0f;
                vcSo.FindProperty("groundPlaneY").floatValue = 0f;
                vcSo.ApplyModifiedProperties();

                // Setup FOV_Indicator child
                Transform fovTrans = player.transform.Find("FOV_Indicator");
                GameObject fovObjChild;
                if (fovTrans == null)
                {
                    fovObjChild = new GameObject("FOV_Indicator");
                    fovObjChild.transform.SetParent(player.transform, false);
                    fovTrans = fovObjChild.transform;
                    sceneModified = true;
                }
                else
                {
                    fovObjChild = fovTrans.gameObject;
                }

                fovTrans.localPosition = new Vector3(0f, 0.05f, 0f);
                fovTrans.localRotation = Quaternion.identity;
                fovTrans.localScale = Vector3.one;

                FieldOfView fovScript = fovObjChild.GetComponent<FieldOfView>();
                if (fovScript == null)
                {
                    // Clean up any old FOVMeshRenderer
                    FOVMeshRenderer oldMeshRenderer = fovObjChild.GetComponent<FOVMeshRenderer>();
                    if (oldMeshRenderer != null) Object.DestroyImmediate(oldMeshRenderer);

                    fovScript = fovObjChild.AddComponent<FieldOfView>();
                    sceneModified = true;
                }

                MeshRenderer fovMr = fovObjChild.GetComponent<MeshRenderer>();
                if (fovMr != null && fovMr.sharedMaterial != fovMaskMat)
                {
                    fovMr.sharedMaterial = fovMaskMat;
                    fovMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    fovMr.receiveShadows = false;
                    sceneModified = true;
                }

                // Also strip obsolete FoW_ShadowOverlay if on player
                Transform oldShadowOverlay = player.transform.Find("FoW_ShadowOverlay");
                if (oldShadowOverlay != null)
                {
                    Object.DestroyImmediate(oldShadowOverlay.gameObject);
                    sceneModified = true;
                }

                // Save to Player.prefab
                string prefabPath = "Assets/_Project/Prefabs/Player.prefab";
                if (System.IO.File.Exists(prefabPath))
                {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(player, prefabPath, InteractionMode.AutomatedAction);
                    Debug.Log("[SetupFoWSystem] Updated Player.prefab");
                }
            }

            // 9. Setup Target Dummy & Assassin Entities with TargetVisibility
            SetupTargetDummy("TargetDummy_01", new Vector3(-15f, 1f, 0f), assassinMat, ref sceneModified);
            SetupTargetDummy("TargetDummy_02", new Vector3(0f, 1f, 12f), assassinMat, ref sceneModified);
            SetupTargetDummy("Assassin_Dummy", new Vector3(18f, 1f, 0f), assassinMat, ref sceneModified);

            // 10. Save Assets & Scene
            if (sceneModified)
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
                EditorSceneManager.SaveScene(targetScene);
                Debug.Log("[SetupFoWSystem] Tactical_Main scene saved with complete FoW and 2.5D Vision configuration!");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupFoWSystem] Setup complete!");
            EditorApplication.delayCall -= ExecuteSetup;
        }

        private static void EnsureLowCoverBox(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat, int layer, ref bool sceneModified)
        {
            Transform existing = parent.transform.Find(name);
            GameObject box;
            if (existing == null)
            {
                box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = name;
                box.transform.SetParent(parent.transform, false);
                sceneModified = true;
            }
            else
            {
                box = existing.gameObject;
            }

            if (box.transform.position != pos) { box.transform.position = pos; sceneModified = true; }
            if (box.transform.localScale != scale) { box.transform.localScale = scale; sceneModified = true; }
            if (box.layer != layer) { box.layer = layer; sceneModified = true; }

            MeshRenderer mr = box.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != mat)
            {
                mr.sharedMaterial = mat;
                sceneModified = true;
            }
        }

        private static void SetupTargetDummy(string name, Vector3 position, Material mat, ref bool sceneModified)
        {
            GameObject dummy = GameObject.Find(name);
            if (dummy == null)
            {
                dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = name;
                dummy.transform.position = position;
                dummy.transform.localScale = new Vector3(1f, 1f, 1f);
                sceneModified = true;
            }

            MeshRenderer mr = dummy.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != mat)
            {
                mr.sharedMaterial = mat;
                sceneModified = true;
            }

            TargetDummy td = dummy.GetComponent<TargetDummy>();
            if (td == null)
            {
                td = dummy.AddComponent<TargetDummy>();
                sceneModified = true;
            }

            TargetVisibility tv = dummy.GetComponent<TargetVisibility>();
            if (tv == null)
            {
                tv = dummy.AddComponent<TargetVisibility>();
                sceneModified = true;
            }
        }

        private static Material EnsureMaterial(string path, string shaderName, System.Action<Material> configure)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError($"[SetupFoWSystem] Could not find shader: {shaderName}");
                    return null;
                }
                mat = new Material(shader);
                configure?.Invoke(mat);
                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();
            }
            else
            {
                configure?.Invoke(mat);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static void EnsureLayer(string layerName, int preferredIndex)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return; // Layer already exists
                }
            }

            // Assign at preferredIndex if empty, otherwise find first empty slot >= 8
            if (preferredIndex < layers.arraySize && string.IsNullOrEmpty(layers.GetArrayElementAtIndex(preferredIndex).stringValue))
            {
                layers.GetArrayElementAtIndex(preferredIndex).stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[SetupFoWSystem] Created layer: {layerName} at index {preferredIndex}");
                return;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[SetupFoWSystem] Created layer: {layerName} at index {i}");
                    return;
                }
            }
        }
    }
}
