using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using HunterVsHider.Managers;
using HunterVsHider.Player;
using HunterVsHider.Map;

namespace HunterVsHider.UI
{
    /// <summary>
    /// Police Breach Selection UI during Prep Phase.
    /// Allows Police officers to select their tactical insertion Breach Room (Alpha, Bravo, Charlie)
    /// and immediately teleports the player's staging avatar inside the selected room.
    /// </summary>
    public class PoliceBreachUI : MonoBehaviour
    {
        public static PoliceBreachUI Instance { get; private set; }

        [Header("UI Containers & References")]
        [Tooltip("Root UI panel containing the Breach selection buttons.")]
        public GameObject breachContainer;

        [Header("Breach Selection Buttons (Optional Canvas Bindings)")]
        public List<Button> breachButtons = new List<Button>();

        [Header("Visual Feedback Colors")]
        public Color normalButtonColor = new Color(0.12f, 0.18f, 0.28f, 0.95f);
        public Color selectedButtonColor = new Color(0.0f, 0.8f, 1.0f, 1.0f);

        [Header("Selection State")]
        [SerializeField] private int selectedRoomIndex = 0;

        public int SelectedRoomIndex => selectedRoomIndex;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (breachContainer == null)
            {
                breachContainer = gameObject;
            }

            SetUIActive(false);
        }

        private void Start()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged += HandleMatchStateChanged;
            }
            SetupButtonListeners();
            RefreshVisibility();
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
            }
        }

        private void Update()
        {
            RefreshVisibility();
        }

        private void HandleMatchStateChanged(MatchState previousState, MatchState newState)
        {
            RefreshVisibility();
        }

        private void SetupButtonListeners()
        {
            if (breachButtons != null)
            {
                for (int i = 0; i < breachButtons.Count; i++)
                {
                    int index = i;
                    if (breachButtons[i] != null)
                    {
                        breachButtons[i].onClick.RemoveAllListeners();
                        breachButtons[i].onClick.AddListener(() => OnBreachButtonClicked(index));
                    }
                }
            }
        }

        public void RefreshVisibility()
        {
            bool shouldBeVisible = false;

            if (MatchManager.Instance != null && MatchManager.Instance.CurrentState == MatchState.PrepPhase)
            {
                PlayerNetworkState localPlayer = GetLocalPlayerState();
                if (localPlayer != null && localPlayer.Role == PlayerRole.Police)
                {
                    shouldBeVisible = true;
                }
            }

            SetUIActive(shouldBeVisible);
        }

        public void SetUIActive(bool active)
        {
            if (breachContainer != null && breachContainer != gameObject)
            {
                breachContainer.SetActive(active);
            }
            else
            {
                var cg = GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = active ? 1f : 0f;
                    cg.interactable = active;
                    cg.blocksRaycasts = active;
                }
            }
        }

        public void OnBreachButtonClicked(int roomIndex)
        {
            selectedRoomIndex = roomIndex;
            Debug.Log($"[PoliceBreachUI] Police Officer selected breach insertion room: {roomIndex} (Insertion delayed to CombatPhase start)");

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.SelectBreachRoomServerRpc(roomIndex);
            }
        }

        private PlayerNetworkState GetLocalPlayerState()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return null;
            var localObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localObj == null) return null;
            return localObj.GetComponent<PlayerNetworkState>();
        }

        private void OnGUI()
        {
            // Interactive UI overlay during Prep Phase for Police players
            if (MatchManager.Instance == null || MatchManager.Instance.CurrentState != MatchState.PrepPhase) return;

            PlayerNetworkState localPlayer = GetLocalPlayerState();
            if (localPlayer == null || localPlayer.Role != PlayerRole.Police) return;

            int mapSize = MatchManager.Instance.SelectedMapSize;
            var roomNames = GridManager.GetBreachRoomNames(mapSize);
            var roomPositions = GridManager.GetBreachSpawnPositions(mapSize);

            int panelW = 420;
            int panelH = 150;
            int x = 20;
            int y = Screen.height - panelH - 30;

            GUI.backgroundColor = new Color(0.06f, 0.10f, 0.16f, 0.95f);
            GUILayout.BeginArea(new Rect(x, y, panelW, panelH), GUI.skin.box);

            GUILayout.Label("<size=14><b>POLICE INSERTION POINT - BREACH STAGING</b></size>");
            GUILayout.Label("<color=#88AACC><size=11>Select your starting tactical insertion point on the South perimeter:</size></color>");
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < roomNames.Count; i++)
            {
                bool isSelected = (selectedRoomIndex == i);
                if (isSelected)
                {
                    GUI.backgroundColor = selectedButtonColor;
                }
                else
                {
                    GUI.backgroundColor = normalButtonColor;
                }

                string btnText = isSelected ? $"<b>[✓] {roomNames[i]}</b>" : $"<b>{roomNames[i]}</b>";
                if (GUILayout.Button(btnText, GUILayout.Height(44)))
                {
                    OnBreachButtonClicked(i);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (selectedRoomIndex >= 0 && selectedRoomIndex < roomPositions.Count)
            {
                Vector3 pos = roomPositions[selectedRoomIndex];
                GUILayout.Label($"<color=#00E5FF><size=11>Target Room: <b>{roomNames[selectedRoomIndex]}</b> | Coordinates: ({pos.x:F1}m, {pos.z:F1}m)</size></color>");
            }

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
