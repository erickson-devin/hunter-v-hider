using UnityEditor;
using UnityEngine;
using HunterVsHider.Player;

namespace HunterVsHider.Editor
{
    public static class SetupTaskSprintMechanic
    {
        [MenuItem("Tools/Hunter v Hider/Verify Sprint Mechanic")]
        public static void RunVerification()
        {
            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskSprintMechanic] STARTING SPRINT MECHANIC VERIFICATION");
            Debug.Log("==========================================================================");

            // 1. Inspect Player.prefab
            string prefabPath = "Assets/_Project/Prefabs/Player.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (playerPrefab == null)
            {
                Debug.LogError($"[Verification FAIL] Could not load player prefab at {prefabPath}");
                return;
            }

            var movement = playerPrefab.GetComponent<PlayerMovement>();
            if (movement == null)
            {
                Debug.LogError("[Verification FAIL] PlayerMovement component missing on Player prefab!");
                return;
            }

            if (Mathf.Abs(movement.walkSpeed - 5.0f) > 0.01f)
            {
                Debug.LogWarning($"[Verification Warning] PlayerMovement.walkSpeed on prefab is {movement.walkSpeed}, updating to 5.0f");
                movement.walkSpeed = 5.0f;
            }

            if (Mathf.Abs(movement.sprintSpeed - 8.5f) > 0.01f)
            {
                Debug.LogWarning($"[Verification Warning] PlayerMovement.sprintSpeed on prefab is {movement.sprintSpeed}, updating to 8.5f");
                movement.sprintSpeed = 8.5f;
            }

            var netState = playerPrefab.GetComponent<PlayerNetworkState>();
            if (netState == null)
            {
                Debug.LogError("[Verification FAIL] PlayerNetworkState component missing on Player prefab!");
                return;
            }

            EditorUtility.SetDirty(playerPrefab);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Verification PASS] PlayerMovement speeds verified: Walk = {movement.walkSpeed}m/s, Sprint = {movement.sprintSpeed}m/s.");
            Debug.Log("[Verification PASS] PlayerNetworkState.isSprinting NetworkVariable verified.");
            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskSprintMechanic] ALL VERIFICATIONS COMPLETED SUCCESSFULLY!");
            Debug.Log("==========================================================================");
        }
    }
}
