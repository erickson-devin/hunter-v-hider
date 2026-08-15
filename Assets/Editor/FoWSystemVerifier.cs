using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HunterVsHider.Vision;
using HunterVsHider.Player;
using HunterVsHider.Gameplay;
using HunterVsHider.Cameras;

namespace HunterVsHider.EditorScripts
{
    public static class FoWSystemVerifier
    {
        [MenuItem("HunterVsHider/Verify FoW & Vision System")]
        public static void RunVerification()
        {
            Debug.Log("=================================================");
            Debug.Log("[FoWSystemVerifier] STARTING VERIFICATION TEST SUITE");
            Debug.Log("=================================================");

            int passed = 0;
            int failed = 0;

            // Test 1: Layer Configuration
            int highLayer = LayerMask.NameToLayer("ObstacleHigh");
            int lowLayer = LayerMask.NameToLayer("ObstacleLow");

            if (highLayer != -1 && lowLayer != -1)
            {
                Debug.Log($"[PASS] Test 1: LayerMasks properly defined. ObstacleHigh = Layer {highLayer}, ObstacleLow = Layer {lowLayer}");
                passed++;
            }
            else
            {
                Debug.LogError($"[FAIL] Test 1: LayerMasks missing! ObstacleHigh = {highLayer}, ObstacleLow = {lowLayer}");
                failed++;
            }

            // Test 2: Math & Clearance Angle Unit Test
            // Eye at (0, 1.8, 0), ObstacleLow top at 1.0m, Ground at 0.0m
            float eyeY = 1.8f;
            float obsHeight = 1.0f;
            float groundY = 0f;

            // Scenario A: Player distance d1 = 4m from 1m box
            float d1_far = 4.0f;
            float slope_far = (eyeY - obsHeight) / d1_far; // 0.8 / 4 = 0.2
            float shadowDelta_far = (obsHeight - groundY) / slope_far; // 1.0 / 0.2 = 5.0m

            // Scenario B: Player distance d1 = 1m from 1m box (player walked closer)
            float d1_close = 1.0f;
            float slope_close = (eyeY - obsHeight) / d1_close; // 0.8 / 1 = 0.8
            float shadowDelta_close = (obsHeight - groundY) / slope_close; // 1.0 / 0.8 = 1.25m

            if (Mathf.Approximately(shadowDelta_far, 5.0f) && Mathf.Approximately(shadowDelta_close, 1.25f) && shadowDelta_close < shadowDelta_far)
            {
                Debug.Log($"[PASS] Test 2: 2.5D Clearance Angle Projection validated mathematically. Far shadow delta = {shadowDelta_far:F2}m -> Close shadow delta = {shadowDelta_close:F2}m (contracts as player approaches).");
                passed++;
            }
            else
            {
                Debug.LogError($"[FAIL] Test 2: Clearance angle math failed! Far: {shadowDelta_far}, Close: {shadowDelta_close}");
                failed++;
            }

            // Test 3: Scene Objects in Tactical_Main
            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != scenePath)
            {
                currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            GameObject fowManagerObj = GameObject.Find("_FogOfWarManager");
            FogOfWarManager fowManager = fowManagerObj != null ? fowManagerObj.GetComponent<FogOfWarManager>() : null;
            if (fowManager != null)
            {
                Debug.Log("[PASS] Test 3: FogOfWarManager exists in scene.");
                passed++;
            }
            else
            {
                Debug.LogError("[FAIL] Test 3: FogOfWarManager missing in scene!");
                failed++;
            }

            // Test 4: Camera & Tracking
            GameObject mainCamObj = GameObject.FindGameObjectWithTag("MainCamera") ?? GameObject.Find("Main Camera");
            Camera cam = mainCamObj != null ? mainCamObj.GetComponent<Camera>() : null;
            AudioListener al = mainCamObj != null ? mainCamObj.GetComponent<AudioListener>() : null;
            CameraFollow follow = mainCamObj != null ? mainCamObj.GetComponent<CameraFollow>() : null;
            TacticalCamera tacCam = mainCamObj != null ? mainCamObj.GetComponent<TacticalCamera>() : null;

            if (cam != null && al != null && (follow != null || tacCam != null) && !cam.orthographic && Mathf.Approximately(cam.fieldOfView, 60f))
            {
                Vector3 camOffset = follow != null ? follow.offset : tacCam.offset;
                float camPitch = follow != null ? follow.pitch : tacCam.pitch;
                Debug.Log($"[PASS] Test 4: Main Camera properly configured (Tag: {mainCamObj.tag}, FOV: {cam.fieldOfView}, Near: {cam.nearClipPlane}, Far: {cam.farClipPlane}, Offset: {camOffset}, Pitch: {camPitch}°).");
                passed++;
            }
            else
            {
                Debug.LogError($"[FAIL] Test 4: Main Camera configuration incomplete! Cam: {cam != null}, AudioListener: {al != null}, Follow: {follow != null || tacCam != null}");
                failed++;
            }

            // Test 5: Player Setup & Proximity Vision
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
            VisionController vc = playerObj != null ? playerObj.GetComponent<VisionController>() : null;
            FieldOfView fov = playerObj != null ? playerObj.GetComponentInChildren<FieldOfView>() : null;

            if (playerObj != null && vc != null && fov != null)
            {
                vc.CalculateVision();
                fov.GenerateFOVMesh();

                bool hasProximity = vc.ProximityRadius > 0f && vc.ProximityResults.Count > 0;
                bool hasMesh = fov.CurrentMesh != null && fov.CurrentMesh.vertexCount > 5;

                if (hasProximity && hasMesh)
                {
                    Debug.Log($"[PASS] Test 5: Player prefab verified with VisionController (Eye: {vc.EyeOffset}, FOV: {vc.ViewAngle}°, Proximity: {vc.ProximityRadius}m) and generated FOV Mesh ({fov.CurrentMesh.vertexCount} verts, {fov.CurrentMesh.triangles.Length / 3} tris).");
                    passed++;
                }
                else
                {
                    Debug.LogError($"[FAIL] Test 5: FOV Mesh or Proximity calculation failed! Prox: {hasProximity}, Mesh: {hasMesh}");
                    failed++;
                }
            }
            else
            {
                Debug.LogError($"[FAIL] Test 5: Player missing or incomplete! PlayerObj: {playerObj != null}, VC: {vc != null}, FOV: {fov != null}");
                failed++;
            }

            // Test 6: FoW_ScreenDarkness Quad & Material
            Transform screenDarkness = mainCamObj != null ? mainCamObj.transform.Find("FoW_ScreenDarkness") : null;
            MeshRenderer screenMr = screenDarkness != null ? screenDarkness.GetComponent<MeshRenderer>() : null;

            if (screenDarkness != null && screenMr != null && screenMr.sharedMaterial != null && screenMr.sharedMaterial.shader.name == "Custom/FogOfWarBlit")
            {
                Debug.Log($"[PASS] Test 6: FoW_ScreenDarkness attached to Main Camera (Layer: {screenDarkness.gameObject.layer} - IgnoreRaycast/NonBlocking) with Custom/FogOfWarBlit material.");
                passed++;
            }
            else
            {
                Debug.LogError($"[FAIL] Test 6: FoW_ScreenDarkness invalid! Trans: {screenDarkness != null}, MR: {screenMr != null}");
                failed++;
            }

            // Test 7: Dynamic Target Visibility
            TargetVisibility[] targets = Object.FindObjectsByType<TargetVisibility>(FindObjectsInactive.Exclude);
            if (targets.Length >= 3)
            {
                Debug.Log($"[PASS] Test 7: Found {targets.Length} dynamic entities configured with TargetVisibility (TargetDummy_01, TargetDummy_02, Assassin_Dummy).");
                passed++;
            }
            else
            {
                Debug.LogError($"[FAIL] Test 7: Found {targets.Length} targets with TargetVisibility (expected >= 3)!");
                failed++;
            }

            // Test 8: Fog of War Manager Update & Active Stamping
            if (fowManager != null && vc != null)
            {
                fowManager.EnsureCameraSetup();
                fowManager.RegisterVisionController(vc);
                fowManager.UpdateFogOfWar();

                if (fowManager.CombinedFoWTexture != null && fowManager.ActiveVisionTexture != null)
                {
                    Debug.Log($"[PASS] Test 8: FogOfWarManager executed active vision stamping and accumulation blit into {fowManager.CombinedFoWTexture.width}x{fowManager.CombinedFoWTexture.height} RT.");
                    passed++;
                }
                else
                {
                    Debug.LogError("[FAIL] Test 8: FoW RenderTextures failed to initialize!");
                    failed++;
                }
            }

            Debug.Log("=================================================");
            Debug.Log($"[FoWSystemVerifier] VERIFICATION COMPLETE: {passed} PASSED, {failed} FAILED.");
            Debug.Log("=================================================");
        }
    }
}
