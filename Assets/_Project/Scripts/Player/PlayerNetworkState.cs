using Unity.Netcode;
using UnityEngine;

namespace HunterVsHider.Player
{
    public enum PlayerRole
    {
        Unassigned,
        Police,
        Assassin
    }

    [RequireComponent(typeof(NetworkObject))]
    public class PlayerNetworkState : NetworkBehaviour
    {
        [Header("Role State")]
        [Tooltip("Networked player role. Read permission: Everyone, Write permission: Server.")]
        public NetworkVariable<PlayerRole> currentRole = new NetworkVariable<PlayerRole>(
            PlayerRole.Unassigned,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public PlayerRole Role => currentRole.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Print synchronization details
            Debug.Log($"[PlayerNetworkState] OnNetworkSpawn -> ClientId: {OwnerClientId}, IsLocalPlayer: {IsLocalPlayer}, IsServer: {IsServer}, Role: {currentRole.Value}");

            // Configure local camera follow
            if (IsLocalPlayer)
            {
                var cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    var follow = cam.GetComponent<HunterVsHider.Cameras.CameraFollow>();
                    if (follow != null)
                    {
                        follow.target = transform;
                    }
                }
            }

            // Network vision isolation: strictly disable all vision masks and visual rings for remote player clones
            if (!IsOwner)
            {
                Transform dynamicFov = transform.Find("FOV_DynamicMask");
                if (dynamicFov != null)
                {
                    var r = dynamicFov.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                    var df = dynamicFov.GetComponent<HunterVsHider.Vision.DynamicFOV>();
                    if (df != null) df.enabled = false;
                    dynamicFov.gameObject.SetActive(false);
                }

                Transform fovVisuals = transform.Find("FOV_Visuals");
                if (fovVisuals != null)
                {
                    fovVisuals.gameObject.SetActive(false);
                }
                return;
            }

            // Subscribe to role changes across the network
            currentRole.OnValueChanged += OnRoleChanged;

            // Apply role-specific visibility on spawn for the local player
            ApplyRoleVision();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            currentRole.OnValueChanged -= OnRoleChanged;
        }

        private void OnRoleChanged(PlayerRole previousRole, PlayerRole newRole)
        {
            Debug.Log($"[PlayerNetworkState] OwnerClientId {OwnerClientId} Role changed from {previousRole} to {newRole} (IsLocalPlayer: {IsLocalPlayer}, IsServer: {IsServer})");
            
            // Re-apply vision settings when role updates
            ApplyRoleVision();
        }

        /// <summary>
        /// Configures Fog of War visibility, Dynamic FOV occlusion, and visuals locally based on player role.
        /// Police: 90 deg 15m Dynamic FOV + 2.5m Proximity Circle (Unified). Starts with pure black unexplored fog.
        /// Assassin: 360 deg 12m Dynamic FOV. Starts with entire map in gray memory (no black fog, no hard UI outline).
        /// Both roles use the Screen-Space Depth Reconstruction Fog of War.
        /// </summary>
        public void ApplyRoleVision()
        {
            if (!IsOwner) return;

            var dynamicFov = GetComponentInChildren<HunterVsHider.Vision.DynamicFOV>(true);

            Transform fovVisuals = transform.Find("FOV_Visuals");
            Transform policeFlashlight = fovVisuals != null ? fovVisuals.Find("Police_Flashlight") : null;

            GameObject visionCam = GameObject.Find("VisionCamera");
            if (visionCam != null) visionCam.SetActive(true);

            var mainCam = UnityEngine.Camera.main;
            var fowPostProcess = mainCam != null ? mainCam.GetComponent<HunterVsHider.Vision.FoW_PostProcessFeature>() : null;
            if (fowPostProcess != null) fowPostProcess.enabled = true; // Universal Fog of War

            switch (currentRole.Value)
            {
                case PlayerRole.Police:
                    if (dynamicFov != null)
                    {
                        dynamicFov.gameObject.SetActive(true);
                        dynamicFov.enabled = true;
                        dynamicFov.SetRoleFOV(PlayerRole.Police);
                    }
                    if (policeFlashlight != null) policeFlashlight.gameObject.SetActive(true);

                    // Police starts with unexplored black fog
                    HunterVsHider.Vision.FogMemoryManager.Instance?.ResetFog();
                    Debug.Log("[PlayerNetworkState] Applied Police Vision: 90 deg 15m FOV + 2.5m Unified Proximity (Black Fog start).");
                    break;

                case PlayerRole.Assassin:
                    if (dynamicFov != null)
                    {
                        dynamicFov.gameObject.SetActive(true);
                        dynamicFov.enabled = true;
                        dynamicFov.SetRoleFOV(PlayerRole.Assassin);
                    }
                    if (policeFlashlight != null) policeFlashlight.gameObject.SetActive(false);

                    // Assassin starts with entire map in gray memory (no black fog)
                    HunterVsHider.Vision.FogMemoryManager.Instance?.SetAllToMemory(0.5f);
                    Debug.Log("[PlayerNetworkState] Applied Assassin Vision: 360 deg 12m Dynamic FOV (Gray Memory start, no hard visual ring).");
                    break;

                case PlayerRole.Unassigned:
                default:
                    if (dynamicFov != null)
                    {
                        dynamicFov.gameObject.SetActive(true);
                        dynamicFov.enabled = true;
                        dynamicFov.SetRoleFOV(PlayerRole.Police);
                    }
                    if (policeFlashlight != null) policeFlashlight.gameObject.SetActive(true);
                    break;
            }
        }

        /// <summary>
        /// Server-authoritative method to forcefully assign a role to this player.
        /// </summary>
        /// <param name="newRole">Role to assign (e.g. Police or Assassin).</param>
        public void ServerAssignRole(PlayerRole newRole)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[PlayerNetworkState] Non-server client {NetworkManager.Singleton?.LocalClientId} attempted to assign role to ClientId {OwnerClientId}!");
                return;
            }

            currentRole.Value = newRole;
            Debug.Log($"[PlayerNetworkState] Server assigned role '{newRole}' to ClientId {OwnerClientId}");
        }
    }
}
