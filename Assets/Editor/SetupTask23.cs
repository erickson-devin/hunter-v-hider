using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class SetupTask23
{
    static SetupTask23()
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

        // 1. Ensure Materials folder exists
        string matFolder = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(matFolder))
        {
            string[] split = matFolder.Split('/');
            AssetDatabase.CreateFolder(split[0], split[1]);
        }

        // 2. Create or load Mat_Wall_Slate.mat
        string matPath = matFolder + "/Mat_Wall_Slate.mat";
        Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (wallMat == null)
        {
            wallMat = new Material(Shader.Find("Standard"));
            
            // RGB: 50, 55, 65 mapped to 0-1
            wallMat.color = new Color(50f / 255f, 55f / 255f, 65f / 255f);
            wallMat.SetFloat("_Glossiness", 0.2f);
            
            AssetDatabase.CreateAsset(wallMat, matPath);
            AssetDatabase.SaveAssets();
            changed = true;
        }

        // 3. Apply to all walls under _TacticalMap
        GameObject mapRoot = GameObject.Find("_TacticalMap");
        if (mapRoot != null)
        {
            MeshRenderer[] renderers = mapRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer r in renderers)
            {
                if (r.sharedMaterial != wallMat)
                {
                    r.sharedMaterial = wallMat;
                    EditorUtility.SetDirty(r);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("Created Mat_Wall_Slate and applied to all TacticalMap walls.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
