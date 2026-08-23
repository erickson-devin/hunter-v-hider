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
        public Button buttonGodTierArsenal;

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

            if (buttonGodTierArsenal != null)
                buttonGodTierArsenal.onClick.AddListener(() => OnSelectGodTierArsenalClicked());
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
            }
        }

        public void SetUIActive(bool active)
        {
            isOpen = active;

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
                    var canvas = GetComponent<Canvas>();
                    if (canvas != null) canvas.enabled = active;
                }
            }

            if (isOpen)
            {
                UpdateSquadSync();
            }
        }

        public void OnSelectGodTierArsenalClicked()
        {
            PlayerNetworkState localPlayer = GetLocalPlayerState();
            if (localPlayer != null && localPlayer.IsOwner)
            {
                var pwm = localPlayer.GetComponent<PlayerWeaponManager>();
                if (pwm != null)
                {
                    pwm.SetupRoleLoadout(PlayerRole.Police);
                }
                localPlayer.CmdSelectWeapon(0);
                UpdateSquadSync();
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

                    sb.AppendLine($"• <b>Officer {i + 1} [{clientTag}]{roleTag}</b>");
                    sb.AppendLine("   └ Arsenal: <color=#4DA6FF><b>God-Tier 3-Weapon Kit</b></color>");
                    sb.AppendLine("      [1] Rifle (45°/24m) | [2] Shotgun (110°/10m) | [3] Pistol (75°/16m)");
                    if (i < policeSquad.Count - 1) sb.AppendLine();
                }

                squadSyncSummaryText.text = sb.ToString();
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

            int panelW = 620;
            int panelH = 370;
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

            GUILayout.Label("<color=#AAAAAA>All officers are outfitted with the complete 3-Weapon Tactical Arsenal with Infinite Reserves.</color>");
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();

            // LEFT PANEL: Selection Panel
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300));
            GUILayout.Label("<b>--- ACTIVE POLICE ARSENAL ---</b>");
            GUILayout.Space(5);

            GUI.backgroundColor = new Color(0.2f, 0.6f, 1.0f);
            if (GUILayout.Button("<b>[★] GOD-TIER 3-WEAPON ARSENAL</b>\n<size=10>Full Loadout Enabled (Infinite Reserves)</size>", GUILayout.Height(42)))
            {
                OnSelectGodTierArsenalClicked();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(5);
            GUILayout.Label("<b>[Key 1] Tactical Rifle</b>\n<size=10><color=#4DA6FF>45° Narrow Beam | 24m Range | 30 Rds</color></size>");
            GUILayout.Label("<b>[Key 2] Combat Shotgun</b>\n<size=10><color=#4DFF79>110° Floodlight | 10m CQC | 8 Pellets | 8 Rds</color></size>");
            GUILayout.Label("<b>[Key 3] Pistol</b>\n<size=10><color=#FF4D4D>75° Tactical Cone | 16m Beam | 15 Rds</color></size>");

            GUILayout.EndVertical();

            GUILayout.Space(10);

            // RIGHT PANEL: Squad Sync Panel
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(280));
            GUILayout.Label("<b>--- SQUAD ARSENAL SYNC ---</b>");
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

                    GUILayout.Label($"<b>Officer {officerNum} [{clientTag}]{youTag}</b>\n   └ <color=#4DA6FF><b>God-Tier 3-Weapon Kit</b></color>\n   <size=10>Rifle + Shotgun + Pistol (Inf Reserves)</size>");
                    officerNum++;
                }
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("<color=#7799BB><size=11>Press [TAB] at any time during Prep Phase to toggle this Armory menu.</size></color>");

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
