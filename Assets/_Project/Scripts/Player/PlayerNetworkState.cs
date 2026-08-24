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

        [Header("Weapon State")]
        [Tooltip("Networked selected weapon profile ID (0 = Tactical Rifle, 1 = Optic-Ready 9mm, 2 = Sub-Compact .45). Read: Everyone, Write: Owner.")]
        public NetworkVariable<int> selectedWeaponID = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        [Header("Movement State")]
        [Tooltip("Networked sprint state for animation and remote client representation. Read: Everyone, Write: Owner.")]
        public NetworkVariable<bool> isSprinting = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public PlayerRole Role => currentRole.Value;
        public int SelectedWeaponID => selectedWeaponID.Value;
        public bool IsSprinting => isSprinting.Value;

        /// <summary>
        /// Updates the networked sprinting state. Called by the owning client.
        /// </summary>
        public void SetSprinting(bool sprinting)
        {
            if (IsOwner && isSprinting.Value != sprinting)
            {
                isSprinting.Value = sprinting;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Print synchronization details
            Debug.Log($"[PlayerNetworkState] OnNetworkSpawn -> ClientId: {OwnerClientId}, IsLocalPlayer: {IsLocalPlayer}, IsServer: {IsServer}, Role: {currentRole.Value}, WeaponID: {selectedWeaponID.Value}");

            // Configure local camera follow
            if (IsLocalPlayer)
            {
                var cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    var follow = cam.GetComponent<HunterVsHider.Cameras.CameraFollow>();
                    if (follow != null)
                    {
                        var matchMgr = HunterVsHider.Managers.MatchManager.Instance ?? HunterVsHider.Managers.MatchManager.Singleton;
                        bool isPrep = matchMgr != null && matchMgr.CurrentState == HunterVsHider.Managers.MatchState.PrepPhase;
                        if (isPrep && Role == PlayerRole.Assassin)
                        {
                            follow.SetTarget(null);
                            follow.ActivateAssassinSkyView();
                        }
                        else
                        {
                            follow.SetTarget(transform);
                        }
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
            }
            else
            {
                // Apply role-specific visibility on spawn for the local player
                ApplyRoleVision();
            }

            // Subscribe to state changes across the network (for both Owner and Observers)
            currentRole.OnValueChanged += OnRoleChanged;
            selectedWeaponID.OnValueChanged += OnWeaponChanged;

            if (IsLocalPlayer && HunterVsHider.Managers.MatchManager.Instance != null)
            {
                HunterVsHider.Managers.MatchManager.Instance.OnMatchStateChanged += HandleMatchStateChanged;
            }

            // Initialize weapon visual
            UpdateWeaponVisual(selectedWeaponID.Value);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            currentRole.OnValueChanged -= OnRoleChanged;
            selectedWeaponID.OnValueChanged -= OnWeaponChanged;

            if (IsLocalPlayer && HunterVsHider.Managers.MatchManager.Instance != null)
            {
                HunterVsHider.Managers.MatchManager.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
            }
        }

        private void HandleMatchStateChanged(HunterVsHider.Managers.MatchState previousState, HunterVsHider.Managers.MatchState newState)
        {
            if (!IsLocalPlayer) return;
            UpdatePrepPhaseCameraAndMovement(newState);
        }

        public void UpdatePrepPhaseCameraAndMovement(HunterVsHider.Managers.MatchState state)
        {
            if (!IsLocalPlayer) return;

            var cam = UnityEngine.Camera.main;
            var camFollow = cam != null ? cam.GetComponent<HunterVsHider.Cameras.CameraFollow>() : null;
            if (camFollow == null) camFollow = HunterVsHider.Cameras.CameraFollow.Instance;
            var playerMovement = GetComponent<PlayerMovement>();
            var playerController = GetComponent<PlayerController>();
            var weaponManager = GetComponent<PlayerWeaponManager>();
            var dynamicFov = GetComponentInChildren<HunterVsHider.Vision.DynamicFOV>(true);

            if (state == HunterVsHider.Managers.MatchState.WaitingForPlayers || state == HunterVsHider.Managers.MatchState.RoleAssignment)
            {
                if (dynamicFov != null)
                {
                    dynamicFov.isVisionMaskSuppressed = true;
                }
                if (camFollow != null)
                {
                    camFollow.ResetToTacticalView(transform);
                }
                if (playerMovement != null) playerMovement.SetMovementEnabled(true);
                if (playerController != null) playerController.enabled = true;
                if (weaponManager != null) weaponManager.enabled = true;
                ForceEnableAllPlayerRenderers();
            }
            else if (state == HunterVsHider.Managers.MatchState.PrepPhase)
            {
                if (dynamicFov != null)
                {
                    dynamicFov.isVisionMaskSuppressed = true;
                }

                if (Role == PlayerRole.Assassin)
                {
                    int mapSize = HunterVsHider.Managers.MatchManager.Instance != null ? HunterVsHider.Managers.MatchManager.Instance.SelectedMapSize : 50;
                    if (camFollow != null)
                    {
                        camFollow.SetTarget(null);
                        camFollow.ActivateAssassinGodView(mapSize);
                    }
                    if (playerMovement != null) playerMovement.SetMovementEnabled(false);
                    if (playerController != null) playerController.enabled = false;
                    if (weaponManager != null) weaponManager.enabled = false;
                    Debug.Log($"[PlayerNetworkState] Assassin entering PrepPhase -> Activated God-View Camera ({mapSize}x{mapSize}). Character targeting detached.");
                }
                else
                {
                    if (camFollow != null)
                    {
                        camFollow.ResetToTacticalView(transform);
                    }
                    if (playerMovement != null) playerMovement.SetMovementEnabled(true);
                    if (playerController != null) playerController.enabled = true;
                    if (weaponManager != null)
                    {
                        weaponManager.enabled = true;
                        weaponManager.ResetAllWeaponStates();
                    }
                    ForceEnableAllPlayerRenderers();
                    Debug.Log($"[PlayerNetworkState] Police in PrepPhase -> Tactical follow camera attached to Police staging area.");
                }
            }
            else if (state == HunterVsHider.Managers.MatchState.CombatPhase)
            {
                if (dynamicFov != null)
                {
                    dynamicFov.isVisionMaskSuppressed = false;

                    if (Role == PlayerRole.Police)
                    {
                        HunterVsHider.Vision.FogMemoryManager.Instance?.ResetFog();
                        dynamicFov.ClearExploredMemoryGrid();
                        if (weaponManager != null && weaponManager.ActiveWeapon != null && weaponManager.ActiveWeapon.weaponData != null)
                        {
                            var wData = weaponManager.ActiveWeapon.weaponData;
                            dynamicFov.UpdateWeaponVisionProfile(wData.viewAngle, wData.viewDistance, 0.3f);
                        }
                        else
                        {
                            dynamicFov.UpdateWeaponVisionProfile(45f, 24f, 0.3f);
                        }
                    }
                    else if (Role == PlayerRole.Assassin)
                    {
                        HunterVsHider.Vision.FogMemoryManager.Instance?.SetAllToMemory(0.5f);
                        dynamicFov.UpdateWeaponVisionProfile(360f, 12f, 0.3f);
                    }
                }
                else
                {
                    if (Role == PlayerRole.Police)
                    {
                        HunterVsHider.Vision.FogMemoryManager.Instance?.ResetFog();
                    }
                    else if (Role == PlayerRole.Assassin)
                    {
                        HunterVsHider.Vision.FogMemoryManager.Instance?.SetAllToMemory(0.5f);
                    }
                }

                if (camFollow != null)
                {
                    camFollow.ResetToTacticalView(transform);
                }
                if (playerMovement != null) playerMovement.SetMovementEnabled(true);
                if (playerController != null) playerController.enabled = true;
                if (weaponManager != null) weaponManager.enabled = true;
                Debug.Log($"[PlayerNetworkState] Local player ({Role}) entered CombatPhase -> Standard tactical follow attached to character.");
            }
            else
            {
                if (camFollow != null)
                {
                    camFollow.ResetToTacticalView(transform);
                }
                if (playerMovement != null)
                {
                    playerMovement.SetMovementEnabled(true);
                }
                if (playerController != null)
                {
                    playerController.enabled = true;
                }
                if (weaponManager != null)
                {
                    weaponManager.enabled = true;
                }
            }
        }

        /// <summary>
        /// Explicitly enables all mesh renderers and UI across all player instances in non-combat phases
        /// ensuring 100% mutual visibility across the armory and staging zones.
        /// </summary>
        public static void ForceEnableAllPlayerRenderers()
        {
            int visionMaskLayer = LayerMask.NameToLayer("VisionMask");
            PlayerNetworkState[] allPlayers = Object.FindObjectsByType<PlayerNetworkState>(FindObjectsInactive.Include);
            for (int i = 0; i < allPlayers.Length; i++)
            {
                if (allPlayers[i] == null) continue;
                Renderer[] rends = allPlayers[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                {
                    if (rends[r].gameObject.layer != visionMaskLayer)
                    {
                        rends[r].enabled = true;
                    }
                }
                Canvas[] canvases = allPlayers[i].GetComponentsInChildren<Canvas>(true);
                for (int c = 0; c < canvases.Length; c++)
                {
                    canvases[c].enabled = true;
                }
            }
        }

        private void OnRoleChanged(PlayerRole previousRole, PlayerRole newRole)
        {
            Debug.Log($"[PlayerNetworkState] OwnerClientId {OwnerClientId} Role changed from {previousRole} to {newRole} (IsLocalPlayer: {IsLocalPlayer}, IsServer: {IsServer})");
            
            var weaponMgr = GetComponent<PlayerWeaponManager>();
            if (weaponMgr != null)
            {
                weaponMgr.SetupRoleLoadout(newRole);
                weaponMgr.ResetAllWeaponStates();
            }

            // Re-apply vision settings when role updates
            if (IsOwner)
            {
                ApplyRoleVision();

                if (HunterVsHider.Managers.MatchManager.Instance != null)
                {
                    if (HunterVsHider.Managers.MatchManager.Instance.CurrentState == HunterVsHider.Managers.MatchState.PrepPhase)
                    {
                        HunterVsHider.Managers.MatchManager.Instance.ExecuteLocalClientPrepPhaseResponse();
                    }
                    else
                    {
                        UpdatePrepPhaseCameraAndMovement(HunterVsHider.Managers.MatchManager.Instance.CurrentState);
                    }
                }
            }
        }

        private void OnWeaponChanged(int previousWeapon, int newWeapon)
        {
            Debug.Log($"[PlayerNetworkState] OwnerClientId {OwnerClientId} Weapon changed from {previousWeapon} to {newWeapon}");
            UpdateWeaponVisual(newWeapon);
        }

        /// <summary>
        /// Owner-authoritative method to select a weapon profile.
        /// </summary>
        /// <param name="weaponID">0: Tactical Rifle, 1: Optic-Ready 9mm, 2: Sub-Compact .45</param>
        public void CmdSelectWeapon(int weaponID)
        {
            if (!IsOwner)
            {
                Debug.LogWarning($"[PlayerNetworkState] Non-owner ClientId {NetworkManager.Singleton?.LocalClientId} attempted to select weapon on ClientId {OwnerClientId}!");
                return;
            }

            selectedWeaponID.Value = weaponID;
            Debug.Log($"[PlayerNetworkState] ClientId {OwnerClientId} CmdSelectWeapon -> ID: {weaponID}");
        }

        /// <summary>
        /// Updates the 3D physical weapon model in the player's hands across all clients.
        /// ID 0 = Tactical Rifle (Blue Long Box)
        /// ID 1 = Optic-Ready 9mm (Green Short Box)
        /// ID 2 = Sub-Compact .45 (Red Short Box)
        /// </summary>
        public void UpdateWeaponVisual(int weaponID)
        {
            Transform weaponHolder = transform.Find("WeaponHolder");
            if (weaponHolder == null) return;

            // Find or create the visual block
            Transform visualBlock = weaponHolder.Find("Weapon_VisualBlock");
            if (visualBlock == null)
            {
                GameObject blockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blockObj.name = "Weapon_VisualBlock";
                blockObj.transform.SetParent(weaponHolder, false);
                
                // Remove physics collider so it doesn't interfere with player movement
                var col = blockObj.GetComponent<Collider>();
                if (col != null) Destroy(col);

                visualBlock = blockObj.transform;
            }

            // Disable individual MeshRenderers on weapon holder children so visualBlock renders cleanly without disabling weapon GameObjects
            Transform p = weaponHolder.Find("Gun_Pistol");
            if (p != null)
            {
                var mr = p.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
                var pm = p.Find("Pistol_Mesh");
                if (pm != null && pm.GetComponent<MeshRenderer>() != null) pm.GetComponent<MeshRenderer>().enabled = false;
            }
            Transform r = weaponHolder.Find("Gun_Rifle");
            if (r != null)
            {
                var mr = r.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }
            Transform s = weaponHolder.Find("Gun_Shotgun");
            if (s != null)
            {
                var mr = s.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }

            MeshRenderer renderer = visualBlock.GetComponent<MeshRenderer>();
            Material mat = null;

            if (Role == PlayerRole.Assassin)
            {
                if (weaponID == 0)
                {
                    // Slash Knife (Silver Blade)
                    visualBlock.localPosition = new Vector3(0.3f, 0.0f, 0.35f);
                    visualBlock.localScale = new Vector3(0.08f, 0.15f, 0.5f);
                    mat = GetOrCreateColorMaterial("Mat_Weapon_Silver", new Color(0.85f, 0.85f, 0.9f));
                }
                else
                {
                    // Throwing Knives (Dark Knife)
                    visualBlock.localPosition = new Vector3(0.3f, 0.0f, 0.28f);
                    visualBlock.localScale = new Vector3(0.06f, 0.1f, 0.35f);
                    mat = GetOrCreateColorMaterial("Mat_Weapon_Dark", new Color(0.2f, 0.2f, 0.25f));
                }
            }
            else
            {
                switch (weaponID)
                {
                    case 0: // Tactical Rifle (Blue Long Box)
                        visualBlock.localPosition = new Vector3(0.3f, 0.0f, 0.6f);
                        visualBlock.localScale = new Vector3(0.18f, 0.18f, 1.2f);
                        mat = GetOrCreateColorMaterial("Mat_Weapon_Blue", new Color(0.15f, 0.45f, 1.0f));
                        break;

                    case 1: // Combat Shotgun (Heavy Green Box)
                        visualBlock.localPosition = new Vector3(0.3f, 0.0f, 0.5f);
                        visualBlock.localScale = new Vector3(0.22f, 0.20f, 0.9f);
                        mat = GetOrCreateColorMaterial("Mat_Weapon_Green", new Color(0.15f, 0.85f, 0.25f));
                        break;

                    case 2: // Pistol (Red Short Box)
                        visualBlock.localPosition = new Vector3(0.3f, 0.0f, 0.28f);
                        visualBlock.localScale = new Vector3(0.13f, 0.13f, 0.32f);
                        mat = GetOrCreateColorMaterial("Mat_Weapon_Red", new Color(0.95f, 0.2f, 0.2f));
                        break;

                    default:
                        visualBlock.localPosition = new Vector3(0.3f, 0.0f, 0.5f);
                        visualBlock.localScale = new Vector3(0.15f, 0.15f, 0.5f);
                        mat = GetOrCreateColorMaterial("Mat_Weapon_Blue", new Color(0.15f, 0.45f, 1.0f));
                        break;
                }
            }

            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        private Material GetOrCreateColorMaterial(string matName, Color color)
        {
            string path = $"Assets/_Project/Materials/{matName}.mat";
            Material mat = null;
            #if UNITY_EDITOR
            mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            #endif

            if (mat == null)
            {
                Shader standardShader = Shader.Find("Universal Render Pipeline/Lit");
                if (standardShader == null) standardShader = Shader.Find("Standard");
                mat = new Material(standardShader);
                mat.color = color;
            }
            return mat;
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
            
            bool isNonCombat = HunterVsHider.Managers.MatchManager.Instance == null || 
                               HunterVsHider.Managers.MatchManager.Instance.CurrentState == HunterVsHider.Managers.MatchState.WaitingForPlayers ||
                               HunterVsHider.Managers.MatchManager.Instance.CurrentState == HunterVsHider.Managers.MatchState.PrepPhase ||
                               HunterVsHider.Managers.MatchManager.Instance.CurrentState == HunterVsHider.Managers.MatchState.RoleAssignment;

            if (fowPostProcess != null)
            {
                fowPostProcess.enabled = !isNonCombat;
            }

            if (dynamicFov != null)
            {
                dynamicFov.isVisionMaskSuppressed = isNonCombat;
            }

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

        /// <summary>
        /// Server-authoritative teleportation method. Dispatches ClientRpc to client owner for immediate position synchronization.
        /// </summary>
        /// <param name="targetPosition">World destination position.</param>
        /// <param name="targetRotation">World destination rotation.</param>
        public void ServerTeleport(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[PlayerNetworkState] Non-server client {NetworkManager.Singleton?.LocalClientId} attempted to teleport ClientId {OwnerClientId}!");
                return;
            }

            // If server owns this player (Host), apply immediately
            if (IsOwner)
            {
                ApplyTeleport(targetPosition, targetRotation);
            }

            // Dispatch RPC to owner client (and remote observers)
            TeleportClientRpc(targetPosition, targetRotation);
            Debug.Log($"[PlayerNetworkState] Server initiated teleport for ClientId {OwnerClientId} to {targetPosition}");
        }

        [ClientRpc]
        private void TeleportClientRpc(Vector3 targetPosition, Quaternion targetRotation)
        {
            // Owner client applies teleport to its authoritative transform and physics
            if (IsOwner)
            {
                ApplyTeleport(targetPosition, targetRotation);
            }
        }

        public void ApplyTeleport(Vector3 targetPosition, Quaternion targetRotation)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = targetPosition;
                rb.rotation = targetRotation;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (netTransform != null)
            {
                netTransform.Teleport(targetPosition, targetRotation, transform.localScale);
            }

            Physics.SyncTransforms();

            Debug.Log($"[PlayerNetworkState] ClientId {OwnerClientId} successfully teleported to {targetPosition}");
        }
    }
}
