using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HunterVsHider.Vision;
using HunterVsHider.Player;

public class SetupTaskFoW
{
    [MenuItem("Tools/Hunter v Hider/Setup 3-Tier Fog of War")]
    public static void ExecuteSetup()
    {
        if (ParrelSync.ClonesManager.IsClone()) return;
        if (Application.isPlaying) return;

        Debug.Log("[SetupTaskFoW] Starting 3-Tier Fog of War Setup...");
        bool anyChanged = false;

        // Ensure Layer 11 is VisionMask
        int visionLayer = LayerMask.NameToLayer("VisionMask");
        if (visionLayer == -1)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp != null && layersProp.arraySize > 11)
            {
                layersProp.GetArrayElementAtIndex(11).stringValue = "VisionMask";
                tagManager.ApplyModifiedProperties();
                Debug.Log("[SetupTaskFoW] Configured Layer 11 as VisionMask in TagManager.");
            }
            visionLayer = 11;
        }

        // Load Materials & Assets
        Material wedgeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Mat_VisionWedge_White.mat");
        Material flashlightMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Mat_Police_Flashlight.mat");
        Material assassinMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Mat_Assassin_Radius.mat");
        Material fowWorldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Mat_FoW_World.mat");
        RenderTexture fovMaskRT = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/_Project/Materials/FoVMask_RT.renderTexture");

        // 1. Setup Player Prefab FOV Mask & Visuals
        string playerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab != null)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(playerPrefabPath);
            bool prefabDirty = false;

            // Clean up legacy FOV_Wedge if present
            Transform legacyWedge = prefabRoot.transform.Find("FOV_Wedge");
            if (legacyWedge != null)
            {
                legacyWedge.gameObject.name = "FOV_Mask";
                prefabDirty = true;
            }

            // Ensure PlayerVisibilityManager
            PlayerVisibilityManager visMgr = prefabRoot.GetComponent<PlayerVisibilityManager>();
            if (visMgr == null)
            {
                visMgr = prefabRoot.AddComponent<PlayerVisibilityManager>();
                visMgr.obstacleMask = 1 << 7; // Layer 7: Obstacle
                prefabDirty = true;
            }

            // Dynamic FOV Mask (VisionMask layer only)
            Transform maskTrans = prefabRoot.transform.Find("FOV_DynamicMask");
            if (maskTrans == null) maskTrans = prefabRoot.transform.Find("FOV_Mask");
            GameObject maskObj;
            if (maskTrans == null)
            {
                maskObj = new GameObject("FOV_DynamicMask");
                maskObj.transform.SetParent(prefabRoot.transform, false);
                maskObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                maskObj.transform.localRotation = Quaternion.identity;
                maskObj.transform.localScale = Vector3.one;
                prefabDirty = true;
            }
            else
            {
                maskObj = maskTrans.gameObject;
                maskObj.name = "FOV_DynamicMask";
            }

            if (maskObj.layer != visionLayer)
            {
                maskObj.layer = visionLayer;
                prefabDirty = true;
            }

            FOVWedgeMesh oldWedgeMesh = maskObj.GetComponent<FOVWedgeMesh>();
            if (oldWedgeMesh != null)
            {
                Object.DestroyImmediate(oldWedgeMesh, true);
                prefabDirty = true;
            }

            DynamicFOV dynamicFov = maskObj.GetComponent<DynamicFOV>();
            if (dynamicFov == null)
            {
                dynamicFov = maskObj.AddComponent<DynamicFOV>();
                prefabDirty = true;
            }
            dynamicFov.visionMaterial = wedgeMat;
            dynamicFov.viewRadius = 15f;
            dynamicFov.viewAngle = 90f;
            dynamicFov.proximityRadius = 2.5f;
            dynamicFov.wallTopOvershoot = 0.25f;
            dynamicFov.rayCount = 180;
            dynamicFov.obstacleMask = 1 << 7;
            dynamicFov.EnsureLayerAndMaterial();

            // Clean up deprecated Police_ProximityMask child if present
            Transform proxTrans = prefabRoot.transform.Find("Police_ProximityMask");
            if (proxTrans != null)
            {
                Object.DestroyImmediate(proxTrans.gameObject, true);
                prefabDirty = true;
            }

            // FOV_Visuals (Default layer)
            Transform visualsTrans = prefabRoot.transform.Find("FOV_Visuals");
            GameObject visualsObj;
            if (visualsTrans == null)
            {
                visualsObj = new GameObject("FOV_Visuals");
                visualsObj.transform.SetParent(prefabRoot.transform, false);
                visualsObj.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                visualsObj.transform.localRotation = Quaternion.identity;
                visualsObj.transform.localScale = Vector3.one;
                prefabDirty = true;
            }
            else
            {
                visualsObj = visualsTrans.gameObject;
            }

            // Police_Flashlight
            Transform flashlightTrans = visualsObj.transform.Find("Police_Flashlight");
            GameObject flashlightObj;
            if (flashlightTrans == null)
            {
                flashlightObj = new GameObject("Police_Flashlight");
                flashlightObj.transform.SetParent(visualsObj.transform, false);
                flashlightObj.transform.localPosition = Vector3.zero;
                flashlightObj.transform.localRotation = Quaternion.identity;
                flashlightObj.transform.localScale = Vector3.one;
                prefabDirty = true;
            }
            else
            {
                flashlightObj = flashlightTrans.gameObject;
            }
            flashlightObj.layer = 0; // Default

            FOVWedgeMesh flashlightMesh = flashlightObj.GetComponent<FOVWedgeMesh>();
            if (flashlightMesh == null)
            {
                flashlightMesh = flashlightObj.AddComponent<FOVWedgeMesh>();
                prefabDirty = true;
            }
            flashlightMesh.wedgeMaterial = flashlightMat;
            flashlightMesh.viewRadius = 15f;
            flashlightMesh.viewAngle = 90f;
            flashlightMesh.segments = 32;
            flashlightMesh.GenerateMesh();
            flashlightMesh.EnsureLayerAndMaterial();

            // Clean up deprecated Assassin_Radius visual ring if present
            Transform assassinTrans = visualsObj.transform.Find("Assassin_Radius");
            if (assassinTrans != null)
            {
                Object.DestroyImmediate(assassinTrans.gameObject, true);
                prefabDirty = true;
            }

            if (prefabDirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, playerPrefabPath);
                anyChanged = true;
                Debug.Log("[SetupTaskFoW] Saved Unified DynamicFOV and Police_Flashlight on Player.prefab");
            }
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // 2. Setup Scene Tactical_Main
        string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        if (activeScene.IsValid())
        {
            bool sceneDirty = false;

            // Ensure Main Camera excludes VisionMask layer
            Camera mainCam = UnityEngine.Camera.main;
            if (mainCam != null)
            {
                int oldMask = mainCam.cullingMask;
                int newMask = oldMask & ~(1 << visionLayer);
                if (oldMask != newMask)
                {
                    mainCam.cullingMask = newMask;
                    EditorUtility.SetDirty(mainCam);
                    sceneDirty = true;
                    Debug.Log("[SetupTaskFoW] Excluded VisionMask from Main Camera culling mask.");
                }
            }

            // Vision Camera
            GameObject visionCamObj = GameObject.Find("VisionCamera");
            if (visionCamObj == null)
            {
                visionCamObj = new GameObject("VisionCamera");
                sceneDirty = true;
            }

            visionCamObj.transform.position = new Vector3(0f, 50f, 0f);
            visionCamObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Camera visionCam = visionCamObj.GetComponent<Camera>();
            if (visionCam == null)
            {
                visionCam = visionCamObj.AddComponent<Camera>();
                sceneDirty = true;
            }

            visionCam.orthographic = true;
            visionCam.orthographicSize = 25f; // Covers 50m x 50m arena
            visionCam.clearFlags = CameraClearFlags.SolidColor;
            visionCam.backgroundColor = Color.black;
            visionCam.cullingMask = 1 << visionLayer;
            visionCam.targetTexture = fovMaskRT;
            visionCam.nearClipPlane = 0.3f;
            visionCam.farClipPlane = 100f;
            visionCam.depth = -10; // Render before main camera

            FogMemoryManager memMgr = visionCamObj.GetComponent<FogMemoryManager>();
            if (memMgr == null)
            {
                memMgr = visionCamObj.AddComponent<FogMemoryManager>();
                sceneDirty = true;
            }
            memMgr.fovMaskRT = fovMaskRT;
            memMgr.memoryFloor = 0.5f;

            // Configure Screen-Space Fog of War on Main Camera
            if (mainCam != null)
            {
                FoW_PostProcessFeature fowFeature = mainCam.GetComponent<FoW_PostProcessFeature>();
                if (fowFeature == null)
                {
                    fowFeature = mainCam.gameObject.AddComponent<FoW_PostProcessFeature>();
                    sceneDirty = true;
                }
                Shader screenShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Materials/FoW_ScreenSpace.shader");
                if (fowFeature.fowShader != screenShader)
                {
                    fowFeature.fowShader = screenShader;
                    sceneDirty = true;
                }
                fowFeature.fovMaskRT = fovMaskRT;
                fowFeature.mapBoundsMin = new Vector2(-25f, -25f);
                fowFeature.mapBoundsSize = new Vector2(50f, 50f);
                fowFeature.memoryDarkness = 0.5f;
            }

            // Remove legacy FoW_Plane if present
            GameObject fowPlaneObj = GameObject.Find("FoW_Plane");
            if (fowPlaneObj != null)
            {
                Object.DestroyImmediate(fowPlaneObj, true);
                sceneDirty = true;
                Debug.Log("[SetupTaskFoW] Removed deprecated physical FoW_Plane.");
            }

            if (sceneDirty)
            {
                if (mainCam != null) EditorUtility.SetDirty(mainCam.gameObject);
                EditorUtility.SetDirty(visionCamObj);
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                anyChanged = true;
                Debug.Log("[SetupTaskFoW] Saved VisionCamera and Screen-Space FoW in Tactical_Main.unity");
            }
        }

        if (anyChanged)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupTaskFoW] 3-Tier Fog of War Setup completed successfully.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
