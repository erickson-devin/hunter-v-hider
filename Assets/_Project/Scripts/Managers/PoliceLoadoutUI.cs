using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using HunterVsHider.Player;

namespace HunterVsHider.Managers
{
    public class PoliceLoadoutUI : MonoBehaviour
    {
        public static PoliceLoadoutUI Instance { get; private set; }

        [Header("UI Root Container")]
        [Tooltip("Root canvas or container panel to toggle with the Tab key.")]
        public GameObject loadoutContainer;

        [Header("Weapon Selection Buttons")]
        public Button buttonTacticalRifle;
        public Button buttonOptic9mm;
        public Button buttonSubCompact45;

        [Header("Squad Sync Panel")]
        [Tooltip("Parent container holding the squad member text elements.")]
        public Transform squadSyncListContainer;
        [Tooltip("Text component to display real-time squad loadouts.")]
        public Text squadSyncSummaryText;

        [Header("Status & Notification")]
        public Text statusHeaderLabel;

        [Header("Visual Feedback Colors")]
        public Color normalButtonColor = new Color(0.18f, 0.22f, 0.3f, 1f);
        public Color selectedButtonColor = new Color(0.2f, 0.6f, 1.0f, 1f);

        private bool isOpen = false;

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

            if (loadoutContainer == null)
            {
                loadoutContainer = gameObject;
            }

            // Ensure disabled by default on start
            SetUIActive(false);

            // Hook up button listeners if assigned in inspector
            if (buttonTacticalRifle != null)
                buttonTacticalRifle.onClick.AddListener(() => OnSelectWeaponClicked(0));
            if (buttonOptic9mm != null)
                buttonOptic9mm.onClick.AddListener(() => OnSelectWeaponClicked(1));
            if (buttonSubCompact45 != null)
                buttonSubCompact45.onClick.AddListener(() => OnSelectWeaponClicked(2));
        }

        private void Update()
        {
            PlayerNetworkState localPlayer = GetLocalPlayerState();

            // Only Police players can open the Police Loadout UI
            if (localPlayer == null || localPlayer.Role != PlayerRole.Police)
            {
                if (isOpen) SetUIActive(false);
                return;
            }

            bool isPrepPhase = MatchManager.Instance != null && MatchManager.Instance.CurrentState == MatchState.PrepPhase;

            // Listen for 'Tab' key toggle during PrepPhase
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (isPrepPhase)
                {
                    SetUIActive(!isOpen);
                }
                else
                {
                    Debug.Log("[PoliceLoadoutUI] Loadout can only be accessed during the Prep Phase.");
                }
            }

            // If match state leaves PrepPhase, close automatically
            if (!isPrepPhase && isOpen)
            {
                SetUIActive(false);
            }

            // While open, keep squad status updated in real-time
            if (isOpen)
            {
                UpdateSquadSync();
                UpdateSelectionHighlights(localPlayer.SelectedWeaponID);
            }
        }

        public void SetUIActive(bool active)
        {
            isOpen = active;

            // If loadoutContainer is a separate panel inside Canvas, toggle it; otherwise toggle gameObject
            if (loadoutContainer != null && loadoutContainer != gameObject)
            {
                loadoutContainer.SetActive(active);
            }
            else
            {
                CanvasGroup cg = GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = active ? 1f : 0f;
                    cg.interactable = active;
                    cg.blocksRaycasts = active;
                }
                else
                {
                    // If root canvas is toggled
                    var canvas = GetComponent<Canvas>();
                    if (canvas != null) canvas.enabled = active;
                }
            }

            if (isOpen)
            {
                PlayerNetworkState localPlayer = GetLocalPlayerState();
                if (localPlayer != null)
                {
                    UpdateSelectionHighlights(localPlayer.SelectedWeaponID);
                }
                UpdateSquadSync();
            }
        }

        public void OnSelectWeaponClicked(int weaponID)
        {
            PlayerNetworkState localPlayer = GetLocalPlayerState();
            if (localPlayer != null && localPlayer.IsOwner)
            {
                localPlayer.CmdSelectWeapon(weaponID);
                UpdateSelectionHighlights(weaponID);
                UpdateSquadSync();
            }
        }

        private void UpdateSelectionHighlights(int selectedID)
        {
            SetButtonColor(buttonTacticalRifle, selectedID == 0);
            SetButtonColor(buttonOptic9mm, selectedID == 1);
            SetButtonColor(buttonSubCompact45, selectedID == 2);
        }

        private void SetButtonColor(Button btn, bool isSelected)
        {
            if (btn == null) return;
            var image = btn.GetComponent<Image>();
            if (image != null)
            {
                image.color = isSelected ? selectedButtonColor : normalButtonColor;
            }
        }

        /// <summary>
        /// Reads all connected Police players across the network and updates the Squad Sync Panel.
        /// </summary>
        public void UpdateSquadSync()
        {
            List<PlayerNetworkState> policeSquad = new List<PlayerNetworkState>();

            if (NetworkManager.Singleton != null)
            {
                foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                {
                    var pObj = clientPair.Value.PlayerObject;
                    if (pObj != null)
                    {
                        var pState = pObj.GetComponent<PlayerNetworkState>();
                        if (pState != null && pState.Role == PlayerRole.Police)
                        {
                            policeSquad.Add(pState);
                        }
                    }
                }
            }

            if (squadSyncSummaryText != null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < policeSquad.Count; i++)
                {
                    var officer = policeSquad[i];
                    string roleTag = officer.IsOwner ? " (You)" : "";
                    string clientTag = officer.OwnerClientId == 0 ? "Host" : $"Client {officer.OwnerClientId}";
                    string weaponName = GetWeaponName(officer.SelectedWeaponID);
                    string colorHex = GetWeaponColorHex(officer.SelectedWeaponID);

                    sb.AppendLine($"• <b>Officer {i + 1} [{clientTag}]{roleTag}</b>");
                    sb.AppendLine($"   └ Weapon: <color={colorHex}><b>{weaponName}</b></color>");
                    if (i < policeSquad.Count - 1) sb.AppendLine();
                }

                squadSyncSummaryText.text = sb.ToString();
            }
        }

        public static string GetWeaponName(int weaponID)
        {
            switch (weaponID)
            {
                case 0: return "Tactical Rifle";
                case 1: return "Optic-Ready 9mm";
                case 2: return "Sub-Compact .45";
                default: return "Unknown Weapon";
            }
        }

        public static string GetWeaponColorHex(int weaponID)
        {
            switch (weaponID)
            {
                case 0: return "#4DA6FF"; // Blue
                case 1: return "#4DFF79"; // Green
                case 2: return "#FF4D4D"; // Red
                default: return "#FFFFFF";
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
            // Interactive OnGUI fallback when Tab is pressed
            if (!isOpen) return;

            PlayerNetworkState localPlayer = GetLocalPlayerState();
            if (localPlayer == null || localPlayer.Role != PlayerRole.Police) return;

            int panelW = 580;
            int panelH = 340;
            int x = (Screen.width - panelW) / 2;
            int y = (Screen.height - panelH) / 2;

            GUI.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            GUILayout.BeginArea(new Rect(x, y, panelW, panelH), GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=18><b>POLICE ARMORY - PREP PHASE LOADOUT</b></size>");
            if (GUILayout.Button("<b>[X] Close (TAB)</b>", GUILayout.Width(110), GUILayout.Height(26)))
            {
                SetUIActive(false);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("<color=#AAAAAA>Select your weapon profile. Your loadout synchronizes in real-time with your squad.</color>");
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            // LEFT PANEL: Selection Panel
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(270));
            GUILayout.Label("<b>--- WEAPON PROFILES ---</b>");
            GUILayout.Space(5);

            int currentSelected = localPlayer.SelectedWeaponID;

            // Option 0: Tactical Rifle
            GUI.backgroundColor = (currentSelected == 0) ? new Color(0.2f, 0.6f, 1.0f) : Color.white;
            if (GUILayout.Button($"<b>Tactical Rifle [0]</b>\n<size=10>Range 50m | DMG 25 | Full-Auto (Blue)</size>", GUILayout.Height(46)))
            {
                OnSelectWeaponClicked(0);
            }

            GUILayout.Space(4);

            // Option 1: Optic-Ready 9mm
            GUI.backgroundColor = (currentSelected == 1) ? new Color(0.2f, 0.9f, 0.3f) : Color.white;
            if (GUILayout.Button($"<b>Optic-Ready 9mm [1]</b>\n<size=10>Range 35m | DMG 20 | Semi-Auto (Green)</size>", GUILayout.Height(46)))
            {
                OnSelectWeaponClicked(1);
            }

            GUILayout.Space(4);

            // Option 2: Sub-Compact .45
            GUI.backgroundColor = (currentSelected == 2) ? new Color(1.0f, 0.3f, 0.3f) : Color.white;
            if (GUILayout.Button($"<b>Sub-Compact .45 [2]</b>\n<size=10>Range 20m | DMG 35 | High-Impact (Red)</size>", GUILayout.Height(46)))
            {
                OnSelectWeaponClicked(2);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // RIGHT PANEL: Squad Sync Panel
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(270));
            GUILayout.Label("<b>--- SQUAD LOADOUT SYNC ---</b>");
            GUILayout.Space(5);

            if (NetworkManager.Singleton != null)
            {
                int officerNum = 1;
                foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                {
                    var pObj = clientPair.Value.PlayerObject;
                    if (pObj == null) continue;
                    var pState = pObj.GetComponent<PlayerNetworkState>();
                    if (pState == null || pState.Role != PlayerRole.Police) continue;

                    string youTag = pState.IsOwner ? " <color=yellow>(You)</color>" : "";
                    string clientTag = pState.OwnerClientId == 0 ? "Host" : $"Client {pState.OwnerClientId}";
                    string wName = GetWeaponName(pState.SelectedWeaponID);
                    string colorHex = GetWeaponColorHex(pState.SelectedWeaponID);

                    GUILayout.Label($"<b>Officer {officerNum} [{clientTag}]{youTag}</b>\n   └ <color={colorHex}><b>{wName}</b></color>");
                    officerNum++;
                }
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("<color=#7799BB><size=11>Press [TAB] at any time during Prep Phase to toggle this Armory menu.</size></color>");

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
