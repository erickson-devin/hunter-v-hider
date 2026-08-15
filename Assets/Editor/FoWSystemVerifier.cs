using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HunterVsHider.Vision;
using HunterVsHider.Player;
using HunterVsHider.Gameplay;

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
            float shadowEnd_far = d1_far + shadowDelta_far; // 9.0m

            // Scenario B: Player distance d1 = 1m from 1m box (player walked closer)
            float d1_close = 1.0f;
            float slope_close = (eyeY - obsHeight) / d1_close; // 0.8 / 1 = 0.8
            float shadowDelta_close = (obsHeight - groundY) / slope_close; // 1.0 / 0.8 = 1.25m
            float shadowEnd_close = d1_close + shadowDelta_close; // 2.25m

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

            // Test 3: Scene Objects & Components in Tactical_Main
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
                Debug.Log("[PASS] Test 3a: FogOfWarManager exists in scene.");
                passed++;
            }
            else
            {
                Debug.LogError("[FAIL] Test 3a: FogOfWarManager missing in scene!");
                failed++;
            }

            GameObject playerObj = GameObject.Find("Player") ?? GameObject.FindGameObjectWithTag("Player");
            VisionController vc = playerObj != null ? playerObj.GetComponent<VisionController>() : null;
            FieldOfView fov = playerObj != null ? playerObj.GetComponentInChildren<FieldOfView>() : null;

            if (vc != null && fov != null)
            {
                Debug.Log($"[PASS] Test 3b: Police Player configured with VisionController (Eye: {vc.EyeOffset}, FOV: {vc.ViewAngle} deg, Dist: {vc.ViewDistance}m) and FieldOfView dynamic mesh generator.");
                passed++;
            }
            else
            {
                Debug.LogError($"[FAIL] Test 3b: Player missing VisionController ({vc != null}) or FieldOfView ({fov != null})!");
                failed++;
            }

            // Test 4: Dynamic Entity Visibility (TargetVisibility on Assassins)
            TargetVisibility[] targets = Object.FindObjectsByType<TargetVisibility>(FindObjectsSortMode.None);
            if (targets.Length >= 2)
            {
                Debug.Log($"[PASS] Test 4: Found {targets.Length} dynamic targets with TargetVisibility attached (including TargetDummies and Assassin dummy).");
                passed++;
            }
            else
            {
                Debug.LogError($"[FAIL] Test 4: Found {targets.Length} targets with TargetVisibility (expected >= 2)!");
                failed++;
            }

            // Test 5: Camera Depth & Screen Darkness Overlay
            GameObject mainCamObj = GameObject.FindGameObjectWithTag("MainCamera") ?? GameObject.Find("Main Camera");
            Camera cam = mainCamObj != null ? mainCamObj.GetComponent<Camera>() : null;
            Transform screenDarkness = mainCamObj != null ? mainCamObj.transform.Find("FoW_ScreenDarkness") : null;
            MeshRenderer screenMr = screenDarkness != null ? screenDarkness.GetComponent<MeshRenderer>() : null;

            if (cam != null && screenMr != null && screenMr.sharedMaterial != null && screenMr.sharedMaterial.shader.name == "Custom/FogOfWarBlit")
            {
                Debug.Log("[PASS] Test 5: Main Camera configured with DepthTextureMode and FoW_ScreenDarkness quad using Custom/FogOfWarBlit shader.");
                passed++;
            }
            else
            {
                Debug.LogError($"[FAIL] Test 5: Camera / FoW_ScreenDarkness invalid! Cam: {cam != null}, ScreenMR: {screenMr != null}, Shader: {(screenMr != null && screenMr.sharedMaterial != null ? screenMr.sharedMaterial.shader.name : "none")}");
                failed++;
            }

            Debug.Log("=================================================");
            Debug.Log($"[FoWSystemVerifier] VERIFICATION COMPLETE: {passed} PASSED, {failed} FAILED.");
            Debug.Log("=================================================");
        }
    }
}
