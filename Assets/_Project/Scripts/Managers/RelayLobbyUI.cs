using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

namespace HunterVsHider.Managers
{
    /// <summary>
    /// Manages the UI state machine for Relay matchmaking:
    /// - MainMenuPanel: Initial Host & Join controls.
    /// - WaitingRoomPanel: Persistent Join Code display, connected players list, and Host Start Match button.
    /// </summary>
    public class RelayLobbyUI : MonoBehaviour
    {
        public static RelayLobbyUI Instance { get; private set; }

        [Header("Panels")]
        [Tooltip("Main menu panel containing Host/Join buttons and join code input.")]
        public GameObject mainMenuPanel;

        [Tooltip("Waiting room panel displayed while players gather in Zone_Lobby.")]
        public GameObject waitingRoomPanel;

        [Header("Main Menu Elements")]
        [Tooltip("Button to host a Relay match.")]
        public Button buttonHostGame;

        [Tooltip("Input field for the client to type a Join Code.")]
        public InputField inputJoinCode;

        [Tooltip("Button for the client to join using the typed code.")]
        public Button buttonJoinGame;

        [Tooltip("Status notification text on the main menu.")]
        public Text textStatus;

        [Header("Waiting Room Elements")]
        [Tooltip("Dedicated persistent Join Code text (TMP_Text).")]
        public TMP_Text txtPersistentJoinCode;

        [Tooltip("Text displaying the real-time list of connected players in lobby.")]
        public TMP_Text txtConnectedPlayers;

        [Tooltip("Button to start the match (Server/Host only).")]
        public Button buttonStartMatch;

        [Tooltip("Button to copy the persistent Join Code to clipboard.")]
        public Button buttonCopyPersistentCode;

        [Tooltip("Sub-status or instructions in the waiting room.")]
        public TMP_Text txtWaitingStatus;

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

            // Ensure initial panel states
            ShowMainMenu();
        }

        private void Start()
        {
            BindButtons();

            // Subscribe to RelayManager events
            if (RelayManager.Instance != null)
            {
                RelayManager.Instance.OnJoinCodeGenerated += HandleJoinCodeGenerated;
                RelayManager.Instance.OnStatusMessageChanged += HandleStatusMessage;
            }

            // Subscribe to NetworkManager events
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            // Subscribe to MatchManager state changes to auto-hide when match begins
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged += HandleMatchStateChanged;
            }

            UpdateStatus("Ready. Click Host Game or enter a code to Join.");
        }

        private void OnDestroy()
        {
            if (RelayManager.Instance != null)
            {
                RelayManager.Instance.OnJoinCodeGenerated -= HandleJoinCodeGenerated;
                RelayManager.Instance.OnStatusMessageChanged -= HandleStatusMessage;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
            }
        }

        private void Update()
        {
            // Keep connected players list and host button state refreshed if waiting room is open
            if (waitingRoomPanel != null && waitingRoomPanel.activeSelf)
            {
                UpdateWaitingRoomStatus();
            }
        }

        private void BindButtons()
        {
            if (buttonHostGame != null)
            {
                buttonHostGame.onClick.RemoveAllListeners();
                buttonHostGame.onClick.AddListener(OnHostButtonClicked);
            }

            if (buttonJoinGame != null)
            {
                buttonJoinGame.onClick.RemoveAllListeners();
                buttonJoinGame.onClick.AddListener(OnJoinButtonClicked);
            }

            if (buttonCopyPersistentCode != null)
            {
                buttonCopyPersistentCode.onClick.RemoveAllListeners();
                buttonCopyPersistentCode.onClick.AddListener(OnCopyPersistentCodeClicked);
            }

            if (buttonStartMatch != null)
            {
                buttonStartMatch.onClick.RemoveAllListeners();
                buttonStartMatch.onClick.AddListener(OnStartMatchClicked);
            }

            if (inputJoinCode != null)
            {
                inputJoinCode.characterLimit = 12;
                inputJoinCode.onEndEdit.AddListener(val =>
                {
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        OnJoinButtonClicked();
                    }
                });
            }
        }

        public async void OnHostButtonClicked()
        {
            if (RelayManager.Instance == null)
            {
                UpdateStatus("Error: RelayManager not found!");
                return;
            }

            SetMainMenuInteractable(false);
            UpdateStatus("Requesting Host Allocation & Join Code...");

            string joinCode = await RelayManager.Instance.CreateRelayHost();
            if (!string.IsNullOrEmpty(joinCode))
            {
                ShowWaitingRoom(joinCode);
            }
            else
            {
                SetMainMenuInteractable(true);
                UpdateStatus("Failed to create Relay host. See console for details.");
            }
        }

        public async void OnJoinButtonClicked()
        {
            if (RelayManager.Instance == null)
            {
                UpdateStatus("Error: RelayManager not found!");
                return;
            }

            string code = inputJoinCode != null ? inputJoinCode.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                UpdateStatus("Please enter a valid 6-character Join Code.");
                return;
            }

            SetMainMenuInteractable(false);
            UpdateStatus($"Connecting to Relay with Code: {code.ToUpper()}...");

            bool success = await RelayManager.Instance.JoinRelayClient(code);
            if (success)
            {
                ShowWaitingRoom(code);
            }
            else
            {
                SetMainMenuInteractable(true);
                UpdateStatus("Failed to join Relay session. Check code and try again.");
            }
        }

        public void OnCopyPersistentCodeClicked()
        {
            string code = string.Empty;
            if (RelayManager.Instance != null && !string.IsNullOrEmpty(RelayManager.Instance.CurrentJoinCode))
            {
                code = RelayManager.Instance.CurrentJoinCode;
            }

            if (!string.IsNullOrEmpty(code))
            {
                GUIUtility.systemCopyBuffer = code;
                if (txtWaitingStatus != null)
                {
                    txtWaitingStatus.text = $"<color=#55FF88>Copied Join Code ({code}) to clipboard!</color>";
                }
            }
        }

        public void OnStartMatchClicked()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[RelayLobbyUI] Only the Host can start the match!");
                return;
            }

            if (MatchManager.Instance != null)
            {
                Debug.Log("[RelayLobbyUI] Host clicked Start Match -> Initiating Match Sequence.");
                MatchManager.Instance.StartMatch();
            }
            else
            {
                Debug.LogWarning("[RelayLobbyUI] MatchManager.Instance not found!");
            }
        }

        public void ShowMainMenu()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
            SetMainMenuInteractable(true);
        }

        public void ShowWaitingRoom(string joinCode)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (waitingRoomPanel != null) waitingRoomPanel.SetActive(true);

            if (txtPersistentJoinCode != null)
            {
                txtPersistentJoinCode.text = $"LOBBY CODE: {joinCode.ToUpper()}";
            }

            UpdateWaitingRoomStatus();
        }

        public void HideAllPanels()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
        }

        private void UpdateWaitingRoomStatus()
        {
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            // Configure Start Match Button based on role
            if (buttonStartMatch != null)
            {
                buttonStartMatch.interactable = isHost;
                var btnText = buttonStartMatch.GetComponentInChildren<TMP_Text>();
                if (btnText == null)
                {
                    var uText = buttonStartMatch.GetComponentInChildren<Text>();
                    if (uText != null)
                    {
                        uText.text = isHost ? "Start Match" : "Waiting for Host...";
                    }
                }
                else
                {
                    btnText.text = isHost ? "Start Match" : "Waiting for Host...";
                }
            }

            // Update connected players list
            if (txtConnectedPlayers != null && NetworkManager.Singleton != null)
            {
                int count = NetworkManager.Singleton.ConnectedClients.Count;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"<b>Connected Players ({count}):</b>");

                foreach (var client in NetworkManager.Singleton.ConnectedClients)
                {
                    ulong clientId = client.Key;
                    bool isLocal = clientId == NetworkManager.Singleton.LocalClientId;
                    string tag = isLocal ? " (You)" : "";
                    string roleStr = clientId == 0 ? " [Host]" : " [Client]";
                    sb.AppendLine($" • Player {clientId}{roleStr}{tag}");
                }

                txtConnectedPlayers.text = sb.ToString();
            }

            if (txtWaitingStatus != null && !txtWaitingStatus.text.Contains("Copied"))
            {
                txtWaitingStatus.text = isHost
                    ? "Gather players in Zone_Lobby. Click 'Start Match' when ready."
                    : "Joined lobby. Waiting for Host to start match...";
            }
        }

        private void HandleJoinCodeGenerated(string joinCode)
        {
            ShowWaitingRoom(joinCode);
        }

        private void HandleStatusMessage(string message)
        {
            UpdateStatus(message);
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                string code = RelayManager.Instance != null ? RelayManager.Instance.CurrentJoinCode : "";
                if (!string.IsNullOrEmpty(code))
                {
                    ShowWaitingRoom(code);
                }
            }
            UpdateWaitingRoomStatus();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                UpdateStatus("Disconnected from server.");
                ShowMainMenu();
            }
            else
            {
                UpdateWaitingRoomStatus();
            }
        }

        private void HandleMatchStateChanged(MatchState previousState, MatchState newState)
        {
            Debug.Log($"[RelayLobbyUI] Match state changed: {previousState} -> {newState}");

            // When the match leaves the WaitingForPlayers lobby phase, hide the waiting room
            if (newState != MatchState.WaitingForPlayers)
            {
                HideAllPanels();
            }
            else
            {
                // If reset to WaitingForPlayers, restore waiting room if still connected
                if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient))
                {
                    string code = RelayManager.Instance != null ? RelayManager.Instance.CurrentJoinCode : "";
                    ShowWaitingRoom(code);
                }
                else
                {
                    ShowMainMenu();
                }
            }
        }

        public void SetMainMenuInteractable(bool interactable)
        {
            if (buttonHostGame != null) buttonHostGame.interactable = interactable;
            if (buttonJoinGame != null) buttonJoinGame.interactable = interactable;
            if (inputJoinCode != null) inputJoinCode.interactable = interactable;
        }

        public void UpdateStatus(string message)
        {
            if (textStatus != null)
            {
                textStatus.text = message;
            }
            Debug.Log($"[RelayLobbyUI] Status: {message}");
        }
    }
}
