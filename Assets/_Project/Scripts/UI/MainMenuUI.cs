using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace HunterVsHider.UI
{
    /// <summary>
    /// Streamlined Main Menu Controller with Network Type selection modal (Local LAN vs Public Relay).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        [Header("Main Menu Panels")]
        [Tooltip("Root Main Panel containing game title and primary buttons.")]
        [SerializeField] public GameObject mainPanel;

        [Tooltip("Network Type Modal Panel (Initially hidden).")]
        [SerializeField] public GameObject networkTypeModalPanel;

        [Header("Main Buttons")]
        [SerializeField] public Button hostButton;
        [SerializeField] public Button joinButton;
        [SerializeField] public Button settingsButton;
        [SerializeField] public Button quitButton;

        [Header("Network Modal Buttons")]
        [SerializeField] public Button localConnectionButton;
        [SerializeField] public Button publicConnectionButton;
        [SerializeField] public Button modalCancelButton;

        [Header("Optional Canvas Reference")]
        [SerializeField] public GameObject canvasMainMenu;

        private bool isPendingHostAction;

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

            if (canvasMainMenu == null)
            {
                canvasMainMenu = gameObject;
            }
        }

        private void Start()
        {
            if (networkTypeModalPanel != null)
            {
                networkTypeModalPanel.SetActive(false);
            }

            // Bind Main Menu buttons
            if (hostButton != null)
            {
                hostButton.onClick.RemoveAllListeners();
                hostButton.onClick.AddListener(OnHostButtonClicked);
            }

            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(OnJoinButtonClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(OnQuitButtonClicked);
            }

            // Bind Modal buttons
            if (localConnectionButton != null)
            {
                localConnectionButton.onClick.RemoveAllListeners();
                localConnectionButton.onClick.AddListener(OnLocalConnectionClicked);
            }

            if (publicConnectionButton != null)
            {
                publicConnectionButton.onClick.RemoveAllListeners();
                publicConnectionButton.onClick.AddListener(OnPublicConnectionClicked);
            }

            if (modalCancelButton != null)
            {
                modalCancelButton.onClick.RemoveAllListeners();
                modalCancelButton.onClick.AddListener(OnModalCancelClicked);
            }
        }

        public void OnHostButtonClicked()
        {
            isPendingHostAction = true;
            if (networkTypeModalPanel != null)
            {
                networkTypeModalPanel.SetActive(true);
            }
            Debug.Log("[MainMenuUI] Host Match clicked -> Opened Network Type Modal");
        }

        public void OnJoinButtonClicked()
        {
            isPendingHostAction = false;
            if (networkTypeModalPanel != null)
            {
                networkTypeModalPanel.SetActive(true);
            }
            Debug.Log("[MainMenuUI] Join Match clicked -> Opened Network Type Modal");
        }

        public void OnModalCancelClicked()
        {
            if (networkTypeModalPanel != null)
            {
                networkTypeModalPanel.SetActive(false);
            }
            Debug.Log("[MainMenuUI] Cancelled Network Type Modal");
        }

        public void OnLocalConnectionClicked()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[MainMenuUI] NetworkManager.Singleton not found!");
                return;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("127.0.0.1", 7777);
            }

            bool success = false;
            if (isPendingHostAction)
            {
                Debug.Log("[MainMenuUI] Starting Local Host on 127.0.0.1:7777...");
                success = NetworkManager.Singleton.StartHost();
            }
            else
            {
                Debug.Log("[MainMenuUI] Connecting to Local Host at 127.0.0.1:7777...");
                success = NetworkManager.Singleton.StartClient();
            }

            if (success)
            {
                HideMainMenu();
            }
            else
            {
                Debug.LogError("[MainMenuUI] Failed to start Netcode connection!");
            }
        }

        public void OnPublicConnectionClicked()
        {
            Debug.Log("[MainMenuUI] Public Connection (Relay) selected.");
            if (HunterVsHider.Managers.RelayLobbyUI.Instance != null)
            {
                if (isPendingHostAction)
                {
                    HunterVsHider.Managers.RelayLobbyUI.Instance.OnHostButtonClicked();
                }
                else
                {
                    if (HunterVsHider.Managers.RelayLobbyUI.Instance.mainMenuPanel != null)
                    {
                        HunterVsHider.Managers.RelayLobbyUI.Instance.mainMenuPanel.SetActive(true);
                    }
                }
                HideMainMenu();
            }
            else if (HunterVsHider.Managers.RelayManager.Instance != null)
            {
                if (isPendingHostAction)
                {
                    _ = HunterVsHider.Managers.RelayManager.Instance.CreateRelayHost(10);
                }
                HideMainMenu();
            }
        }

        public void OnSettingsButtonClicked()
        {
            Debug.Log("[MainMenuUI] Settings clicked.");
        }

        public void OnQuitButtonClicked()
        {
            Debug.Log("[MainMenuUI] Quitting application.");
            Application.Quit();
        }

        public void HideMainMenu()
        {
            if (networkTypeModalPanel != null)
            {
                networkTypeModalPanel.SetActive(false);
            }

            if (canvasMainMenu != null)
            {
                canvasMainMenu.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void ShowMainMenu()
        {
            if (canvasMainMenu != null)
            {
                canvasMainMenu.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }

            if (networkTypeModalPanel != null)
            {
                networkTypeModalPanel.SetActive(false);
            }
        }
    }
}
