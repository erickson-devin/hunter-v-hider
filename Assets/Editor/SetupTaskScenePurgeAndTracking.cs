using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HunterVsHider.Vision;
using HunterVsHider.Map;

namespace HunterVsHider.EditorScripts
{
    public class SetupTaskScenePurgeAndTracking
    {
        [MenuItem("Tools/Hunter v Hider/Purge Scene, Normalize Fog Transform and Shadows")]
        public static void ExecuteSetup()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;
            if (Application.isPlaying) return;

            Debug.Log("[SetupTaskScenePurgeAndTracking] Starting Scene Purge, Fog Normalization, and Shadow Calibration...");

            // 1. Open Tactical_Main Scene
            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[SetupTaskScenePurgeAndTracking] Could not open scene {scenePath}!");
                return;
            }

            bool sceneDirty = false;

            // 2. Permanent Scene Purge: Remove _TacticalMap and all Test Dummies
            GameObject tacticalMap = GameObject.Find("_TacticalMap");
            if (tacticalMap != null)
            {
                Debug.Log("[SetupTaskScenePurgeAndTracking] Permanently deleting _TacticalMap and static prototype geometry.");
                Object.DestroyImmediate(tacticalMap);
                sceneDirty = true;
            }

            GameObject[] rootObjs = activeScene.GetRootGameObjects();
            foreach (var rootObj in rootObjs)
            {
                string objName = rootObj.name.ToLower();
                if (objName.Contains("dummy") || objName.Contains("targetdummy") || objName.Contains("testdummy") || objName == "_tacticalmap")
                {
                    Debug.Log($"[SetupTaskScenePurgeAndTracking] Permanently deleting prototype object: {rootObj.name}");
                    Object.DestroyImmediate(rootObj);
                    sceneDirty = true;
                }
            }

            // 3. Normalize Fog Volume Transform (Tracking Fix)
            // Create or ensure dedicated FogOfWarManager GameObject at (0, 0, 0)
            GameObject fogManagerObj = GameObject.Find("FogOfWarManager");
            if (fogManagerObj == null)
            {
                fogManagerObj = GameObject.Find("DynamicFog");
            }
            if (fogManagerObj == null)
            {
                fogManagerObj = new GameObject("FogOfWarManager");
                sceneDirty = true;
            }

            // Hard-reset transform to normalized origin (Position: 0,0,0; Rotation: 0,0,0; Scale: 1,1,1)
            fogManagerObj.transform.position = Vector3.zero;
            fogManagerObj.transform.rotation = Quaternion.identity;
            fogManagerObj.transform.localScale = Vector3.one;

            DynamicFog dynamicFog = fogManagerObj.GetComponent<DynamicFog>();
            if (dynamicFog == null) dynamicFog = fogManagerObj.AddComponent<DynamicFog>();
            dynamicFog.worldCenter = Vector3.zero;
            dynamicFog.worldSize = new Vector2(300f, 300f);
            dynamicFog.textureSize = 2048;
            dynamicFog.obstacleMask = 1 << LayerMask.NameToLayer("Obstacle");

            FoW_PostProcessFeature postProcessFeature = fogManagerObj.GetComponent<FoW_PostProcessFeature>();
            if (postProcessFeature == null) postProcessFeature = fogManagerObj.AddComponent<FoW_PostProcessFeature>();
            postProcessFeature.worldCenter = Vector3.zero;
            postProcessFeature.worldSize = new Vector2(300f, 300f);
            postProcessFeature.textureSize = 2048;
            postProcessFeature.mapBoundsMin = new Vector2(-150f, -150f);
            postProcessFeature.mapBoundsSize = new Vector2(300f, 300f);

            RenderTexture fovMaskRT = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/_Project/Materials/FoVMask_RT.renderTexture");
            postProcessFeature.fovMaskRT = fovMaskRT;

            Shader fowShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Materials/FoW_ScreenSpace.shader");
            if (fowShader == null) fowShader = Shader.Find("HunterVsHider/FoW_ScreenSpace");
            postProcessFeature.fowShader = fowShader;

            EditorUtility.SetDirty(fogManagerObj);

            // Also ensure Main Camera FoW_PostProcessFeature is synchronized
            Camera mainCam = UnityEngine.Camera.main;
            if (mainCam != null)
            {
                FoW_PostProcessFeature camFoW = mainCam.GetComponent<FoW_PostProcessFeature>();
                if (camFoW == null) camFoW = mainCam.gameObject.AddComponent<FoW_PostProcessFeature>();
                camFoW.worldCenter = Vector3.zero;
                camFoW.worldSize = new Vector2(300f, 300f);
                camFoW.textureSize = 2048;
                camFoW.mapBoundsMin = new Vector2(-150f, -150f);
                camFoW.mapBoundsSize = new Vector2(300f, 300f);
                camFoW.fovMaskRT = fovMaskRT;
                camFoW.fowShader = fowShader;
                EditorUtility.SetDirty(mainCam.gameObject);
            }

            // 4. Configure VisionCamera
            GameObject visionCamObj = GameObject.Find("VisionCamera");
            if (visionCamObj != null)
            {
                visionCamObj.transform.position = new Vector3(0f, 100f, 0f);
                visionCamObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                visionCamObj.transform.localScale = Vector3.one;

                Camera vCam = visionCamObj.GetComponent<Camera>();
                if (vCam != null)
                {
                    vCam.orthographic = true;
                    vCam.orthographicSize = 150f;
                    vCam.targetTexture = fovMaskRT;
                    int visionLayer = LayerMask.NameToLayer("VisionMask");
                    if (visionLayer != -1) vCam.cullingMask = 1 << visionLayer;
                }

                FogMemoryManager memMgr = visionCamObj.GetComponent<FogMemoryManager>();
                if (memMgr == null) memMgr = visionCamObj.AddComponent<FogMemoryManager>();
                memMgr.fovMaskRT = fovMaskRT;
                memMgr.textureResolution = 2048;
                memMgr.memoryFloor = 0.5f;

                EditorUtility.SetDirty(visionCamObj);
            }

            // 5. Configure Prefab_TacticalWall Shadows & Layer
            string wallPrefabPath = "Assets/_Project/Prefabs/Prefab_TacticalWall.prefab";
            GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wallPrefabPath);
            if (wallPrefab != null)
            {
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(wallPrefabPath);
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer == -1) obstacleLayer = 7;

                prefabContents.layer = obstacleLayer;
                prefabContents.tag = "Obstacle";

                Renderer rend = prefabContents.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
                    rend.receiveShadows = true;
                }

                BoxCollider boxCol = prefabContents.GetComponent<BoxCollider>();
                if (boxCol == null) prefabContents.AddComponent<BoxCollider>();
                boxCol.enabled = true;

                PrefabUtility.SaveAsPrefabAsset(prefabContents, wallPrefabPath);
                PrefabUtility.UnloadPrefabContents(prefabContents);
                Debug.Log("[SetupTaskScenePurgeAndTracking] Configured Prefab_TacticalWall shadows (TwoSided), BoxCollider, and Obstacle layer.");
            }

            // 6. Configure Player.prefab DynamicFOV obstacleMask
            string playerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab != null)
            {
                GameObject playerContents = PrefabUtility.LoadPrefabContents(playerPrefabPath);
                DynamicFOV dynFov = playerContents.GetComponentInChildren<DynamicFOV>(true);
                if (dynFov != null)
                {
                    int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                    dynFov.obstacleMask = 1 << obstacleLayer;
                    dynFov.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    dynFov.transform.localRotation = Quaternion.identity;
                    dynFov.transform.localScale = Vector3.one;
                }
                PrefabUtility.SaveAsPrefabAsset(playerContents, playerPrefabPath);
                PrefabUtility.UnloadPrefabContents(playerContents);
                Debug.Log("[SetupTaskScenePurgeAndTracking] Configured Player.prefab DynamicFOV with Obstacle layer mask.");
            }

            // 7. Save Scene & Assets
            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log("[SetupTaskScenePurgeAndTracking] Scene purge, normalized fog transform, and shadow calibration complete.");
            }

            EditorApplication.delayCall -= ExecuteSetup;
        }
    }
}
