using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using HunterVsHider.Player;

namespace HunterVsHider.Managers
{
    /// <summary>
    /// Manages the UI overlay for the Assassin during the Prep Phase.
    /// Displays the "Generate Random Map" button to request deterministic seed updates.
    /// </summary>
    public class AssassinPrepUI : MonoBehaviour
    {
        public static AssassinPrepUI Instance { get; private set; }

        [Header("UI Container & Elements")]
        [Tooltip("Root panel GameObject containing the Assassin Prep Phase controls.")]
        public GameObject prepContainer;

        [Tooltip("Button to trigger random map seed generation.")]
        public Button buttonGenerateMap;

        [Tooltip("Button to transition from Prep Phase into Combat Phase.")]
        public Button buttonStartCombat;

        [Tooltip("Optional status / seed label.")]
        public TMP_Text textSeedInfo;

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

            if (prepContainer == null)
            {
                prepContainer = gameObject;
            }

            if (buttonGenerateMap != null)
            {
                buttonGenerateMap.onClick.RemoveAllListeners();
                buttonGenerateMap.onClick.AddListener(OnGenerateMapClicked);
            }

            if (buttonStartCombat != null)
            {
                buttonStartCombat.onClick.RemoveAllListeners();
                buttonStartCombat.onClick.AddListener(OnStartCombatClicked);
            }

            SetUIVisible(false);
        }

        private void Start()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged += HandleMatchStateChanged;
                MatchManager.Instance.OnMapSeedReceived += HandleMapSeedReceived;
            }
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
                MatchManager.Instance.OnMapSeedReceived -= HandleMapSeedReceived;
            }
        }

        private void Update()
        {
            // Continuously verify local player role and match state
            RefreshVisibility();
        }

        private void HandleMatchStateChanged(MatchState previousState, MatchState newState)
        {
            RefreshVisibility();
        }

        private void HandleMapSeedReceived(int seed)
        {
            if (textSeedInfo != null)
            {
                textSeedInfo.text = $"MAP SEED: #{seed}";
            }
        }

        public void RefreshVisibility()
        {
            bool shouldBeVisible = false;

            if (MatchManager.Instance != null && MatchManager.Instance.CurrentState == MatchState.PrepPhase)
            {
                PlayerNetworkState localPlayer = GetLocalPlayerState();
                if (localPlayer != null && localPlayer.Role == PlayerRole.Assassin)
                {
                    shouldBeVisible = true;
                }
            }

            SetUIVisible(shouldBeVisible);
        }

        private void SetUIVisible(bool visible)
        {
            if (prepContainer != null && prepContainer.activeSelf != visible)
            {
                prepContainer.SetActive(visible);
            }

            if (buttonGenerateMap != null)
            {
                buttonGenerateMap.interactable = visible;
            }

            if (buttonStartCombat != null)
            {
                buttonStartCombat.interactable = visible;
            }
        }

        public void OnGenerateMapClicked()
        {
            if (MatchManager.Singleton == null)
            {
                Debug.LogWarning("[AssassinPrepUI] MatchManager.Singleton not found!");
                return;
            }

            Debug.Log("[AssassinPrepUI] Assassin requested new random map generation.");
            MatchManager.Singleton.RequestGenerateNewMap();
        }

        public void OnStartCombatClicked()
        {
            if (MatchManager.Singleton == null)
            {
                Debug.LogWarning("[AssassinPrepUI] MatchManager.Singleton not found!");
                return;
            }

            Debug.Log("[AssassinPrepUI] Assassin requested start of Combat Phase.");
            MatchManager.Singleton.RequestStartCombatPhase();
        }

        private PlayerNetworkState GetLocalPlayerState()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return null;
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj == null) return null;
            return playerObj.GetComponent<PlayerNetworkState>();
        }
    }
}
