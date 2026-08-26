using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using HunterVsHider.Map;
using HunterVsHider.Managers;

namespace HunterVsHider.EditorScripts
{
    public class SetupTaskMapGenerator
    {
        [MenuItem("Tools/Hunter v Hider/Setup Map Generator and Assassin UI")]
        public static void ExecuteSetup()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;
            if (Application.isPlaying) return;

            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[SetupTaskMapGenerator] Could not open scene {scenePath}!");
                return;
            }

            bool anyChanged = false;

            // 1. Setup MapGenerator in Zone_CombatArena
            GameObject combatArena = GameObject.Find("Zone_CombatArena");
            if (combatArena == null)
            {
                combatArena = new GameObject("Zone_CombatArena");
                anyChanged = true;
            }

            Transform mapGenTrans = combatArena.transform.Find("MapGenerator");
            GameObject mapGenObj = (mapGenTrans != null) ? mapGenTrans.gameObject : null;
            if (mapGenObj == null)
            {
                mapGenObj = new GameObject("MapGenerator");
                mapGenObj.transform.SetParent(combatArena.transform, false);
                mapGenObj.transform.localPosition = Vector3.zero;
                mapGenObj.transform.localRotation = Quaternion.identity;
                mapGenObj.transform.localScale = Vector3.one;
                anyChanged = true;
            }

            MapGenerator mapGen = mapGenObj.GetComponent<MapGenerator>();
            if (mapGen == null)
            {
                mapGen = mapGenObj.AddComponent<MapGenerator>();
                anyChanged = true;
            }

            Transform envTrans = mapGenObj.transform.Find("GeneratedEnvironment");
            if (envTrans == null)
            {
                GameObject envObj = new GameObject("GeneratedEnvironment");
                envObj.transform.SetParent(mapGenObj.transform, false);
                envObj.transform.localPosition = Vector3.zero;
                envObj.transform.localRotation = Quaternion.identity;
                envObj.transform.localScale = Vector3.one;
                mapGen.generatedEnvironment = envObj.transform;
                anyChanged = true;
            }
            else
            {
                mapGen.generatedEnvironment = envTrans;
            }

            EditorUtility.SetDirty(mapGen);

            // 2. Setup AssassinPrepUI Canvas
            GameObject assassinCanvasObj = GameObject.Find("AssassinPrepUI");
            if (assassinCanvasObj == null)
            {
                assassinCanvasObj = new GameObject("AssassinPrepUI");
                anyChanged = true;
            }

            Canvas canvas = assassinCanvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = assassinCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            CanvasScaler scaler = assassinCanvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = assassinCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = assassinCanvasObj.GetComponent<GraphicRaycaster>();
            if (raycaster == null) assassinCanvasObj.AddComponent<GraphicRaycaster>();

            AssassinPrepUI prepUI = assassinCanvasObj.GetComponent<AssassinPrepUI>();
            if (prepUI == null) prepUI = assassinCanvasObj.AddComponent<AssassinPrepUI>();

            // Root Panel (Container)
            GameObject panelObj = GetOrCreateChild(assassinCanvasObj, "Panel_AssassinPrep");
            RectTransform panelRt = panelObj.GetComponent<RectTransform>();
            if (panelRt == null) panelRt = panelObj.AddComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            // Generate Random Map Button (Bottom Center)
            GameObject btnObj = GetOrCreateChild(panelObj, "Button_GenerateMap");
            RectTransform btnRt = btnObj.GetComponent<RectTransform>();
            if (btnRt == null) btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0f);
            btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.sizeDelta = new Vector2(360, 56);
            btnRt.anchoredPosition = new Vector2(0, 40);

            Image btnImg = btnObj.GetComponent<Image>();
            if (btnImg == null) btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.35f, 0.65f, 1f);

            Button btn = btnObj.GetComponent<Button>();
            if (btn == null) btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.15f, 0.35f, 0.65f, 1f);
            cb.highlightedColor = new Color(0.22f, 0.48f, 0.85f, 1f);
            cb.pressedColor = new Color(0.10f, 0.25f, 0.48f, 1f);
            cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            btn.colors = cb;

            GameObject btnTextObj = GetOrCreateChild(btnObj, "Text");
            TextMeshProUGUI btnText = btnTextObj.GetComponent<TextMeshProUGUI>();
            if (btnText == null) btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "🎲 GENERATE RANDOM MAP";
            btnText.fontSize = 18;
            btnText.fontStyle = FontStyles.Bold;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;
            RectTransform txtRt = btnTextObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            // Seed Info Text
            GameObject seedTextObj = GetOrCreateChild(panelObj, "Txt_SeedInfo");
            TextMeshProUGUI seedText = seedTextObj.GetComponent<TextMeshProUGUI>();
            if (seedText == null) seedText = seedTextObj.AddComponent<TextMeshProUGUI>();
            seedText.text = "MAP SEED: #------";
            seedText.fontSize = 14;
            seedText.fontStyle = FontStyles.Italic;
            seedText.alignment = TextAlignmentOptions.Center;
            seedText.color = new Color(0.3f, 0.85f, 1.0f);
            RectTransform seedRt = seedTextObj.GetComponent<RectTransform>();
            seedRt.anchorMin = new Vector2(0.5f, 0f);
            seedRt.anchorMax = new Vector2(0.5f, 0f);
            seedRt.pivot = new Vector2(0.5f, 0f);
            seedRt.sizeDelta = new Vector2(300, 24);
            seedRt.anchoredPosition = new Vector2(0, 102);

            // Wire references
            prepUI.prepContainer = panelObj;
            prepUI.buttonGenerateMap = btn;
            prepUI.textSeedInfo = seedText;

            // Panel disabled by default
            panelObj.SetActive(false);

            EditorUtility.SetDirty(assassinCanvasObj);
            EditorUtility.SetDirty(prepUI);

            if (anyChanged)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log("[SetupTaskMapGenerator] Setup complete! MapGenerator and AssassinPrepUI configured and saved in Tactical_Main.");
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
    }
}
