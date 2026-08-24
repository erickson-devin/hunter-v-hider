using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using HunterVsHider.UI;

namespace HunterVsHider.Editor
{
    public class SetupTaskMainMenuUI
    {
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (ParrelSync.ClonesManager.IsClone()) return;
                if (Application.isPlaying) return;
                ExecuteSetup();
            };
        }

        [MenuItem("Tools/Hunter v Hider/Setup Main Menu UI")]
        public static void ExecuteSetup()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;
            if (Application.isPlaying) return;

            Debug.Log("[SetupTaskMainMenuUI] Starting Streamlined Main Menu UI Setup with Background Image...");

            // 1. Ensure Texture Import Settings for MainMenuBackground
            string bgJpgPath = "Assets/_Project/Textures/UI/MainMenuBackground.jpg";
            string bgPngPath = "Assets/_Project/Textures/UI/MainMenuBackground.png";
            string activeBgPath = System.IO.File.Exists(bgJpgPath) ? bgJpgPath : (System.IO.File.Exists(bgPngPath) ? bgPngPath : null);

            if (activeBgPath != null)
            {
                TextureImporter importer = AssetImporter.GetAtPath(activeBgPath) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        changed = true;
                    }
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }
                    if (importer.filterMode != FilterMode.Bilinear)
                    {
                        importer.filterMode = FilterMode.Bilinear;
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                        Debug.Log($"[SetupTaskMainMenuUI] Configured Sprite import settings on {activeBgPath}");
                    }
                }
            }

            // 2. Setup Scenes
            SetupSceneCanvas("Assets/_Project/Scenes/Tactical_Main.unity", activeBgPath);

            string mainMenuScenePath = "Assets/_Project/Scenes/Scene_MainMenu.unity";
            if (System.IO.File.Exists(mainMenuScenePath))
            {
                SetupSceneCanvas(mainMenuScenePath, activeBgPath);
            }
        }

        private static void SetupSceneCanvas(string scenePath, string bgPath)
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogWarning($"[SetupTaskMainMenuUI] Scene {scenePath} not valid or not found.");
                return;
            }

            // Ensure EventSystem exists
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                eventSystem = esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            // Ensure Canvas_MainMenu exists
            GameObject canvasObj = GameObject.Find("Canvas_MainMenu");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("Canvas_MainMenu");
            }

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasObj.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // Above general in-game canvases

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvasObj.AddComponent<GraphicRaycaster>();
            }

            MainMenuUI mainMenuUI = canvasObj.GetComponent<MainMenuUI>();
            if (mainMenuUI == null)
            {
                mainMenuUI = canvasObj.AddComponent<MainMenuUI>();
            }
            mainMenuUI.canvasMainMenu = canvasObj;

            // Background_Image (Full-screen stretch)
            Transform bgTrans = canvasObj.transform.Find("Background_Image");
            GameObject bgObj = (bgTrans != null) ? bgTrans.gameObject : new GameObject("Background_Image");
            bgObj.transform.SetParent(canvasObj.transform, false);
            bgObj.transform.SetAsFirstSibling(); // Placed behind all UI panels

            RectTransform bgRT = bgObj.GetComponent<RectTransform>();
            if (bgRT == null) bgRT = bgObj.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            Image bgImage = bgObj.GetComponent<Image>();
            if (bgImage == null) bgImage = bgObj.AddComponent<Image>();
            bgImage.color = Color.white;

            if (!string.IsNullOrEmpty(bgPath))
            {
                Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);
                if (bgSprite != null)
                {
                    bgImage.sprite = bgSprite;
                    Debug.Log($"[SetupTaskMainMenuUI] Assigned {bgPath} to Background_Image in {scenePath}.");
                }
            }

            // Construct MainPanel
            Transform mainPanelTrans = canvasObj.transform.Find("MainPanel");
            GameObject mainPanelObj;
            if (mainPanelTrans == null)
            {
                mainPanelObj = new GameObject("MainPanel");
                mainPanelObj.transform.SetParent(canvasObj.transform, false);
            }
            else
            {
                mainPanelObj = mainPanelTrans.gameObject;
            }

            RectTransform mainPanelRT = mainPanelObj.GetComponent<RectTransform>();
            if (mainPanelRT == null) mainPanelRT = mainPanelObj.AddComponent<RectTransform>();
            mainPanelRT.anchorMin = new Vector2(0.5f, 0.5f);
            mainPanelRT.anchorMax = new Vector2(0.5f, 0.5f);
            mainPanelRT.pivot = new Vector2(0.5f, 0.5f);
            mainPanelRT.sizeDelta = new Vector2(500, 600);
            mainPanelRT.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup vlg = mainPanelObj.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = mainPanelObj.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title Text
            Transform titleTrans = mainPanelObj.transform.Find("Text_Title");
            GameObject titleObj = (titleTrans != null) ? titleTrans.gameObject : new GameObject("Text_Title");
            titleObj.transform.SetParent(mainPanelObj.transform, false);
            TextMeshProUGUI titleTMP = titleObj.GetComponent<TextMeshProUGUI>();
            if (titleTMP == null) titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "HUNTER V HIDER";
            titleTMP.fontSize = 48;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color = new Color(0.9f, 0.85f, 0.2f);
            RectTransform titleRT = titleObj.GetComponent<RectTransform>();
            titleRT.sizeDelta = new Vector2(450, 80);

            // 4 Main Buttons: HOST, JOIN, SETTINGS, QUIT
            Button hostBtn = CreateOrGetButton(mainPanelObj.transform, "Button_HostMatch", "HOST MATCH", new Color(0.15f, 0.35f, 0.7f));
            Button joinBtn = CreateOrGetButton(mainPanelObj.transform, "Button_JoinMatch", "JOIN MATCH", new Color(0.2f, 0.55f, 0.3f));
            Button settingsBtn = CreateOrGetButton(mainPanelObj.transform, "Button_Settings", "SETTINGS", new Color(0.3f, 0.3f, 0.35f));
            Button quitBtn = CreateOrGetButton(mainPanelObj.transform, "Button_Quit", "QUIT", new Color(0.6f, 0.2f, 0.2f));

            mainMenuUI.mainPanel = mainPanelObj;
            mainMenuUI.hostButton = hostBtn;
            mainMenuUI.joinButton = joinBtn;
            mainMenuUI.settingsButton = settingsBtn;
            mainMenuUI.quitButton = quitBtn;

            // Construct NetworkTypeModal
            Transform modalTrans = canvasObj.transform.Find("NetworkTypeModal");
            GameObject modalObj;
            if (modalTrans == null)
            {
                modalObj = new GameObject("NetworkTypeModal");
                modalObj.transform.SetParent(canvasObj.transform, false);
            }
            else
            {
                modalObj = modalTrans.gameObject;
            }

            RectTransform modalRT = modalObj.GetComponent<RectTransform>();
            if (modalRT == null) modalRT = modalObj.AddComponent<RectTransform>();
            modalRT.anchorMin = Vector2.zero;
            modalRT.anchorMax = Vector2.one;
            modalRT.sizeDelta = Vector2.zero;
            modalRT.anchoredPosition = Vector2.zero;

            Image backdropImg = modalObj.GetComponent<Image>();
            if (backdropImg == null) backdropImg = modalObj.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.85f);

            // Centered Modal Container
            Transform containerTrans = modalObj.transform.Find("ModalContainer");
            GameObject containerObj = (containerTrans != null) ? containerTrans.gameObject : new GameObject("ModalContainer");
            containerObj.transform.SetParent(modalObj.transform, false);

            RectTransform containerRT = containerObj.GetComponent<RectTransform>();
            if (containerRT == null) containerRT = containerObj.AddComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.5f, 0.5f);
            containerRT.anchorMax = new Vector2(0.5f, 0.5f);
            containerRT.pivot = new Vector2(0.5f, 0.5f);
            containerRT.sizeDelta = new Vector2(450, 400);
            containerRT.anchoredPosition = Vector2.zero;

            Image containerImg = containerObj.GetComponent<Image>();
            if (containerImg == null) containerImg = containerObj.AddComponent<Image>();
            containerImg.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

            VerticalLayoutGroup modalVlg = containerObj.GetComponent<VerticalLayoutGroup>();
            if (modalVlg == null) modalVlg = containerObj.AddComponent<VerticalLayoutGroup>();
            modalVlg.childAlignment = TextAnchor.MiddleCenter;
            modalVlg.spacing = 18f;
            modalVlg.padding = new RectOffset(30, 30, 30, 30);
            modalVlg.childControlWidth = true;
            modalVlg.childControlHeight = false;
            modalVlg.childForceExpandWidth = true;
            modalVlg.childForceExpandHeight = false;

            // Modal Header
            Transform headerTrans = containerObj.transform.Find("Text_Header");
            GameObject headerObj = (headerTrans != null) ? headerTrans.gameObject : new GameObject("Text_Header");
            headerObj.transform.SetParent(containerObj.transform, false);
            TextMeshProUGUI headerTMP = headerObj.GetComponent<TextMeshProUGUI>();
            if (headerTMP == null) headerTMP = headerObj.AddComponent<TextMeshProUGUI>();
            headerTMP.text = "SELECT CONNECTION TYPE";
            headerTMP.fontSize = 26;
            headerTMP.fontStyle = FontStyles.Bold;
            headerTMP.alignment = TextAlignmentOptions.Center;
            headerTMP.color = Color.white;
            RectTransform headerRT = headerObj.GetComponent<RectTransform>();
            headerRT.sizeDelta = new Vector2(380, 50);

            // Modal Buttons: LOCAL (LAN), PUBLIC (RELAY), CANCEL
            Button localBtn = CreateOrGetButton(containerObj.transform, "Button_LocalLAN", "LOCAL (LAN)", new Color(0.2f, 0.6f, 0.35f));
            Button publicBtn = CreateOrGetButton(containerObj.transform, "Button_PublicRelay", "PUBLIC (RELAY)", new Color(0.25f, 0.45f, 0.8f));
            Button cancelBtn = CreateOrGetButton(containerObj.transform, "Button_Cancel", "CANCEL", new Color(0.5f, 0.2f, 0.2f));

            mainMenuUI.networkTypeModalPanel = modalObj;
            mainMenuUI.localConnectionButton = localBtn;
            mainMenuUI.publicConnectionButton = publicBtn;
            mainMenuUI.modalCancelButton = cancelBtn;

            // Modal initially inactive
            modalObj.SetActive(false);

            EditorUtility.SetDirty(canvasObj);
            EditorUtility.SetDirty(mainMenuUI);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[SetupTaskMainMenuUI] Canvas_MainMenu hierarchy successfully saved in {scenePath}!");
        }

        private static Button CreateOrGetButton(Transform parent, string buttonName, string label, Color bgColor)
        {
            Transform btnTrans = parent.Find(buttonName);
            GameObject btnObj = (btnTrans != null) ? btnTrans.gameObject : new GameObject(buttonName);
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            if (rt == null) rt = btnObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380, 60);

            Image img = btnObj.GetComponent<Image>();
            if (img == null) img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.GetComponent<Button>();
            if (btn == null) btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = bgColor * 1.25f;
            cb.pressedColor = bgColor * 0.8f;
            cb.selectedColor = bgColor;
            btn.colors = cb;

            Transform txtTrans = btnObj.transform.Find("Text");
            GameObject txtObj = (txtTrans != null) ? txtTrans.gameObject : new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);

            RectTransform txtRT = txtObj.GetComponent<RectTransform>();
            if (txtRT == null) txtRT = txtObj.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;
            txtRT.anchoredPosition = Vector2.zero;

            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }
    }
}
