using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using HunterVsHider.Player;

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

        [Header("Zone References")]
        [Tooltip("Root container / spawn location for pre-match Lobby (Position: X=1000, Y=0, Z=0)")]
        public Transform zoneLobby;

        [Tooltip("Root container / spawn location for Police Prep phase (Position: X=2000, Y=0, Z=0)")]
        public Transform zonePolicePrep;

        [Tooltip("Root container / spawn location for Combat Arena (Position: X=0, Y=0, Z=0)")]
        public Transform zoneCombatArena;

        [Header("Match State Synchronization")]
        [Tooltip("Synchronized match state across all network clients.")]
        public NetworkVariable<MatchState> currentMatchState = new NetworkVariable<MatchState>(
            MatchState.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public MatchState CurrentState => currentMatchState.Value;

        [Header("UI Configuration")]
        [SerializeField] private bool showMatchHUD = true;
        [SerializeField] private int hudOffsetX = 15;
        [SerializeField] private int hudOffsetY = 345;

        public event Action<MatchState, MatchState> OnMatchStateChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            FindZoneReferencesIfNull();
        }

        private void Start()
        {
            FindZoneReferencesIfNull();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            currentMatchState.OnValueChanged += HandleMatchStateChanged;
            Debug.Log($"[MatchManager] OnNetworkSpawn -> Current State: {currentMatchState.Value}, IsServer: {IsServer}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            currentMatchState.OnValueChanged -= HandleMatchStateChanged;
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

            // Once roles are assigned, instantly transition to PrepPhase
            SetMatchState(MatchState.PrepPhase);
        }

        /// <summary>
        /// Teleports Police players to Zone_PolicePrep and Assassin player to Zone_CombatArena.
        /// </summary>
        private void ExecutePrepPhaseTeleportation()
        {
            if (!IsServer) return;

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
                        // Assassin teleports directly into the Combat Arena (e.g. North Spawn at Y=1, Z=18)
                        Vector3 targetSpawn = combatArenaPos + new Vector3(0f, 1f, 18f);

                        playerState.ServerTeleport(targetSpawn, Quaternion.Euler(0f, 180f, 0f));
                        Debug.Log($"[MatchManager] Teleported Assassin ClientId {playerState.OwnerClientId} to Zone_CombatArena ({targetSpawn})");
                    }
                    else
                    {
                        // Fallback unassigned -> lobby
                        playerState.ServerTeleport(lobbyPos + Vector3.up * 1f, Quaternion.identity);
                    }
                }
            }
        }

        private void HandleMatchStateChanged(MatchState previousState, MatchState newState)
        {
            Debug.Log($"[MatchManager] MatchState changed from {previousState} to {newState}");
            OnMatchStateChanged?.Invoke(previousState, newState);
        }

        private void OnGUI()
        {
            if (!showMatchHUD) return;
            if (NetworkManager.Singleton == null || (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)) return;

            GUILayout.BeginArea(new Rect(hudOffsetX, hudOffsetY, 260, 140), GUI.skin.box);

            GUILayout.Label("<b>== MATCH CONTROLLER ==</b>");
            GUILayout.Label($"<b>State:</b> <color=yellow>{currentMatchState.Value}</color>");

            if (IsServer)
            {
                if (currentMatchState.Value == MatchState.WaitingForPlayers)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                    if (GUILayout.Button("<b>START MATCH (Host)</b>", GUILayout.Height(35)))
                    {
                        StartMatch();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUILayout.Space(4);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Prep Phase", GUILayout.Height(24)))
                    {
                        SetMatchState(MatchState.PrepPhase);
                    }
                    if (GUILayout.Button("Combat", GUILayout.Height(24)))
                    {
                        SetMatchState(MatchState.CombatPhase);
                    }
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Reset to Lobby", GUILayout.Height(22)))
                    {
                        ResetToLobby();
                    }
                }
            }
            else
            {
                GUILayout.Label("<color=cyan>Waiting for Host to start...</color>");
            }

            GUILayout.EndArea();
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
