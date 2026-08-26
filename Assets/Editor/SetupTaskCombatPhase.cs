using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using HunterVsHider.Managers;
using HunterVsHider.Map;

namespace HunterVsHider.EditorScripts
{
    public class SetupTaskCombatPhase
    {
        [MenuItem("Tools/Hunter v Hider/Setup Combat Phase Spawning & UI")]
        public static void ExecuteSetup()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;
            if (Application.isPlaying) return;

            Debug.Log("[SetupTaskCombatPhase] Setting up Combat Phase Spawning & UI...");

            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[SetupTaskCombatPhase] Could not open scene {scenePath}!");
                return;
            }

            bool sceneDirty = false;

            // 1. Ensure MapGenerator component exists
            GameObject combatArena = GameObject.Find("Zone_CombatArena");
            if (combatArena != null)
            {
                Transform mapGenTrans = combatArena.transform.Find("MapGenerator");
                if (mapGenTrans == null)
                {
                    GameObject mapGenObj = new GameObject("MapGenerator");
                    mapGenObj.transform.SetParent(combatArena.transform, false);
                    mapGenTrans = mapGenObj.transform;
                    sceneDirty = true;
                }
                if (mapGenTrans.GetComponent<MapGenerator>() == null)
                {
                    mapGenTrans.gameObject.AddComponent<MapGenerator>();
                    sceneDirty = true;
                }
            }

            // 2. Setup AssassinPrepUI Canvas with Start Combat Button
            GameObject canvasObj = GameObject.Find("Canvas_AssassinPrep");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("Canvas_AssassinPrep");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
                sceneDirty = true;
            }

            AssassinPrepUI prepUI = canvasObj.GetComponent<AssassinPrepUI>();
            if (prepUI == null)
            {
                prepUI = canvasObj.AddComponent<AssassinPrepUI>();
                sceneDirty = true;
            }

            // Panel Container
            Transform panelTrans = canvasObj.transform.Find("Panel_AssassinPrep");
            GameObject panelObj;
            if (panelTrans == null)
            {
                panelObj = new GameObject("Panel_AssassinPrep");
                panelObj.transform.SetParent(canvasObj.transform, false);
                sceneDirty = true;
            }
            else
            {
                panelObj = panelTrans.gameObject;
            }

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            if (panelRect == null) panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 30f);
            panelRect.sizeDelta = new Vector2(680f, 110f);

            Image panelImg = panelObj.GetComponent<Image>();
            if (panelImg == null) panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);

            // Horizontal Layout Group
            HorizontalLayoutGroup hlg = panelObj.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = panelObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.padding = new RectOffset(20, 20, 15, 15);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Generate Map Button
            Transform btnGenTrans = panelObj.transform.Find("Btn_GenerateMap");
            GameObject btnGenObj;
            if (btnGenTrans == null)
            {
                btnGenObj = new GameObject("Btn_GenerateMap");
                btnGenObj.transform.SetParent(panelObj.transform, false);
                sceneDirty = true;
            }
            else
            {
                btnGenObj = btnGenTrans.gameObject;
            }

            RectTransform btnGenRect = btnGenObj.GetComponent<RectTransform>();
            if (btnGenRect == null) btnGenRect = btnGenObj.AddComponent<RectTransform>();
            btnGenRect.sizeDelta = new Vector2(280f, 70f);

            Image btnGenImg = btnGenObj.GetComponent<Image>();
            if (btnGenImg == null) btnGenImg = btnGenObj.AddComponent<Image>();
            btnGenImg.color = new Color(0.12f, 0.45f, 0.85f, 1f);

            Button btnGen = btnGenObj.GetComponent<Button>();
            if (btnGen == null) btnGen = btnGenObj.AddComponent<Button>();

            Transform lblGenTrans = btnGenObj.transform.Find("Text_Label");
            GameObject lblGenObj;
            if (lblGenTrans == null)
            {
                lblGenObj = new GameObject("Text_Label");
                lblGenObj.transform.SetParent(btnGenObj.transform, false);
                sceneDirty = true;
            }
            else
            {
                lblGenObj = lblGenTrans.gameObject;
            }

            RectTransform lblGenRect = lblGenObj.GetComponent<RectTransform>();
            if (lblGenRect == null) lblGenRect = lblGenObj.AddComponent<RectTransform>();
            lblGenRect.anchorMin = Vector2.zero;
            lblGenRect.anchorMax = Vector2.one;
            lblGenRect.offsetMin = Vector2.zero;
            lblGenRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmpGen = lblGenObj.GetComponent<TextMeshProUGUI>();
            if (tmpGen == null) tmpGen = lblGenObj.AddComponent<TextMeshProUGUI>();
            tmpGen.text = "🎲 GENERATE MAP";
            tmpGen.fontSize = 20;
            tmpGen.fontStyle = FontStyles.Bold;
            tmpGen.alignment = TextAlignmentOptions.Center;
            tmpGen.color = Color.white;

            // Start Combat Button
            Transform btnCombatTrans = panelObj.transform.Find("Btn_StartCombat");
            GameObject btnCombatObj;
            if (btnCombatTrans == null)
            {
                btnCombatObj = new GameObject("Btn_StartCombat");
                btnCombatObj.transform.SetParent(panelObj.transform, false);
                sceneDirty = true;
            }
            else
            {
                btnCombatObj = btnCombatTrans.gameObject;
            }

            RectTransform btnCombatRect = btnCombatObj.GetComponent<RectTransform>();
            if (btnCombatRect == null) btnCombatRect = btnCombatObj.AddComponent<RectTransform>();
            btnCombatRect.sizeDelta = new Vector2(280f, 70f);

            Image btnCombatImg = btnCombatObj.GetComponent<Image>();
            if (btnCombatImg == null) btnCombatImg = btnCombatObj.AddComponent<Image>();
            btnCombatImg.color = new Color(0.85f, 0.22f, 0.15f, 1f); // Vivid Crimson

            Button btnCombat = btnCombatObj.GetComponent<Button>();
            if (btnCombat == null) btnCombat = btnCombatObj.AddComponent<Button>();

            Transform lblCombatTrans = btnCombatObj.transform.Find("Text_Label");
            GameObject lblCombatObj;
            if (lblCombatTrans == null)
            {
                lblCombatObj = new GameObject("Text_Label");
                lblCombatObj.transform.SetParent(btnCombatObj.transform, false);
                sceneDirty = true;
            }
            else
            {
                lblCombatObj = lblCombatTrans.gameObject;
            }

            RectTransform lblCombatRect = lblCombatObj.GetComponent<RectTransform>();
            if (lblCombatRect == null) lblCombatRect = lblCombatObj.AddComponent<RectTransform>();
            lblCombatRect.anchorMin = Vector2.zero;
            lblCombatRect.anchorMax = Vector2.one;
            lblCombatRect.offsetMin = Vector2.zero;
            lblCombatRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmpCombat = lblCombatObj.GetComponent<TextMeshProUGUI>();
            if (tmpCombat == null) tmpCombat = lblCombatObj.AddComponent<TextMeshProUGUI>();
            tmpCombat.text = "⚔️ START COMBAT";
            tmpCombat.fontSize = 20;
            tmpCombat.fontStyle = FontStyles.Bold;
            tmpCombat.alignment = TextAlignmentOptions.Center;
            tmpCombat.color = Color.white;

            // Link references to AssassinPrepUI
            prepUI.prepContainer = panelObj;
            prepUI.buttonGenerateMap = btnGen;
            prepUI.buttonStartCombat = btnCombat;
            EditorUtility.SetDirty(prepUI);
            sceneDirty = true;

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log("[SetupTaskCombatPhase] Saved AssassinPrepUI with Start Combat Phase button in Tactical_Main.unity.");
            }

            EditorApplication.delayCall -= ExecuteSetup;
        }
    }
}
