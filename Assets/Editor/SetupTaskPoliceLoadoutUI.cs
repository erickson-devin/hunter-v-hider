using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HunterVsHider.Managers;

[InitializeOnLoad]
public class SetupTaskPoliceLoadoutUI
{
    static SetupTaskPoliceLoadoutUI()
    {
        EditorApplication.delayCall += ExecuteSetup;
    }

    [MenuItem("Tools/Hunter v Hider/Setup Police Loadout UI")]
    public static void ExecuteSetup()
    {
        if (Application.isPlaying) return;

        Debug.Log("[SetupTaskPoliceLoadoutUI] Starting Police Loadout UI Setup...");
        bool anyChanged = false;

        // 1. Create weapon materials
        string matDir = "Assets/_Project/Materials";
        if (!AssetDatabase.IsValidFolder(matDir))
        {
            AssetDatabase.CreateFolder("Assets/_Project", "Materials");
        }

        CreateColorMaterialIfMissing($"{matDir}/Mat_Weapon_Blue.mat", new Color(0.15f, 0.45f, 1.0f));
        CreateColorMaterialIfMissing($"{matDir}/Mat_Weapon_Green.mat", new Color(0.15f, 0.85f, 0.25f));
        CreateColorMaterialIfMissing($"{matDir}/Mat_Weapon_Red.mat", new Color(0.95f, 0.2f, 0.2f));

        // 2. Open Scene Tactical_Main
        string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        if (!activeScene.IsValid())
        {
            Debug.LogError($"[SetupTaskPoliceLoadoutUI] Could not open scene {scenePath}!");
            return;
        }

        // 3. Ensure EventSystem exists
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            eventSystem = esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
            anyChanged = true;
            Debug.Log("[SetupTaskPoliceLoadoutUI] Created EventSystem in Tactical_Main.");
        }

        // 4. Setup PoliceLoadoutUI Canvas in Scene
        GameObject loadoutCanvasObj = GameObject.Find("PoliceLoadoutUI");
        if (loadoutCanvasObj == null)
        {
            loadoutCanvasObj = new GameObject("PoliceLoadoutUI");
            anyChanged = true;
            Debug.Log("[SetupTaskPoliceLoadoutUI] Created PoliceLoadoutUI GameObject in Tactical_Main.");
        }

        Canvas canvas = loadoutCanvasObj.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = loadoutCanvasObj.AddComponent<Canvas>();
            anyChanged = true;
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = loadoutCanvasObj.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = loadoutCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            anyChanged = true;
        }

        GraphicRaycaster raycaster = loadoutCanvasObj.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = loadoutCanvasObj.AddComponent<GraphicRaycaster>();
            anyChanged = true;
        }

        PoliceLoadoutUI loadoutUI = loadoutCanvasObj.GetComponent<PoliceLoadoutUI>();
        if (loadoutUI == null)
        {
            loadoutUI = loadoutCanvasObj.AddComponent<PoliceLoadoutUI>();
            anyChanged = true;
        }

        // Initially disabled
        canvas.enabled = false;

        EditorUtility.SetDirty(loadoutCanvasObj);

        if (anyChanged)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupTaskPoliceLoadoutUI] Setup completed & Tactical_Main saved successfully.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }

    private static void CreateColorMaterialIfMissing(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit");
            if (standardShader == null) standardShader = Shader.Find("Standard");
            mat = new Material(standardShader);
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[SetupTaskPoliceLoadoutUI] Created weapon material at {path}");
        }
    }
}
