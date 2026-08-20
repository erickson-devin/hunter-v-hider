using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HunterVsHider.Vision;

namespace HunterVsHider.EditorScripts
{
    [InitializeOnLoad]
    public class SetupTaskFoWBoundary
    {
        static SetupTaskFoWBoundary()
        {
            EditorApplication.delayCall += ExecuteSetup;
        }

        [MenuItem("Tools/Hunter v Hider/Expand FoW Boundary (300x300m)")]
        public static void ExecuteSetup()
        {
            if (Application.isPlaying) return;

            Debug.Log("[SetupTaskFoWBoundary] Expanding Fog of War boundaries to 300x300 meters...");

            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[SetupTaskFoWBoundary] Could not open scene {scenePath}!");
                return;
            }

            bool anyChanged = false;

            RenderTexture fovMaskRT = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/_Project/Materials/FoVMask_RT.renderTexture");
            int visionLayer = LayerMask.NameToLayer("VisionMask");
            if (visionLayer == -1) visionLayer = 11;

            // 1. Configure VisionCamera
            GameObject visionCamObj = GameObject.Find("VisionCamera");
            if (visionCamObj == null)
            {
                visionCamObj = new GameObject("VisionCamera");
                anyChanged = true;
            }

            visionCamObj.transform.position = new Vector3(0f, 100f, 0f);
            visionCamObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            visionCamObj.transform.localScale = Vector3.one;

            Camera visionCam = visionCamObj.GetComponent<Camera>();
            if (visionCam == null) visionCam = visionCamObj.AddComponent<Camera>();

            visionCam.orthographic = true;
            visionCam.orthographicSize = 150f; // Exactly 300m vertical and 300m horizontal at aspect 1:1
            visionCam.clearFlags = CameraClearFlags.SolidColor;
            visionCam.backgroundColor = Color.black;
            visionCam.cullingMask = 1 << visionLayer;
            visionCam.targetTexture = fovMaskRT;
            visionCam.nearClipPlane = 0.3f;
            visionCam.farClipPlane = 200f;
            visionCam.depth = -10;

            FogMemoryManager memMgr = visionCamObj.GetComponent<FogMemoryManager>();
            if (memMgr == null) memMgr = visionCamObj.AddComponent<FogMemoryManager>();
            memMgr.fovMaskRT = fovMaskRT;
            memMgr.memoryFloor = 0.5f;
            EditorUtility.SetDirty(visionCamObj);

            // 2. Configure FoW_PostProcessFeature on Main Camera
            Camera mainCam = UnityEngine.Camera.main;
            if (mainCam != null)
            {
                FoW_PostProcessFeature fowPost = mainCam.GetComponent<FoW_PostProcessFeature>();
                if (fowPost == null)
                {
                    fowPost = mainCam.gameObject.AddComponent<FoW_PostProcessFeature>();
                }

                Shader fowShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Materials/FoW_ScreenSpace.shader");
                if (fowShader == null) fowShader = Shader.Find("HunterVsHider/FoW_ScreenSpace");

                fowPost.fowShader = fowShader;
                fowPost.fovMaskRT = fovMaskRT;
                fowPost.worldCenter = Vector3.zero;
                fowPost.worldSize = new Vector2(300f, 300f);
                fowPost.textureSize = 2048;
                fowPost.mapBoundsMin = new Vector2(-150f, -150f);
                fowPost.mapBoundsSize = new Vector2(300f, 300f);
                fowPost.memoryDarkness = 0.5f;
                fowPost.fogColor = new Color(0.02f, 0.02f, 0.03f, 1f);

                DynamicFog dynamicFog = mainCam.GetComponent<DynamicFog>();
                if (dynamicFog == null)
                {
                    dynamicFog = mainCam.gameObject.AddComponent<DynamicFog>();
                }
                dynamicFog.worldCenter = Vector3.zero;
                dynamicFog.worldSize = new Vector2(300f, 300f);
                dynamicFog.textureSize = 2048;
                dynamicFog.SyncWithPostProcessFeature();

                EditorUtility.SetDirty(mainCam.gameObject);
                anyChanged = true;
            }

            // 3. Ensure any physical FoW_Plane is removed
            GameObject fowPlane = GameObject.Find("FoW_Plane");
            if (fowPlane != null)
            {
                Object.DestroyImmediate(fowPlane);
                anyChanged = true;
            }

            if (anyChanged)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log("[SetupTaskFoWBoundary] Successfully expanded FoW boundary to 300x300m and saved Tactical_Main.");
            }

            EditorApplication.delayCall -= ExecuteSetup;
        }
    }
}
