using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HunterVsHider.UI;
using HunterVsHider.Player;
using HunterVsHider.Vision;

namespace HunterVsHider.Editor
{
    [InitializeOnLoad]
    public static class SetupTaskMiniMapUI
    {
        private const string ScenePath = "Assets/_Project/Scenes/Tactical_Main.unity";

        static SetupTaskMiniMapUI()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("SetupTaskMiniMapUI_Executed", false))
                {
                    SessionState.SetBool("SetupTaskMiniMapUI_Executed", true);
                    ExecuteSetup();
                }
            };
        }

        [MenuItem("Tools/Hunter v Hider/Setup State-Gated Mini-Map UI")]
        public static void ExecuteSetup()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;
            if (Application.isPlaying) return;

            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskMiniMapUI] STARTING STATE-GATED MINI-MAP UI SETUP...");
            Debug.Log("==========================================================================");

            bool anyModified = false;

            // 1. Ensure TargetVisibility on Player.prefab
            string playerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab != null)
            {
                if (playerPrefab.GetComponent<TargetVisibility>() == null)
                {
                    playerPrefab.AddComponent<TargetVisibility>();
                    EditorUtility.SetDirty(playerPrefab);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[SetupTaskMiniMapUI] Added TargetVisibility component to Player.prefab.");
                }
            }

            // 1b. Load MiniMap FoW Overlay Material
            string matPath = "Assets/_Project/Materials/Mat_MiniMap_FoWOverlay.mat";
            Material miniMapMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (miniMapMat == null)
            {
                Shader shader = Shader.Find("HunterVsHider/UI/MiniMap_FoWOverlay");
                if (shader != null)
                {
                    miniMapMat = new Material(shader);
                    AssetDatabase.CreateAsset(miniMapMat, matPath);
                    AssetDatabase.SaveAssets();
                }
            }

            // 2. Open or check scene
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // 3. Create or locate Canvas_MiniMap
            GameObject canvasObj = GameObject.Find("Canvas_MiniMap");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("Canvas_MiniMap");
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas_MiniMap");
                anyModified = true;
            }

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = canvasObj.AddComponent<GraphicRaycaster>();

            // 4. Create or configure MiniMap_Panel (200px x 200px anchored Top-Right)
            Transform panelTr = canvasObj.transform.Find("MiniMap_Panel");
            GameObject panelObj;
            if (panelTr == null)
            {
                panelObj = new GameObject("MiniMap_Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                panelObj.transform.SetParent(canvasObj.transform, false);
                Undo.RegisterCreatedObjectUndo(panelObj, "Create MiniMap_Panel");
                anyModified = true;
            }
            else
            {
                panelObj = panelTr.gameObject;
            }

            RectTransform panelRT = panelObj.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(1f, 1f);
            panelRT.anchorMax = new Vector2(1f, 1f);
            panelRT.pivot = new Vector2(1f, 1f);
            panelRT.anchoredPosition = new Vector2(-20f, -20f);
            panelRT.sizeDelta = new Vector2(200f, 200f);

            Image panelBg = panelObj.GetComponent<Image>();
            if (panelBg == null) panelBg = panelObj.AddComponent<Image>();
            panelBg.color = new Color(0.04f, 0.06f, 0.09f, 0.92f); // Deep Tactical Slate

            CanvasGroup canvasGroup = panelObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panelObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // 5. Create Mask Container
            Transform maskTr = panelObj.transform.Find("MapMask");
            GameObject maskObj;
            if (maskTr == null)
            {
                maskObj = new GameObject("MapMask", typeof(RectTransform), typeof(RectMask2D));
                maskObj.transform.SetParent(panelObj.transform, false);
                anyModified = true;
            }
            else
            {
                maskObj = maskTr.gameObject;
            }

            RectTransform maskRT = maskObj.GetComponent<RectTransform>();
            maskRT.anchorMin = Vector2.zero;
            maskRT.anchorMax = Vector2.one;
            maskRT.offsetMin = new Vector2(2f, 2f);
            maskRT.offsetMax = new Vector2(-2f, -2f);

            // 6. Create MapTextureDisplay (RawImage)
            Transform mapDisplayTr = maskObj.transform.Find("MapTextureDisplay");
            GameObject mapDisplayObj;
            if (mapDisplayTr == null)
            {
                mapDisplayObj = new GameObject("MapTextureDisplay", typeof(RectTransform), typeof(RawImage));
                mapDisplayObj.transform.SetParent(maskObj.transform, false);
                anyModified = true;
            }
            else
            {
                mapDisplayObj = mapDisplayTr.gameObject;
            }

            RectTransform mapDisplayRT = mapDisplayObj.GetComponent<RectTransform>();
            mapDisplayRT.anchorMin = Vector2.zero;
            mapDisplayRT.anchorMax = Vector2.one;
            mapDisplayRT.offsetMin = Vector2.zero;
            mapDisplayRT.offsetMax = Vector2.zero;

            RawImage mapRawImage = mapDisplayObj.GetComponent<RawImage>();
            if (mapRawImage == null) mapRawImage = mapDisplayObj.AddComponent<RawImage>();
            mapRawImage.color = Color.white;
            mapRawImage.raycastTarget = false;

            // 7. Create EnemyIconsContainer
            Transform enemyTr = maskObj.transform.Find("EnemyIconsContainer");
            GameObject enemyObj;
            if (enemyTr == null)
            {
                enemyObj = new GameObject("EnemyIconsContainer", typeof(RectTransform));
                enemyObj.transform.SetParent(maskObj.transform, false);
                anyModified = true;
            }
            else
            {
                enemyObj = enemyTr.gameObject;
            }

            RectTransform enemyRT = enemyObj.GetComponent<RectTransform>();
            enemyRT.anchorMin = Vector2.zero;
            enemyRT.anchorMax = Vector2.one;
            enemyRT.offsetMin = Vector2.zero;
            enemyRT.offsetMax = Vector2.zero;

            // 8. Create Local Player Icon (White Directional Arrow)
            Transform localIconTr = maskObj.transform.Find("PlayerIconLocal");
            GameObject localIconObj;
            if (localIconTr == null)
            {
                localIconObj = new GameObject("PlayerIconLocal", typeof(RectTransform), typeof(Image));
                localIconObj.transform.SetParent(maskObj.transform, false);
                anyModified = true;
            }
            else
            {
                localIconObj = localIconTr.gameObject;
            }

            RectTransform localIconRT = localIconObj.GetComponent<RectTransform>();
            localIconRT.sizeDelta = new Vector2(16f, 16f);
            localIconRT.pivot = new Vector2(0.5f, 0.5f);
            localIconRT.anchoredPosition = Vector2.zero;

            Image localIconImage = localIconObj.GetComponent<Image>();
            if (localIconImage == null) localIconImage = localIconObj.AddComponent<Image>();
            localIconImage.color = Color.white; // Clean White Arrow
            localIconImage.raycastTarget = false;

            // 9. Add Sleek Tactical Header Badge
            Transform headerTr = panelObj.transform.Find("HeaderBadge");
            if (headerTr == null)
            {
                GameObject headerObj = new GameObject("HeaderBadge", typeof(RectTransform), typeof(Text));
                headerObj.transform.SetParent(panelObj.transform, false);
                RectTransform headerRT = headerObj.GetComponent<RectTransform>();
                headerRT.anchorMin = new Vector2(0f, 1f);
                headerRT.anchorMax = new Vector2(1f, 1f);
                headerRT.pivot = new Vector2(0.5f, 0f);
                headerRT.anchoredPosition = new Vector2(0f, 4f);
                headerRT.sizeDelta = new Vector2(200f, 18f);

                Text headerText = headerObj.GetComponent<Text>();
                headerText.text = "TACTICAL RADAR // 50m";
                headerText.fontSize = 11;
                headerText.alignment = TextAnchor.MiddleCenter;
                headerText.color = new Color(0.45f, 0.65f, 0.85f, 0.9f);
                anyModified = true;
            }

            // 10. Attach and wire MiniMapUI script
            MiniMapUI miniMapUI = canvasObj.GetComponent<MiniMapUI>();
            if (miniMapUI == null)
            {
                miniMapUI = canvasObj.AddComponent<MiniMapUI>();
                anyModified = true;
            }

            miniMapUI.miniMapPanel = panelRT;
            miniMapUI.miniMapCanvasGroup = canvasGroup;
            miniMapUI.mapTextureDisplay = mapRawImage;
            miniMapUI.miniMapMaskMaterial = miniMapMat;
            miniMapUI.playerIconLocal = localIconRT;
            miniMapUI.enemyIconsContainer = enemyRT;
            miniMapUI.localPlayerColor = Color.white;
            miniMapUI.spottedEnemyColor = new Color(0.95f, 0.25f, 0.25f, 1f);
            miniMapUI.defaultArenaSize = 50f;
            miniMapUI.fowWorldDimensions = new Vector2(300f, 300f);
            miniMapUI.fowWorldCenter = Vector3.zero;

            if (miniMapMat != null)
            {
                mapRawImage.material = miniMapMat;
            }

            // 11. Mark scene dirty and save
            EditorUtility.SetDirty(canvasObj);
            EditorUtility.SetDirty(panelObj);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[SetupTaskMiniMapUI] State-gated tactical mini-map UI hierarchy configured and saved successfully in Tactical_Main.unity! (AnyModified: {anyModified})");
        }
    }
}
