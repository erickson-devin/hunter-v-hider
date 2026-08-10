using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HunterVsHider.Player;

[InitializeOnLoad]
public class SetupTask25
{
    static SetupTask25()
    {
        EditorApplication.delayCall += ExecuteSetup;
    }

    private static void ExecuteSetup()
    {
        if (Application.isPlaying) return;

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "Tactical_Main")
        {
            return;
        }

        bool changed = false;

        // 1. Create FOV Material
        string matFolder = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(matFolder))
        {
            string[] split = matFolder.Split('/');
            AssetDatabase.CreateFolder(split[0], split[1]);
        }

        string matPath = matFolder + "/Mat_FOV_YellowOutline.mat";
        Material fovMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (fovMat == null)
        {
            // Use Unlit/Color for a flat vibrant color
            fovMat = new Material(Shader.Find("Unlit/Color"));
            // #FFE600 -> RGB: 255, 230, 0
            fovMat.color = new Color(255f / 255f, 230f / 255f, 0f / 255f);
            
            AssetDatabase.CreateAsset(fovMat, matPath);
            AssetDatabase.SaveAssets();
            changed = true;
        }

        // 2. Setup Player and FOV Indicator
        GameObject player = GameObject.Find("Player") ?? GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Transform fovTransform = player.transform.Find("FOV_Indicator");
            if (fovTransform == null)
            {
                GameObject fovObj = new GameObject("FOV_Indicator");
                fovObj.transform.SetParent(player.transform, false);
                fovObj.transform.localPosition = new Vector3(0, 0.05f, 0);

                FOVMeshRenderer fovScript = fovObj.AddComponent<FOVMeshRenderer>();
                SerializedObject fovSo = new SerializedObject(fovScript);
                fovSo.FindProperty("fovAngle").floatValue = 90f;
                fovSo.FindProperty("viewDistance").floatValue = 15f;
                fovSo.FindProperty("rayCount").intValue = 50;
                
                // Set LayerMask to include "Obstacle" and "Default"
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                int defaultLayer = LayerMask.NameToLayer("Default");
                int mask = (1 << obstacleLayer) | (1 << defaultLayer);
                fovSo.FindProperty("obstacleLayer").intValue = mask;
                
                fovSo.ApplyModifiedProperties();

                MeshRenderer mr = fovObj.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = fovMat;
                }

                changed = true;
            }

            // 3. Link Player Prefab back to the asset
            string prefabPath = "Assets/_Project/Prefabs/Player.prefab";
            if (System.IO.File.Exists(prefabPath))
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(player, prefabPath, InteractionMode.AutomatedAction);
                changed = true;
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("Created FOVMeshRenderer logic, connected Prefab, and updated Scene.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
