using Unity.Netcode;
using UnityEngine;
using HunterVsHider.Player;

namespace HunterVsHider.Managers
{
    public class NetworkHUD : MonoBehaviour
    {
        [Header("UI Configuration")]
        public bool showGUI = true;
        public int guiOffsetX = 15;
        public int guiOffsetY = 15;

        private void Start()
        {
            // Process command-line arguments for automated testing / standalone instances
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-mode" && i + 1 < args.Length)
                {
                    string mode = args[i + 1].ToLower();
                    if (mode == "host")
                    {
                        NetworkManager.Singleton.StartHost();
                    }
                    else if (mode == "client")
                    {
                        NetworkManager.Singleton.StartClient();
                    }
                    else if (mode == "server")
                    {
                        NetworkManager.Singleton.StartServer();
                    }
                }
            }
        }

        private string relayJoinCodeInput = "";

        private void OnGUI()
        {
            if (!showGUI) return;

            if (NetworkManager.Singleton == null)
            {
                GUILayout.BeginArea(new Rect(guiOffsetX, guiOffsetY, 240, 100), GUI.skin.box);
                GUILayout.Label("NetworkManager not found!");
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(guiOffsetX, guiOffsetY, 280, 420), GUI.skin.box);

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                GUILayout.Label("<b>== Hunter v Hider NGO ==</b>");
                GUILayout.Space(5);

                if (GUILayout.Button("Start Host (LAN)", GUILayout.Height(26)))
                {
                    NetworkManager.Singleton.StartHost();
                }

                if (GUILayout.Button("Start Client (LAN)", GUILayout.Height(26)))
                {
                    NetworkManager.Singleton.StartClient();
                }

                GUILayout.Space(6);
                GUILayout.Label("<b>-- Relay Matchmaking --</b>");

                if (GUILayout.Button("Host Game (Relay)", GUILayout.Height(30)))
                {
                    if (RelayManager.Instance != null)
                    {
                        _ = RelayManager.Instance.CreateRelayHost();
                    }
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("Code:", GUILayout.Width(45));
                relayJoinCodeInput = GUILayout.TextField(relayJoinCodeInput, 10, GUILayout.Height(24));
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Join Game (Relay)", GUILayout.Height(30)))
                {
                    if (RelayManager.Instance != null && !string.IsNullOrWhiteSpace(relayJoinCodeInput))
                    {
                        _ = RelayManager.Instance.JoinRelayClient(relayJoinCodeInput);
                    }
                }

                GUILayout.Space(6);
                if (GUILayout.Button("Start Server (Dedicated LAN)", GUILayout.Height(24)))
                {
                    NetworkManager.Singleton.StartServer();
                }
            }
            else
            {
                string statusMode = NetworkManager.Singleton.IsHost ? "HOST" : (NetworkManager.Singleton.IsServer ? "SERVER" : "CLIENT");
                GUILayout.Label($"<b>Status:</b> <color=green>{statusMode}</color>");
                GUILayout.Label($"<b>Local Client ID:</b> {NetworkManager.Singleton.LocalClientId}");
                GUILayout.Label($"<b>Connected Clients:</b> {NetworkManager.Singleton.ConnectedClients.Count}");
                GUILayout.Space(5);

                // If Server / Host, show role assignment controls for each connected client
                if (NetworkManager.Singleton.IsServer)
                {
                    GUILayout.Label("<b>--- Role Assignment ---</b>");
                    foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                    {
                        ulong clientId = clientPair.Key;
                        var networkObject = clientPair.Value.PlayerObject;
                        string roleName = "No Player Obj";

                        PlayerNetworkState state = null;
                        if (networkObject != null)
                        {
                            state = networkObject.GetComponent<PlayerNetworkState>();
                            if (state != null)
                            {
                                roleName = state.Role.ToString();
                            }
                        }

                        GUILayout.Label($"Client {clientId}: <b>{roleName}</b>");
                        if (state != null)
                        {
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button("Police", GUILayout.Height(22)))
                            {
                                state.ServerAssignRole(PlayerRole.Police);
                            }
                            if (GUILayout.Button("Assassin", GUILayout.Height(22)))
                            {
                                state.ServerAssignRole(PlayerRole.Assassin);
                            }
                            if (GUILayout.Button("Reset", GUILayout.Height(22)))
                            {
                                state.ServerAssignRole(PlayerRole.Unassigned);
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                else
                {
                    // Local Client info
                    var localPlayerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
                    if (localPlayerObj != null)
                    {
                        var state = localPlayerObj.GetComponent<PlayerNetworkState>();
                        if (state != null)
                        {
                            GUILayout.Label($"<b>Your Role:</b> {state.Role}");
                        }
                    }
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Disconnect / Shutdown", GUILayout.Height(26)))
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }

            GUILayout.EndArea();
        }
    }
}
