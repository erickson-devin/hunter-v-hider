using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using HunterVsHider.Player;
using HunterVsHider.Map;
using HunterVsHider.UI;

namespace HunterVsHider.Managers
{
    public enum MatchState
    {
        WaitingForPlayers,
        RoleAssignment,
        PrepPhase,
        CombatPhase,
        MatchEnd
    }

    [RequireComponent(typeof(NetworkObject))]
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }
        public static MatchManager Singleton => Instance;

        [Header("Zone References")]
        [Tooltip("Root container / spawn location for pre-match Lobby (Position: X=1000, Y=0, Z=0)")]
        public Transform zoneLobby;

        [Tooltip("Root container / spawn location for Police Prep phase (Position: X=2000, Y=0, Z=0)")]
        public Transform zonePolicePrep;

        [Tooltip("Root container / spawn location for Combat Arena (Position: X=0, Y=0, Z=0)")]
        public Transform zoneCombatArena;

        public static readonly Vector3 OffGridStagingPosition = new Vector3(-999f, 1f, -999f);

        [Header("Match State Synchronization")]
        [Tooltip("Synchronized match state across all network clients.")]
        public NetworkVariable<MatchState> currentMatchState = new NetworkVariable<MatchState>(
            MatchState.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public MatchState CurrentState => currentMatchState.Value;

        [Header("Map Size Configuration")]
        [Tooltip("Networked arena map boundary size (50, 100, or 150). Read: Everyone, Write: Server.")]
        public NetworkVariable<int> selectedMapSize = new NetworkVariable<int>(
            50,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public int SelectedMapSize => selectedMapSize.Value;

        [Header("Map Generation State")]
        [Tooltip("Deterministic random seed for procedural arena map generation. Read: Everyone, Write: Server.")]
        public NetworkVariable<int> mapGenerationSeed = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public int MapGenerationSeed => mapGenerationSeed.Value;

        public event Action<int> OnMapSeedReceived;

        /// <summary>
        /// Server-only method to update the selected map size boundary.
        /// Strictly clamped to 50 (Small), 100 (Medium), or 150 (Large).
        /// </summary>
        /// <param name="newSize">Target map size (50, 100, or 150)</param>
        public void CmdSetMapSize(int newSize)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[MatchManager] CmdSetMapSize can only be executed on the Server/Host!");
                return;
            }

            int clampedSize = (newSize <= 75) ? 50 : ((newSize <= 125) ? 100 : 150);
            selectedMapSize.Value = clampedSize;
            Debug.Log($"[MatchManager] Server updated selectedMapSize to: {clampedSize}x{clampedSize}");
        }

        /// <summary>
        /// Server-only method to generate a new deterministic map seed.
        /// </summary>
        public void CmdGenerateNewMap()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[MatchManager] CmdGenerateNewMap can only be executed on the Server/Host! Forwarding to RequestGenerateNewMap().");
                RequestGenerateNewMap();
                return;
            }

            int current = mapGenerationSeed.Value;
            int newSeed;
            do
            {
                newSeed = UnityEngine.Random.Range(1, 999999);
            } while (newSeed == current);

            mapGenerationSeed.Value = newSeed;
            Debug.Log($"[MatchManager] Server generated new map seed: {newSeed}");
        }

        /// <summary>
        /// Requests map regeneration from either Host or Client (Assassin).
        /// </summary>
        public void RequestGenerateNewMap()
        {
            if (IsServer)
            {
                CmdGenerateNewMap();
            }
            else
            {
                RequestGenerateNewMapServerRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestGenerateNewMapServerRpc()
        {
            CmdGenerateNewMap();
        }

        public event Action<MatchState, MatchState> OnMatchStateChanged;

        private void Awake()
        {
            Application.runInBackground = true;

            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            PurgeLegacyPrototypeDummies();
            FindZoneReferencesIfNull();
        }

        private void Start()
        {
            PurgeLegacyPrototypeDummies();
            FindZoneReferencesIfNull();
        }

        /// <summary>
        /// Runtime scene sanitation: finds and destroys any legacy prototype dummies or test objects.
        /// </summary>
        public void PurgeLegacyPrototypeDummies()
        {
            var allObjs = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            int purgedCount = 0;
            foreach (var obj in allObjs)
            {
                if (obj == null) continue;
                string objName = obj.name.ToLower();
                bool isPrototypeDummy = objName.Contains("dummy") || 
                                       objName.Contains("targetdummy") || 
                                       objName.Contains("testdummy") ||
                                       objName.Contains("enemy_dummy") ||
                                       objName.StartsWith("target_dummy");

                if (isPrototypeDummy)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(obj);
                        purgedCount++;
                        continue;
                    }
#endif
                    Destroy(obj);
                    purgedCount++;
                }
            }
            if (purgedCount > 0)
            {
                Debug.Log($"[MatchManager] Runtime scene sanitation purged {purgedCount} prototype dummy objects.");
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            FindZoneReferencesIfNull();
            currentMatchState.OnValueChanged += HandleMatchStateChanged;
            mapGenerationSeed.OnValueChanged += HandleMapSeedChanged;

            // Process initial state immediately (crucial for late-joining clients)
            HandleMatchStateChanged(MatchState.WaitingForPlayers, currentMatchState.Value);

            if (mapGenerationSeed.Value > 0)
            {
                HandleMapSeedChanged(0, mapGenerationSeed.Value);
            }

            Debug.Log($"[MatchManager] OnNetworkSpawn -> Initial State: {currentMatchState.Value}, MapSeed: {mapGenerationSeed.Value}, IsServer: {IsServer}, IsClient: {IsClient}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            currentMatchState.OnValueChanged -= HandleMatchStateChanged;
            mapGenerationSeed.OnValueChanged -= HandleMapSeedChanged;
        }

        private void HandleMapSeedChanged(int previousSeed, int newSeed)
        {
            if (newSeed > 0)
            {
                Debug.Log($"[MatchManager] Map Generation Seed updated: {previousSeed} -> {newSeed}");
                OnMapSeedReceived?.Invoke(newSeed);
            }
        }

        public void FindZoneReferencesIfNull()
        {
            if (zoneLobby == null)
            {
                var obj = GameObject.Find("Zone_Lobby");
                if (obj != null) zoneLobby = obj.transform;
            }

            if (zonePolicePrep == null)
            {
                var obj = GameObject.Find("Zone_PolicePrep");
                if (obj != null) zonePolicePrep = obj.transform;
            }

            if (zoneCombatArena == null)
            {
                var obj = GameObject.Find("Zone_CombatArena");
                if (obj != null) zoneCombatArena = obj.transform;
            }

            EnsureOffGridStagingPlatform();
        }

        public void EnsureOffGridStagingPlatform()
        {
            var existing = GameObject.Find("Zone_OffGridStagingPlatform");
            if (existing == null)
            {
                GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = "Zone_OffGridStagingPlatform";
                platform.transform.position = new Vector3(-999f, -0.5f, -999f);
                platform.transform.localScale = new Vector3(15f, 1f, 15f);
                var rend = platform.GetComponent<Renderer>();
                if (rend != null) rend.enabled = false; // Hidden off-grid floor
                var col = platform.GetComponent<BoxCollider>();
                if (col == null) col = platform.AddComponent<BoxCollider>();
                col.enabled = true;
                var netObj = platform.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null) DestroyImmediate(netObj);
            }
        }

        /// <summary>
        /// Server-authoritative entry point to initiate match sequence from WaitingForPlayers to RoleAssignment.
        /// </summary>
        public void StartMatch()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[MatchManager] StartMatch can only be called by the Server/Host!");
                return;
            }

            if (currentMatchState.Value != MatchState.WaitingForPlayers)
            {
                Debug.LogWarning($"[MatchManager] Cannot StartMatch when current state is {currentMatchState.Value}!");
                return;
            }

            Debug.Log("[MatchManager] Server starting match: Transitioning to RoleAssignment...");
            SetMatchState(MatchState.RoleAssignment);
        }

        /// <summary>
        /// Server-authoritative method to transition from PrepPhase to CombatPhase.
        /// Can be called on timer expiration or manual host trigger.
        /// </summary>
        public void TransitionToCombatPhase()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[MatchManager] TransitionToCombatPhase called on non-server client! Forwarding via RPC.");
                RequestStartCombatPhase();
                return;
            }

            if (currentMatchState.Value != MatchState.PrepPhase)
            {
                Debug.LogWarning($"[MatchManager] Cannot transition to CombatPhase when current state is {currentMatchState.Value}!");
                return;
            }

            Debug.Log("[MatchManager] Server transitioning to CombatPhase...");
            SetMatchState(MatchState.CombatPhase);
        }

        public void CmdStartCombatPhase() => TransitionToCombatPhase();

        /// <summary>
        /// Requests combat phase transition from either Host or Client.
        /// </summary>
        public void RequestStartCombatPhase()
        {
            if (IsServer)
            {
                TransitionToCombatPhase();
            }
            else
            {
                RequestStartCombatPhaseServerRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestStartCombatPhaseServerRpc()
        {
            TransitionToCombatPhase();
        }

        [Header("Asymmetric Spawning State")]
        private readonly Dictionary<ulong, int> policeSelectedBreachRooms = new Dictionary<ulong, int>();
        private Vector3 assassinCustomSpawnPosition = Vector3.zero;

        /// <summary>
        /// Server RPC allowing a Police player to select a tactical South Breach Room during PrepPhase.
        /// Records the chosen breach room index without teleporting immediately so the officer stays in Zone_PolicePrep.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SelectBreachRoomServerRpc(int roomIndex, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            int mapSize = selectedMapSize.Value > 0 ? selectedMapSize.Value : 50;
            var breachPositions = GridManager.GetBreachSpawnPositions(mapSize);
            var breachNames = GridManager.GetBreachRoomNames(mapSize);

            if (breachPositions == null || breachPositions.Count == 0) return;

            int clampedIndex = Mathf.Clamp(roomIndex, 0, breachPositions.Count - 1);
            policeSelectedBreachRooms[senderClientId] = clampedIndex;

            string roomName = (clampedIndex < breachNames.Count) ? breachNames[clampedIndex] : $"Room {clampedIndex}";
            Debug.Log($"[MatchManager] Server registered Police ClientId {senderClientId} breach selection: {roomName} (Physical teleportation deferred to CombatPhase start)");
        }

        /// <summary>
        /// Server RPC allowing the Assassin to confirm custom drag-and-drop spawn coordinates during PrepPhase.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ConfirmAssassinSpawnServerRpc(Vector3 spawnPos, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            int mapSize = selectedMapSize.Value > 0 ? selectedMapSize.Value : 50;
            float halfSize = mapSize * 0.5f;
            float minAllowedZ = -halfSize + (mapSize * 0.70f);

            // Server-side validation: must be within top 30% sector and within lateral bounds
            if (spawnPos.z >= minAllowedZ && Mathf.Abs(spawnPos.x) <= halfSize)
            {
                assassinCustomSpawnPosition = new Vector3(spawnPos.x, 1.0f, spawnPos.z);
                Debug.Log($"[MatchManager] Server confirmed Assassin ClientId {senderClientId} custom spawn position: {assassinCustomSpawnPosition}");
            }
            else
            {
                Debug.LogWarning($"[MatchManager] Rejected invalid Assassin spawn position {spawnPos} (Must have Z >= {minAllowedZ})");
            }
        }

        public Vector3 GetAssassinSpawnPosition(int mapSize)
        {
            if (assassinCustomSpawnPosition != Vector3.zero)
            {
                return assassinCustomSpawnPosition;
            }
            return MapGenerator.GetSafeAssassinSpawnPosition(mapSize);
        }

        public Vector3 GetPoliceBreachSpawnPosition(ulong clientId, int fallbackIndex, int mapSize)
        {
            var breachPositions = GridManager.GetBreachSpawnPositions(mapSize);
            if (breachPositions == null || breachPositions.Count == 0)
            {
                return MapGenerator.GetSafePoliceSpawnPosition(fallbackIndex, mapSize);
            }

            if (policeSelectedBreachRooms.TryGetValue(clientId, out int roomIndex))
            {
                int clamped = Mathf.Clamp(roomIndex, 0, breachPositions.Count - 1);
                return breachPositions[clamped];
            }

            int defaultIndex = Mathf.Abs(fallbackIndex) % breachPositions.Count;
            return breachPositions[defaultIndex];
        }

        /// <summary>
        /// Server-only state controller that handles phase entry logic and transitions.
        /// </summary>
        /// <param name="newState">Target match state.</param>
        public void SetMatchState(MatchState newState)
        {
            if (!IsServer) return;

            MatchState previous = currentMatchState.Value;
            currentMatchState.Value = newState;

            ExecuteServerStateEntry(newState);
        }

        private void ExecuteServerStateEntry(MatchState state)
        {
            switch (state)
            {
                case MatchState.RoleAssignment:
                    ExecuteRoleAssignment();
                    break;

                case MatchState.PrepPhase:
                    ExecutePrepPhaseTeleportation();
                    break;

                case MatchState.CombatPhase:
                    ExecuteCombatPhaseTeleportation();
                    Debug.Log("[MatchManager] Entered CombatPhase.");
                    break;

                case MatchState.MatchEnd:
                    Debug.Log("[MatchManager] Entered MatchEnd.");
                    break;
            }
        }

        /// <summary>
        /// Gathers all connected PlayerNetworkState objects and performs a server-side weighted random roll.
        /// Selects EXACTLY ONE player to be Assassin. Assigns all remaining players Police.
        /// Instantly transitions to PrepPhase.
        /// </summary>
        private void ExecuteRoleAssignment()
        {
            if (!IsServer) return;

            List<PlayerNetworkState> connectedPlayers = new List<PlayerNetworkState>();

            if (NetworkManager.Singleton != null)
            {
                foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                {
                    var playerObj = clientPair.Value.PlayerObject;
                    if (playerObj != null)
                    {
                        var playerState = playerObj.GetComponent<PlayerNetworkState>();
                        if (playerState != null)
                        {
                            connectedPlayers.Add(playerState);
                        }
                    }
                }
            }

            if (connectedPlayers.Count == 0)
            {
                Debug.LogWarning("[MatchManager] RoleAssignment attempted with 0 connected players!");
                SetMatchState(MatchState.PrepPhase);
                return;
            }

            // Weighted random selection for Assassin (1 Assassin, N Police)
            int assassinIndex = UnityEngine.Random.Range(0, connectedPlayers.Count);

            Debug.Log($"[MatchManager] RoleAssignment -> Total Players: {connectedPlayers.Count}, Selected Assassin Index: {assassinIndex}");

            for (int i = 0; i < connectedPlayers.Count; i++)
            {
                PlayerRole assignedRole = (i == assassinIndex) ? PlayerRole.Assassin : PlayerRole.Police;
                connectedPlayers[i].ServerAssignRole(assignedRole);
                Debug.Log($"[MatchManager] Assigned ClientId {connectedPlayers[i].OwnerClientId} -> {assignedRole}");
            }

            // Generate initial procedural map seed if not yet generated
            if (mapGenerationSeed.Value == 0)
            {
                CmdGenerateNewMap();
            }

            // Once roles are assigned, instantly transition to PrepPhase
            SetMatchState(MatchState.PrepPhase);
        }

        /// <summary>
        /// Teleports Police players to Zone_PolicePrep and Assassin player to Zone_CombatArena.
        /// </summary>
        private void ExecutePrepPhaseTeleportation()
        {
            if (!IsServer) return;

            PurgeLegacyPrototypeDummies();
            FindZoneReferencesIfNull();

            Vector3 lobbyPos = zoneLobby != null ? zoneLobby.position : new Vector3(1000f, 0f, 0f);
            Vector3 policePrepPos = zonePolicePrep != null ? zonePolicePrep.position : new Vector3(2000f, 0f, 0f);
            Vector3 combatArenaPos = zoneCombatArena != null ? zoneCombatArena.position : Vector3.zero;

            int policeCount = 0;

            if (NetworkManager.Singleton != null)
            {
                foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                {
                    var playerObj = clientPair.Value.PlayerObject;
                    if (playerObj == null) continue;

                    var playerState = playerObj.GetComponent<PlayerNetworkState>();
                    if (playerState == null) continue;

                    if (playerState.Role == PlayerRole.Police)
                    {
                        // Arrange police players in a grid layout at Police Prep zone
                        float offsetX = (policeCount % 4) * 2.5f - 3.75f;
                        float offsetZ = (policeCount / 4) * 2.5f - 2f;
                        Vector3 targetSpawn = policePrepPos + new Vector3(offsetX, 1f, offsetZ);

                        playerState.ServerTeleport(targetSpawn, Quaternion.identity);
                        Debug.Log($"[MatchManager] Teleported Police ClientId {playerState.OwnerClientId} to Zone_PolicePrep ({targetSpawn})");
                        policeCount++;
                    }
                    else if (playerState.Role == PlayerRole.Assassin)
                    {
                        // Assassin physical character is staged off-grid during PrepPhase
                        Vector3 targetSpawn = OffGridStagingPosition;

                        playerState.ServerTeleport(targetSpawn, Quaternion.Euler(0f, 180f, 0f));
                        Debug.Log($"[MatchManager] Teleported Assassin ClientId {playerState.OwnerClientId} to OffGrid Staging Zone ({targetSpawn})");
                    }
                    else
                    {
                        // Fallback unassigned -> lobby
                        playerState.ServerTeleport(lobbyPos + Vector3.up * 1f, Quaternion.identity);
                    }
                }
            }
        }

        /// <summary>
        /// Teleports Police players from Zone_PolicePrep into the generated combat arena corridors (South spawn),
        /// and ensures the Assassin avatar is positioned in the arena (North spawn).
        /// </summary>
        private void ExecuteCombatPhaseTeleportation()
        {
            if (!IsServer) return;

            PurgeLegacyPrototypeDummies();
            FindZoneReferencesIfNull();

            int mapSize = selectedMapSize.Value > 0 ? selectedMapSize.Value : 50;
            int policeCount = 0;

            if (NetworkManager.Singleton != null)
            {
                foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                {
                    var playerObj = clientPair.Value.PlayerObject;
                    if (playerObj == null) continue;

                    var playerState = playerObj.GetComponent<PlayerNetworkState>();
                    if (playerState == null) continue;

                    if (playerState.Role == PlayerRole.Police)
                    {
                        Vector3 targetSpawn = GetPoliceBreachSpawnPosition(playerState.OwnerClientId, policeCount, mapSize);
                        playerState.ServerTeleport(targetSpawn, Quaternion.identity);
                        Debug.Log($"[MatchManager] Teleported Police ClientId {playerState.OwnerClientId} to Breach Room ({targetSpawn})");
                        policeCount++;
                    }
                    else if (playerState.Role == PlayerRole.Assassin)
                    {
                        Vector3 targetSpawn = GetAssassinSpawnPosition(mapSize);
                        playerState.ServerTeleport(targetSpawn, Quaternion.Euler(0f, 180f, 0f));
                        Debug.Log($"[MatchManager] Teleported Assassin ClientId {playerState.OwnerClientId} to Combat Arena ({targetSpawn})");
                    }
                }

                Physics.SyncTransforms();
            }
        }

        private void HandleMatchStateChanged(MatchState previousState, MatchState newState)
        {
            Debug.Log($"[MatchManager] MatchState changed from {previousState} to {newState} (IsServer: {IsServer}, IsClient: {IsClient})");
            FindZoneReferencesIfNull();

            // When state transitions into PrepPhase, local clients immediately respond and execute zone movement
            if (newState == MatchState.PrepPhase)
            {
                ExecuteLocalClientPrepPhaseResponse();
            }
            else if (newState == MatchState.CombatPhase)
            {
                ExecuteLocalClientCombatPhaseResponse();
            }

            OnMatchStateChanged?.Invoke(previousState, newState);
        }

        /// <summary>
        /// Executes client-side / local player zone positioning response when entering PrepPhase.
        /// Ensures the local player moves to Zone_PolicePrep or Zone_CombatArena even if network RPCs are delayed.
        /// </summary>
        public void ExecuteLocalClientPrepPhaseResponse()
        {
            FindZoneReferencesIfNull();

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return;
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj == null) return;

            var playerState = localPlayerObj.GetComponent<PlayerNetworkState>();
            if (playerState == null) return;

            Vector3 targetSpawn;
            Quaternion targetRot;

            if (playerState.Role == PlayerRole.Police)
            {
                Vector3 policePrepPos = zonePolicePrep != null ? zonePolicePrep.position : new Vector3(2000f, 0f, 0f);
                float offsetX = (playerState.OwnerClientId % 4) * 2.5f - 3.75f;
                float offsetZ = (playerState.OwnerClientId / 4) * 2.5f - 2f;
                targetSpawn = policePrepPos + new Vector3(offsetX, 1f, offsetZ);
                targetRot = Quaternion.identity;
            }
            else if (playerState.Role == PlayerRole.Assassin)
            {
                // Physical character is staged off-grid while camera enters God-View
                targetSpawn = OffGridStagingPosition;
                targetRot = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                targetSpawn = zoneLobby != null ? zoneLobby.position + Vector3.up : new Vector3(1000f, 1f, 0f);
                targetRot = Quaternion.identity;
            }

            // Immediately set local transform and physics position
            localPlayerObj.transform.position = targetSpawn;
            localPlayerObj.transform.rotation = targetRot;

            var rb = localPlayerObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = targetSpawn;
                rb.rotation = targetRot;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var netTransform = localPlayerObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (netTransform != null && playerState.IsOwner)
            {
                netTransform.Teleport(targetSpawn, targetRot, localPlayerObj.transform.localScale);
            }

            Physics.SyncTransforms();

            // Update local camera and movement
            playerState.UpdatePrepPhaseCameraAndMovement(MatchState.PrepPhase);

            var pwm = localPlayerObj.GetComponent<PlayerWeaponManager>();
            if (pwm != null)
            {
                pwm.ResetAllWeaponStates();
            }

            PlayerNetworkState.ForceEnableAllPlayerRenderers();

            Debug.Log($"[MatchManager] Local Client {playerState.OwnerClientId} ({playerState.Role}) executed PrepPhase teleport to {targetSpawn}");
        }

        /// <summary>
        /// Executes client-side / local player response when entering CombatPhase via a sequential settlement pipeline:
        /// Step 1: Teleport to spawn
        /// Step 2: Sync physics transforms
        /// Step 3: Yield frame for NetworkTransform & camera settlement
        /// Step 4: Clear Fog memory and enable FOV cone at settled coordinates
        /// </summary>
        public void ExecuteLocalClientCombatPhaseResponse()
        {
            StartCoroutine(LocalClientCombatPhaseSettlementRoutine());
        }

        private System.Collections.IEnumerator LocalClientCombatPhaseSettlementRoutine()
        {
            FindZoneReferencesIfNull();

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) yield break;
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj == null) yield break;

            var playerState = localPlayerObj.GetComponent<PlayerNetworkState>();
            if (playerState == null) yield break;

            int mapSize = selectedMapSize.Value > 0 ? selectedMapSize.Value : 50;

            Vector3 targetSpawn;
            Quaternion targetRot;

            if (playerState.Role == PlayerRole.Police)
            {
                int localBreachIndex = (PoliceBreachUI.Instance != null) ? PoliceBreachUI.Instance.SelectedRoomIndex : 0;
                var breachPositions = GridManager.GetBreachSpawnPositions(mapSize);
                if (breachPositions != null && breachPositions.Count > 0)
                {
                    int clamped = Mathf.Clamp(localBreachIndex, 0, breachPositions.Count - 1);
                    targetSpawn = breachPositions[clamped];
                }
                else
                {
                    targetSpawn = MapGenerator.GetSafePoliceSpawnPosition(0, mapSize);
                }
                targetRot = Quaternion.identity;
            }
            else if (playerState.Role == PlayerRole.Assassin)
            {
                if (AssassinPlacementController.Instance != null && AssassinPlacementController.Instance.HasConfirmedSpawn)
                {
                    targetSpawn = AssassinPlacementController.Instance.ConfirmedSpawnPosition;
                }
                else
                {
                    targetSpawn = GetAssassinSpawnPosition(mapSize);
                }
                targetRot = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                targetSpawn = new Vector3(0f, 1f, 0f);
                targetRot = Quaternion.identity;
            }

            // Step 1 (Teleport): Move local transform to destination spawn coordinates
            playerState.ApplyTeleport(targetSpawn, targetRot);

            // Step 2 (Physics Sync): Update engine physics matrix immediately
            Physics.SyncTransforms();

            // Step 3 (Yield Frame): Allow NetworkTransform, rendering, and camera to settle at target
            yield return new WaitForEndOfFrame();

            // Step 4 (Memory Wipe & FOV Enable): Wipe memory buffers at settled coordinates, then activate weapon FOV
            if (playerState.Role == PlayerRole.Police)
            {
                HunterVsHider.Vision.FogMemoryManager.Instance?.ResetFog();
                var dynFov = localPlayerObj.GetComponentInChildren<HunterVsHider.Vision.DynamicFOV>(true);
                if (dynFov != null)
                {
                    dynFov.ClearExploredMemoryGrid();
                }
            }
            else if (playerState.Role == PlayerRole.Assassin)
            {
                HunterVsHider.Vision.FogMemoryManager.Instance?.SetAllToMemory(0.5f);
            }

            // Re-apply role vision and snap camera back
            playerState.ApplyRoleVision();
            playerState.UpdatePrepPhaseCameraAndMovement(MatchState.CombatPhase);

            Debug.Log($"[MatchManager] Local Client {playerState.OwnerClientId} ({playerState.Role}) completed CombatPhase settlement at {targetSpawn}");
        }



        /// <summary>
        /// Debug / recovery helper to reset match back to WaitingForPlayers in Lobby.
        /// </summary>
        public void ResetToLobby()
        {
            if (!IsServer) return;

            FindZoneReferencesIfNull();
            Vector3 lobbyPos = zoneLobby != null ? zoneLobby.position : new Vector3(1000f, 0f, 0f);

            int count = 0;
            if (NetworkManager.Singleton != null)
            {
                foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                {
                    var playerObj = clientPair.Value.PlayerObject;
                    if (playerObj == null) continue;

                    var playerState = playerObj.GetComponent<PlayerNetworkState>();
                    if (playerState != null)
                    {
                        playerState.ServerAssignRole(PlayerRole.Unassigned);
                        float offsetX = (count % 4) * 2.5f - 3.75f;
                        float offsetZ = (count / 4) * 2.5f;
                        playerState.ServerTeleport(lobbyPos + new Vector3(offsetX, 1f, offsetZ), Quaternion.identity);
                        count++;
                    }
                }
            }

            SetMatchState(MatchState.WaitingForPlayers);
            Debug.Log("[MatchManager] Reset all players to Lobby.");
        }
    }
}
