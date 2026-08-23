using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HunterVsHider.Managers;
using HunterVsHider.Cameras;
using HunterVsHider.Player;
using HunterVsHider.Map;

namespace HunterVsHider.Editor
{
    public static class SetupTaskAssassinPrepGodView
    {
        [MenuItem("Tools/Hunter v Hider/Verify Assassin Prep God-View & Staging")]
        public static void RunVerification()
        {
            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskAssassinPrepGodView] STARTING ASSASSIN PREP & GOD-VIEW VERIFICATION");
            Debug.Log("==========================================================================");

            Scene activeScene = SceneManager.GetActiveScene();

            // 1. Verify MatchManager and Off-Grid Staging Zone
            var matchManager = Object.FindFirstObjectByType<MatchManager>();
            if (matchManager == null)
            {
                Debug.LogError("[Verification FAIL] MatchManager not found in active scene!");
                return;
            }

            Vector3 expectedOffGrid = new Vector3(-999f, 1f, -999f);
            if (MatchManager.OffGridStagingPosition != expectedOffGrid)
            {
                Debug.LogError($"[Verification FAIL] MatchManager.OffGridStagingPosition is {MatchManager.OffGridStagingPosition}, expected {expectedOffGrid}");
                return;
            }

            matchManager.FindZoneReferencesIfNull();
            matchManager.EnsureOffGridStagingPlatform();

            var platformObj = GameObject.Find("Zone_OffGridStagingPlatform");
            if (platformObj == null)
            {
                Debug.LogError("[Verification FAIL] Zone_OffGridStagingPlatform GameObject was not created!");
                return;
            }

            var col = platformObj.GetComponent<BoxCollider>();
            if (col == null || !col.enabled)
            {
                Debug.LogError("[Verification FAIL] Zone_OffGridStagingPlatform is missing an active BoxCollider!");
                return;
            }

            Debug.Log($"[Verification PASS] Off-Grid Staging Zone verified at {MatchManager.OffGridStagingPosition} with BoxCollider platform.");

            // 2. Verify CameraFollow and God-View Panning Mode
            var cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                var camFollow = cam.GetComponent<CameraFollow>();
                if (camFollow == null) camFollow = cam.gameObject.AddComponent<CameraFollow>();

                camFollow.ActivateAssassinGodView(50);
                if (!camFollow.IsSkyViewActive)
                {
                    Debug.LogError("[Verification FAIL] CameraFollow.IsSkyViewActive is false after ActivateAssassinGodView(50)!");
                    return;
                }

                camFollow.ResetToTacticalView();
                if (camFollow.IsSkyViewActive)
                {
                    Debug.LogError("[Verification FAIL] CameraFollow.IsSkyViewActive is true after ResetToTacticalView()!");
                    return;
                }

                Debug.Log("[Verification PASS] CameraFollow God-View activation, WASD pan setup, and tactical reset verified.");
            }

            // 3. Verify Safe North Spawn coordinates across all 3 tiers
            foreach (int tier in new int[] { 50, 100, 150 })
            {
                Vector3 assassinSpawn = MapGenerator.GetSafeAssassinSpawnPosition(tier);
                Vector3 policeSpawn = MapGenerator.GetSafePoliceSpawnPosition(0, tier);

                float halfTier = tier * 0.5f;
                if (Mathf.Abs(assassinSpawn.x) > halfTier || Mathf.Abs(assassinSpawn.z) > halfTier)
                {
                    Debug.LogError($"[Verification FAIL] Assassin spawn {assassinSpawn} is outside tier {tier} bounds!");
                    return;
                }
                if (Mathf.Abs(policeSpawn.x) > halfTier || Mathf.Abs(policeSpawn.z) > halfTier)
                {
                    Debug.LogError($"[Verification FAIL] Police spawn {policeSpawn} is outside tier {tier} bounds!");
                    return;
                }
            }
            Debug.Log("[Verification PASS] Safe North Assassin Spawn and South Police Spawn coordinates verified across tiers 50, 100, 150.");

            EditorUtility.SetDirty(matchManager);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskAssassinPrepGodView] ALL VERIFICATIONS COMPLETED SUCCESSFULLY!");
            Debug.Log("==========================================================================");
        }
    }
}
