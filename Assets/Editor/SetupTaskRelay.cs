using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using HunterVsHider.Managers;

namespace HunterVsHider.Editor
{
    public class SetupTaskRelay
    {
        [MenuItem("Tools/Hunter v Hider/Setup Relay Infrastructure")]
        public static void ExecuteSetup()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;
            if (Application.isPlaying) return;

            Debug.Log("[SetupTaskRelay] Starting Relay Infrastructure & Two-Panel Lobby UI Setup...");
            bool anyChanged = false;

            // 1. Open Scene Tactical_Main
            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[SetupTaskRelay] Could not open scene {scenePath}!");
                return;
            }

            // 2. Ensure EventSystem exists
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                eventSystem = esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
                anyChanged = true;
                Debug.Log("[SetupTaskRelay] Created EventSystem in Tactical_Main.");
            }

            // 3. Ensure RelayManager exists in scene (attached to NetworkManager)
            GameObject netManagerObj = GameObject.Find("NetworkManager");
            if (netManagerObj != null)
            {
                RelayManager relayManager = netManagerObj.GetComponent<RelayManager>();
                if (relayManager == null)
                {
                    relayManager = netManagerObj.AddComponent<RelayManager>();
                    anyChanged = true;
                    Debug.Log("[SetupTaskRelay] Added RelayManager to NetworkManager GameObject.");
                }
            }
            else
            {
                GameObject relayObj = GameObject.Find("RelayManager");
                if (relayObj == null)
                {
                    relayObj = new GameObject("RelayManager");
                    relayObj.AddComponent<RelayManager>();
                    anyChanged = true;
                    Debug.Log("[SetupTaskRelay] Created dedicated RelayManager GameObject.");
                }
            }

            // 4. Setup RelayLobbyUI Canvas
            GameObject lobbyCanvasObj = GameObject.Find("RelayLobbyUI");
            if (lobbyCanvasObj == null)
            {
                lobbyCanvasObj = new GameObject("RelayLobbyUI");
                anyChanged = true;
                Debug.Log("[SetupTaskRelay] Created RelayLobbyUI GameObject in Tactical_Main.");
            }

            // Ensure CameraFollow and PlayerCamera components exist on Main Camera
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                if (mainCam.GetComponent<HunterVsHider.Cameras.CameraFollow>() == null)
                {
                    mainCam.gameObject.AddComponent<HunterVsHider.Cameras.CameraFollow>();
                    anyChanged = true;
                }
                if (mainCam.GetComponent<HunterVsHider.Cameras.PlayerCamera>() == null)
                {
                    mainCam.gameObject.AddComponent<HunterVsHider.Cameras.PlayerCamera>();
                    anyChanged = true;
                }
            }

            Canvas canvas = lobbyCanvasObj.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = lobbyCanvasObj.AddComponent<Canvas>();
                anyChanged = true;
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = lobbyCanvasObj.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = lobbyCanvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                anyChanged = true;
            }

            GraphicRaycaster raycaster = lobbyCanvasObj.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = lobbyCanvasObj.AddComponent<GraphicRaycaster>();
                anyChanged = true;
            }

            RelayLobbyUI lobbyUI = lobbyCanvasObj.GetComponent<RelayLobbyUI>();
            if (lobbyUI == null)
            {
                lobbyUI = lobbyCanvasObj.AddComponent<RelayLobbyUI>();
                anyChanged = true;
            }

            // Clean up old container if legacy setup exists
            Transform oldLegacyContainer = lobbyCanvasObj.transform.Find("LobbyContainer");
            if (oldLegacyContainer != null)
            {
                Object.DestroyImmediate(oldLegacyContainer.gameObject);
                anyChanged = true;
            }

            // Standard Font reference
            Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (standardFont == null)
            {
                standardFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            // ==========================================
            // 5. MAIN MENU PANEL
            // ==========================================
            GameObject mainMenuObj = GetOrCreateChild(lobbyCanvasObj, "MainMenuPanel");
            RectTransform mainMenuRt = mainMenuObj.GetComponent<RectTransform>();
            if (mainMenuRt == null) mainMenuRt = mainMenuObj.AddComponent<RectTransform>();
            mainMenuRt.anchorMin = new Vector2(0.5f, 0.5f);
            mainMenuRt.anchorMax = new Vector2(0.5f, 0.5f);
            mainMenuRt.pivot = new Vector2(0.5f, 0.5f);
            mainMenuRt.sizeDelta = new Vector2(560, 460);
            mainMenuRt.anchoredPosition = Vector2.zero;

            Image mainBg = mainMenuObj.GetComponent<Image>();
            if (mainBg == null) mainBg = mainMenuObj.AddComponent<Image>();
            mainBg.color = new Color(0.08f, 0.10f, 0.14f, 0.95f);

            // Title
            GameObject titleObj = GetOrCreateChild(mainMenuObj, "TitleText");
            SetupText(titleObj, standardFont, "HUNTER v HIDER", 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.3f, 0.75f, 1.0f));
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(500, 36);
            titleRt.anchoredPosition = new Vector2(0, -20);

            // Status Text
            GameObject statusObj = GetOrCreateChild(mainMenuObj, "StatusText");
            Text statusText = SetupText(statusObj, standardFont, "Ready.", 14, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(1.0f, 0.85f, 0.3f));
            RectTransform statusRt = statusObj.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.5f, 1f);
            statusRt.anchorMax = new Vector2(0.5f, 1f);
            statusRt.pivot = new Vector2(0.5f, 1f);
            statusRt.sizeDelta = new Vector2(520, 28);
            statusRt.anchoredPosition = new Vector2(0, -56);

            // Host Button (Relay)
            GameObject hostBtnObj = GetOrCreateChild(mainMenuObj, "Button_HostGame");
            Button hostBtn = SetupButton(hostBtnObj, standardFont, "Host Game (Relay)", 16, new Color(0.15f, 0.40f, 0.75f), Color.white);
            RectTransform hostBtnRt = hostBtnObj.GetComponent<RectTransform>();
            hostBtnRt.anchorMin = new Vector2(0.5f, 1f);
            hostBtnRt.anchorMax = new Vector2(0.5f, 1f);
            hostBtnRt.pivot = new Vector2(0.5f, 1f);
            hostBtnRt.sizeDelta = new Vector2(460, 42);
            hostBtnRt.anchoredPosition = new Vector2(0, -92);

            // Input Field
            GameObject inputFieldObj = GetOrCreateChild(mainMenuObj, "InputField_JoinCode");
            InputField joinInput = SetupInputField(inputFieldObj, standardFont, "ENTER 6-CHAR CODE");
            RectTransform inputRt = inputFieldObj.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.5f, 1f);
            inputRt.anchorMax = new Vector2(0.5f, 1f);
            inputRt.pivot = new Vector2(0.5f, 1f);
            inputRt.sizeDelta = new Vector2(460, 42);
            inputRt.anchoredPosition = new Vector2(0, -142);

            // Join Button (Relay)
            GameObject joinBtnObj = GetOrCreateChild(mainMenuObj, "Button_JoinGame");
            Button joinBtn = SetupButton(joinBtnObj, standardFont, "Join Game (Relay)", 16, new Color(0.16f, 0.52f, 0.32f), Color.white);
            RectTransform joinBtnRt = joinBtnObj.GetComponent<RectTransform>();
            joinBtnRt.anchorMin = new Vector2(0.5f, 1f);
            joinBtnRt.anchorMax = new Vector2(0.5f, 1f);
            joinBtnRt.pivot = new Vector2(0.5f, 1f);
            joinBtnRt.sizeDelta = new Vector2(460, 42);
            joinBtnRt.anchoredPosition = new Vector2(0, -192);

            // Divider
            GameObject divObj = GetOrCreateChild(mainMenuObj, "Divider");
            RectTransform divRt = divObj.GetComponent<RectTransform>();
            if (divRt == null) divRt = divObj.AddComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0.5f, 1f);
            divRt.anchorMax = new Vector2(0.5f, 1f);
            divRt.pivot = new Vector2(0.5f, 1f);
            divRt.sizeDelta = new Vector2(460, 2);
            divRt.anchoredPosition = new Vector2(0, -244);
            Image divImg = divObj.GetComponent<Image>();
            if (divImg == null) divImg = divObj.AddComponent<Image>();
            divImg.color = new Color(0.2f, 0.25f, 0.32f, 0.7f);

            // Host LAN Button
            GameObject hostLanBtnObj = GetOrCreateChild(mainMenuObj, "Button_HostLan");
            Button hostLanBtn = SetupButton(hostLanBtnObj, standardFont, "Host Game (Local LAN)", 16, new Color(0.24f, 0.32f, 0.60f), Color.white);
            RectTransform hostLanBtnRt = hostLanBtnObj.GetComponent<RectTransform>();
            hostLanBtnRt.anchorMin = new Vector2(0.5f, 1f);
            hostLanBtnRt.anchorMax = new Vector2(0.5f, 1f);
            hostLanBtnRt.pivot = new Vector2(0.5f, 1f);
            hostLanBtnRt.sizeDelta = new Vector2(460, 42);
            hostLanBtnRt.anchoredPosition = new Vector2(0, -260);

            // Join LAN Button
            GameObject joinLanBtnObj = GetOrCreateChild(mainMenuObj, "Button_JoinLan");
            Button joinLanBtn = SetupButton(joinLanBtnObj, standardFont, "Join Game (Local LAN)", 16, new Color(0.18f, 0.48f, 0.48f), Color.white);
            RectTransform joinLanBtnRt = joinLanBtnObj.GetComponent<RectTransform>();
            joinLanBtnRt.anchorMin = new Vector2(0.5f, 1f);
            joinLanBtnRt.anchorMax = new Vector2(0.5f, 1f);
            joinLanBtnRt.pivot = new Vector2(0.5f, 1f);
            joinLanBtnRt.sizeDelta = new Vector2(460, 42);
            joinLanBtnRt.anchoredPosition = new Vector2(0, -310);

            // ==========================================
            // 6. WAITING ROOM PANEL (Disabled by default)
            // ==========================================
            GameObject waitingObj = GetOrCreateChild(lobbyCanvasObj, "WaitingRoomPanel");
            RectTransform waitingRt = waitingObj.GetComponent<RectTransform>();
            if (waitingRt == null) waitingRt = waitingObj.AddComponent<RectTransform>();
            waitingRt.anchorMin = new Vector2(1f, 0.5f);
            waitingRt.anchorMax = new Vector2(1f, 0.5f);
            waitingRt.pivot = new Vector2(1f, 0.5f);
            waitingRt.sizeDelta = new Vector2(620, 540);
            waitingRt.anchoredPosition = new Vector2(-20f, 0f);

            Image waitBg = waitingObj.GetComponent<Image>();
            if (waitBg == null) waitBg = waitingObj.AddComponent<Image>();
            waitBg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

            // Waiting Room Title
            GameObject waitTitleObj = GetOrCreateChild(waitingObj, "WaitingTitle");
            SetupTMPText(waitTitleObj, "LOBBY WAITING ROOM", 24, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.3f, 0.75f, 1.0f));
            RectTransform waitTitleRt = waitTitleObj.GetComponent<RectTransform>();
            waitTitleRt.anchorMin = new Vector2(0.5f, 1f);
            waitTitleRt.anchorMax = new Vector2(0.5f, 1f);
            waitTitleRt.pivot = new Vector2(0.5f, 1f);
            waitTitleRt.sizeDelta = new Vector2(560, 35);
            waitTitleRt.anchoredPosition = new Vector2(0, -20);

            // Persistent Join Code Display Card
            GameObject codeCardObj = GetOrCreateChild(waitingObj, "JoinCodeCard");
            RectTransform codeCardRt = codeCardObj.GetComponent<RectTransform>();
            if (codeCardRt == null) codeCardRt = codeCardObj.AddComponent<RectTransform>();
            codeCardRt.anchorMin = new Vector2(0.5f, 1f);
            codeCardRt.anchorMax = new Vector2(0.5f, 1f);
            codeCardRt.pivot = new Vector2(0.5f, 1f);
            codeCardRt.sizeDelta = new Vector2(560, 80);
            codeCardRt.anchoredPosition = new Vector2(0, -65);
            Image codeCardBg = codeCardObj.GetComponent<Image>();
            if (codeCardBg == null) codeCardBg = codeCardObj.AddComponent<Image>();
            codeCardBg.color = new Color(0.03f, 0.04f, 0.07f, 0.9f);

            // Txt_PersistentJoinCode
            GameObject pCodeObj = GetOrCreateChild(codeCardObj, "Txt_PersistentJoinCode");
            TMP_Text pCodeText = SetupTMPText(pCodeObj, "LOBBY CODE: ------", 28, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.2f, 0.95f, 0.65f));
            RectTransform pCodeRt = pCodeObj.GetComponent<RectTransform>();
            pCodeRt.anchorMin = new Vector2(0f, 0f);
            pCodeRt.anchorMax = new Vector2(0.72f, 1f);
            pCodeRt.offsetMin = new Vector2(20, 0);
            pCodeRt.offsetMax = new Vector2(0, 0);

            // Copy Code Button
            GameObject copyPersistentBtnObj = GetOrCreateChild(codeCardObj, "Button_CopyCode");
            Button copyPersistentBtn = SetupButton(copyPersistentBtnObj, standardFont, "Copy Code", 14, new Color(0.25f, 0.32f, 0.42f), Color.white);
            RectTransform copyPersistentRt = copyPersistentBtnObj.GetComponent<RectTransform>();
            copyPersistentRt.anchorMin = new Vector2(0.74f, 0.2f);
            copyPersistentRt.anchorMax = new Vector2(0.96f, 0.8f);
            copyPersistentRt.offsetMin = Vector2.zero;
            copyPersistentRt.offsetMax = Vector2.zero;

            // Connected Players Box
            // Map Size Selector Card
            GameObject mapSizeCardObj = GetOrCreateChild(waitingObj, "MapSizeCard");
            RectTransform mapSizeCardRt = mapSizeCardObj.GetComponent<RectTransform>();
            if (mapSizeCardRt == null) mapSizeCardRt = mapSizeCardObj.AddComponent<RectTransform>();
            mapSizeCardRt.anchorMin = new Vector2(0.5f, 1f);
            mapSizeCardRt.anchorMax = new Vector2(0.5f, 1f);
            mapSizeCardRt.pivot = new Vector2(0.5f, 1f);
            mapSizeCardRt.sizeDelta = new Vector2(560, 50);
            mapSizeCardRt.anchoredPosition = new Vector2(0, -155);
            Image mapSizeBg = mapSizeCardObj.GetComponent<Image>();
            if (mapSizeBg == null) mapSizeBg = mapSizeCardObj.AddComponent<Image>();
            mapSizeBg.color = new Color(0.04f, 0.05f, 0.08f, 0.9f);

            // Label
            GameObject mapSizeLabelObj = GetOrCreateChild(mapSizeCardObj, "Label_MapSize");
            SetupTMPText(mapSizeLabelObj, "MAP SIZE:", 15, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.65f, 0.80f, 1.0f));
            RectTransform mapSizeLabelRt = mapSizeLabelObj.GetComponent<RectTransform>();
            mapSizeLabelRt.anchorMin = new Vector2(0f, 0f);
            mapSizeLabelRt.anchorMax = new Vector2(0.45f, 1f);
            mapSizeLabelRt.offsetMin = new Vector2(16, 0);
            mapSizeLabelRt.offsetMax = new Vector2(0, 0);

            // Dropdown (Native TMP_Dropdown with valid Template hierarchy)
            TMP_Dropdown mapSizeDropdown = SetupNativeTMPDropdown(mapSizeCardObj.transform, "Dropdown_MapSize", new System.Collections.Generic.List<string> { "50", "100", "250" });

            // Connected Players Box
            GameObject playersCardObj = GetOrCreateChild(waitingObj, "ConnectedPlayersBox");
            RectTransform playersCardRt = playersCardObj.GetComponent<RectTransform>();
            if (playersCardRt == null) playersCardRt = playersCardObj.AddComponent<RectTransform>();
            playersCardRt.anchorMin = new Vector2(0.5f, 1f);
            playersCardRt.anchorMax = new Vector2(0.5f, 1f);
            playersCardRt.pivot = new Vector2(0.5f, 1f);
            playersCardRt.sizeDelta = new Vector2(560, 130);
            playersCardRt.anchoredPosition = new Vector2(0, -215);
            Image playersCardBg = playersCardObj.GetComponent<Image>();
            if (playersCardBg == null) playersCardBg = playersCardObj.AddComponent<Image>();
            playersCardBg.color = new Color(0.04f, 0.05f, 0.08f, 0.85f);

            // Txt_ConnectedPlayers
            GameObject pListObj = GetOrCreateChild(playersCardObj, "Txt_ConnectedPlayers");
            TMP_Text pListText = SetupTMPText(pListObj, "<b>Connected Players (1):</b>\n • Player 0 [Host] (You)", 16, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.88f, 0.92f, 0.96f));
            RectTransform pListRt = pListObj.GetComponent<RectTransform>();
            pListRt.anchorMin = Vector2.zero;
            pListRt.anchorMax = Vector2.one;
            pListRt.offsetMin = new Vector2(16, 12);
            pListRt.offsetMax = new Vector2(-16, -12);

            // Waiting Status Text
            GameObject waitStatusObj = GetOrCreateChild(waitingObj, "Txt_WaitingStatus");
            TMP_Text waitStatusText = SetupTMPText(waitStatusObj, "Gather players in Zone_Lobby. Click 'Start Match' when ready.", 14, FontStyles.Italic, TextAlignmentOptions.Center, new Color(1.0f, 0.85f, 0.35f));
            RectTransform waitStatusRt = waitStatusObj.GetComponent<RectTransform>();
            waitStatusRt.anchorMin = new Vector2(0.5f, 1f);
            waitStatusRt.anchorMax = new Vector2(0.5f, 1f);
            waitStatusRt.pivot = new Vector2(0.5f, 1f);
            waitStatusRt.sizeDelta = new Vector2(560, 35);
            waitStatusRt.anchoredPosition = new Vector2(0, -355);

            // Start Match Button
            GameObject startMatchBtnObj = GetOrCreateChild(waitingObj, "Button_StartMatch");
            Button startMatchBtn = SetupButton(startMatchBtnObj, standardFont, "Start Match", 20, new Color(0.18f, 0.55f, 0.90f), Color.white);
            RectTransform startMatchRt = startMatchBtnObj.GetComponent<RectTransform>();
            startMatchRt.anchorMin = new Vector2(0.5f, 1f);
            startMatchRt.anchorMax = new Vector2(0.5f, 1f);
            startMatchRt.pivot = new Vector2(0.5f, 1f);
            startMatchRt.sizeDelta = new Vector2(560, 56);
            startMatchRt.anchoredPosition = new Vector2(0, -400);

            // 7. Wire References into RelayLobbyUI
            lobbyUI.mainMenuPanel = mainMenuObj;
            lobbyUI.waitingRoomPanel = waitingObj;

            lobbyUI.buttonHostGame = hostBtn;
            lobbyUI.inputJoinCode = joinInput;
            lobbyUI.buttonJoinGame = joinBtn;
            lobbyUI.buttonHostLan = hostLanBtn;
            lobbyUI.buttonJoinLan = joinLanBtn;
            lobbyUI.textStatus = statusText;

            lobbyUI.txtPersistentJoinCode = pCodeText;
            lobbyUI.txtConnectedPlayers = pListText;
            lobbyUI.dropdownMapSize = mapSizeDropdown;
            lobbyUI.buttonStartMatch = startMatchBtn;
            lobbyUI.buttonCopyPersistentCode = copyPersistentBtn;
            lobbyUI.txtWaitingStatus = waitStatusText;

            // Ensure WaitingRoomPanel is disabled by default in scene
            mainMenuObj.SetActive(true);
            waitingObj.SetActive(false);

            EditorUtility.SetDirty(lobbyCanvasObj);
            EditorUtility.SetDirty(lobbyUI);

            // 8. Save Scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            anyChanged = true;

            if (anyChanged)
            {
                Debug.Log("[SetupTaskRelay] Two-Panel Relay Lobby UI setup completed & Tactical_Main saved successfully!");
            }
            EditorApplication.delayCall -= ExecuteSetup;
        }

        private static GameObject GetOrCreateChild(GameObject parent, string name)
        {
            Transform child = parent.transform.Find(name);
            if (child != null) return child.gameObject;

            GameObject newObj = new GameObject(name);
            newObj.transform.SetParent(parent.transform, false);
            return newObj;
        }

        private static Text SetupText(GameObject obj, Font font, string content, int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            Text txt = obj.GetComponent<Text>();
            if (txt == null) txt = obj.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = alignment;
            txt.color = color;
            return txt;
        }

        private static TMP_Text SetupTMPText(GameObject obj, string content, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            return tmp;
        }

        private static Button SetupButton(GameObject obj, Font font, string label, int fontSize, Color btnColor, Color textColor)
        {
            Image img = obj.GetComponent<Image>();
            if (img == null) img = obj.AddComponent<Image>();
            img.color = btnColor;

            Button btn = obj.GetComponent<Button>();
            if (btn == null) btn = obj.AddComponent<Button>();

            ColorBlock cb = btn.colors;
            cb.normalColor = btnColor;
            cb.highlightedColor = btnColor * 1.2f;
            cb.pressedColor = btnColor * 0.8f;
            cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            btn.colors = cb;

            GameObject textObj = GetOrCreateChild(obj, "Text");
            Text txt = SetupText(textObj, font, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
            RectTransform txtRt = textObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            return btn;
        }

        private static InputField SetupInputField(GameObject obj, Font font, string placeholderText)
        {
            Image bg = obj.GetComponent<Image>();
            if (bg == null) bg = obj.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.9f);

            InputField input = obj.GetComponent<InputField>();
            if (input == null) input = obj.AddComponent<InputField>();

            GameObject placeholderObj = GetOrCreateChild(obj, "Placeholder");
            Text phText = SetupText(placeholderObj, font, placeholderText, 18, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.45f, 0.50f, 0.55f));
            RectTransform phRt = placeholderObj.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(10, 0);
            phRt.offsetMax = new Vector2(-10, 0);

            GameObject textObj = GetOrCreateChild(obj, "Text");
            Text mainText = SetupText(textObj, font, "", 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            RectTransform txtRt = textObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(10, 0);
            txtRt.offsetMax = new Vector2(-10, 0);

            input.textComponent = mainText;
            input.placeholder = phText;
            input.characterLimit = 12;
            input.contentType = InputField.ContentType.Alphanumeric;

            return input;
        }

        private static TMP_Dropdown SetupNativeTMPDropdown(Transform parent, string name, System.Collections.Generic.List<string> options)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject dropdownObj = TMPro.TMP_DefaultControls.CreateDropdown(new TMPro.TMP_DefaultControls.Resources());
            dropdownObj.name = name;
            dropdownObj.transform.SetParent(parent, false);

            RectTransform dropdownRt = dropdownObj.GetComponent<RectTransform>();
            dropdownRt.anchorMin = new Vector2(0.50f, 0.10f);
            dropdownRt.anchorMax = new Vector2(0.96f, 0.90f);
            dropdownRt.offsetMin = Vector2.zero;
            dropdownRt.offsetMax = Vector2.zero;

            Image bg = dropdownObj.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.10f, 0.14f, 0.20f, 1f);

            TMP_Dropdown dropdown = dropdownObj.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            // 1. Style caption text
            if (dropdown.captionText != null)
            {
                dropdown.captionText.fontSize = 16;
                dropdown.captionText.fontStyle = FontStyles.Bold;
                dropdown.captionText.alignment = TextAlignmentOptions.Center;
                dropdown.captionText.color = Color.white;
            }

            // 2. Style Template Background (#111620 - Dark Slate / Navy)
            if (dropdown.template != null)
            {
                Image templateBg = dropdown.template.GetComponent<Image>();
                if (templateBg != null) templateBg.color = new Color(0.067f, 0.086f, 0.125f, 0.98f);

                // 3. Style Viewport -> Content -> Item hierarchy
                Transform itemTrans = dropdown.template.Find("Viewport/Content/Item");
                if (itemTrans != null)
                {
                    // Item Background (#1A2230 - Dark Blue / Grey hover state)
                    Transform itemBgTrans = itemTrans.Find("Item Background");
                    if (itemBgTrans != null)
                    {
                        Image itemBgImg = itemBgTrans.GetComponent<Image>();
                        if (itemBgImg != null) itemBgImg.color = new Color(0.102f, 0.133f, 0.188f, 1f);
                    }

                    // Toggle button color block
                    Toggle itemToggle = itemTrans.GetComponent<Toggle>();
                    if (itemToggle != null)
                    {
                        ColorBlock cb = itemToggle.colors;
                        cb.normalColor = new Color(0.067f, 0.086f, 0.125f, 1f);
                        cb.highlightedColor = new Color(0.14f, 0.19f, 0.28f, 1f);
                        cb.pressedColor = new Color(0.20f, 0.28f, 0.40f, 1f);
                        cb.selectedColor = new Color(0.12f, 0.16f, 0.24f, 1f);
                        itemToggle.colors = cb;
                    }

                    // Item Checkmark (Bright Green #33F5A6)
                    Transform itemCheckTrans = itemTrans.Find("Item Checkmark");
                    if (itemCheckTrans != null)
                    {
                        Image itemCheckImg = itemCheckTrans.GetComponent<Image>();
                        if (itemCheckImg != null) itemCheckImg.color = new Color(0.2f, 0.95f, 0.65f, 1f);
                    }

                    // Item Label (Crisp White Text)
                    Transform itemLabelTrans = itemTrans.Find("Item Label");
                    if (itemLabelTrans != null)
                    {
                        TMP_Text itemLabel = itemLabelTrans.GetComponent<TMP_Text>();
                        if (itemLabel != null)
                        {
                            itemLabel.fontSize = 15;
                            itemLabel.fontStyle = FontStyles.Bold;
                            itemLabel.color = Color.white;
                        }
                    }
                }

                // 4. Style Scrollbar (Transparent background, Dark Grey handle)
                Transform scrollbarTrans = dropdown.template.Find("Scrollbar");
                if (scrollbarTrans != null)
                {
                    Image scrollbarBg = scrollbarTrans.GetComponent<Image>();
                    if (scrollbarBg != null) scrollbarBg.color = new Color(0f, 0f, 0f, 0f);

                    Transform handleTrans = scrollbarTrans.Find("Sliding Area/Handle");
                    if (handleTrans != null)
                    {
                        Image handleImg = handleTrans.GetComponent<Image>();
                        if (handleImg != null) handleImg.color = new Color(0.25f, 0.32f, 0.42f, 0.8f);
                    }
                }

                // Ensure Template GameObject is disabled by default (enabled at runtime by TMP_Dropdown)
                dropdown.template.gameObject.SetActive(false);
            }

            return dropdown;
        }

        [MenuItem("Tools/Hunter v Hider/Build Windows Standalone")]
        public static void BuildWindowsStandalone()
        {
            string buildPath = "Builds/HunterVsHider.exe";
            string[] scenes = new[] { "Assets/_Project/Scenes/Tactical_Main.unity" };

            Debug.Log($"[SetupTaskRelay] Starting Standalone Windows Build -> {buildPath}");
            
            System.IO.Directory.CreateDirectory("Builds");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[SetupTaskRelay] Build succeeded! File size: {report.summary.totalSize} bytes");
            }
            else
            {
                Debug.LogError($"[SetupTaskRelay] Build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }
        }
    }
}
