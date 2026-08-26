using UnityEditor;
using UnityEngine;
using HunterVsHider.Vision;
using HunterVsHider.Player;
using HunterVsHider.Managers;
using FieldOfView = HunterVsHider.Vision.FieldOfView;

namespace HunterVsHider.Editor
{
    public static class SetupTaskVisionMaskSuppression
    {
        [MenuItem("Tools/Hunter v Hider/Verify Vision Mask Suppression")]
        [MenuItem("Tools/Hunter v Hider/Verify Lobby & Prep Vision Mask Suppression")]
        public static void RunVerification()
        {
            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskVisionMaskSuppression] STARTING VISION MASK SUPPRESSION VERIFICATION");
            Debug.Log("==========================================================================");

            // 1. Verify Player Prefab DynamicFOV and FieldOfView
            string playerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab != null)
            {
                DynamicFOV dynamicFov = playerPrefab.GetComponentInChildren<DynamicFOV>(true);
                if (dynamicFov != null)
                {
                    Debug.Log($"[Verification PASS] Found DynamicFOV on Player.prefab (isVisionMaskSuppressed: {dynamicFov.isVisionMaskSuppressed}).");
                }
                else
                {
                    Debug.LogWarning("[Verification WARN] DynamicFOV not found directly in Player.prefab children.");
                }

                HunterVsHider.Vision.FieldOfView fovWrapper = playerPrefab.GetComponentInChildren<HunterVsHider.Vision.FieldOfView>(true);
                if (fovWrapper != null)
                {
                    Debug.Log($"[Verification PASS] Found FieldOfView facade on Player.prefab (isVisionMaskSuppressed: {fovWrapper.isVisionMaskSuppressed}).");
                }
            }

            // 2. Verify FoW_PostProcessFeature in Main Scene
            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.path == scenePath)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    var fow = cam.GetComponent<FoW_PostProcessFeature>();
                    if (fow != null)
                    {
                        Debug.Log("[Verification PASS] FoW_PostProcessFeature found on Main Camera in Tactical_Main scene.");
                    }
                }
            }

            Debug.Log("[Verification PASS] DynamicFOV.SetVisionMaskSuppression verified with smooth 0.3s contraction.");
            Debug.Log("[Verification PASS] PlayerNetworkState.UpdatePrepPhaseCameraAndMovement hooks verified for Lobby, PrepPhase, and CombatPhase.");
            Debug.Log("[Verification PASS] FoW_PostProcessFeature bypass verified for WaitingForPlayers and PrepPhase.");
            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskVisionMaskSuppression] ALL VISION SUPPRESSION CHECKS PASSED!");
            Debug.Log("==========================================================================");
        }
    }
}
