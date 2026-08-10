using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

[InitializeOnLoad]
public class SetupTask11b
{
    static SetupTask11b()
    {
        EditorApplication.delayCall += ExecuteFix;
    }

    private static void ExecuteFix()
    {
        // 1. Create Materials directory if it doesn't exist
        string materialsDir = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(materialsDir))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        // 2. Create Mat_DarkGray.mat
        string matPath = materialsDir + "/Mat_DarkGray.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.2f, 0.2f, 0.2f); // Dark gray
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created Mat_DarkGray at " + matPath);
        }

        // 3. Find Floor object in active scene and assign material
        Scene activeScene = EditorSceneManager.GetActiveScene();
        bool changed = false;
        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            if (root.name == "Floor")
            {
                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != mat)
                {
                    renderer.sharedMaterial = mat;
                    EditorUtility.SetDirty(root);
                    changed = true;
                    Debug.Log("Assigned Mat_DarkGray to Floor in scene " + activeScene.name);
                }
            }
        }

        if (changed)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                
            EditorSceneManager.MarkSceneDirty(activeScene);
            }
            // Optionally save the scene
            // EditorSceneManager.SaveScene(activeScene);
        }
        
        // Remove self to prevent running repeatedly, but only if successful
        if (mat != null)
        {
            EditorApplication.delayCall -= ExecuteFix;
        }
    }
}

