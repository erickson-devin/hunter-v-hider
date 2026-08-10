using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class SetupTask13c
{
    static SetupTask13c()
    {
        EditorApplication.delayCall += ExecuteCleanup;
    }

    private static void ExecuteCleanup()
    {
        bool sceneChanged = false;

        // 1. Clean up active scene
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name == "Tactical_Main")
        {
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                // Remove VisualIndicator_Forward
                Transform indicator = playerGo.transform.Find("VisualIndicator_Forward");
                if (indicator != null)
                {
                    GameObject.DestroyImmediate(indicator.gameObject);
                    sceneChanged = true;
                }

                // Remove duplicate WeaponHolders
                int holderCount = 0;
                // Iterate backwards when deleting children
                for (int i = playerGo.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = playerGo.transform.GetChild(i);
                    if (child.name == "WeaponHolder")
                    {
                        holderCount++;
                        if (holderCount > 1)
                        {
                            GameObject.DestroyImmediate(child.gameObject);
                            sceneChanged = true;
                        }
                        else
                        {
                            // Verify remaining WeaponHolder
                            child.localPosition = Vector3.zero;
                            child.localRotation = Quaternion.identity;
                            // It should contain Gun_Pistol -> MuzzlePoint (handled by Task 13b)
                        }
                    }
                }
            }

            if (sceneChanged)
            {
                EditorUtility.SetDirty(playerGo);
                if (!UnityEngine.Application.isPlaying)
                {
                    
                EditorSceneManager.MarkSceneDirty(activeScene);
                }
            }
        }

        // 2. Clean up Prefab
        string prefabPath = "Assets/_Project/Prefabs/Player.prefab";
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabRoot != null)
        {
            bool prefabChanged = false;

            Transform indicator = prefabRoot.transform.Find("VisualIndicator_Forward");
            if (indicator != null)
            {
                GameObject.DestroyImmediate(indicator.gameObject, true);
                prefabChanged = true;
            }

            int holderCount = 0;
            for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = prefabRoot.transform.GetChild(i);
                if (child.name == "WeaponHolder")
                {
                    holderCount++;
                    if (holderCount > 1)
                    {
                        GameObject.DestroyImmediate(child.gameObject, true);
                        prefabChanged = true;
                    }
                }
            }

            if (prefabChanged)
            {
                PrefabUtility.SavePrefabAsset(prefabRoot);
                Debug.Log("Card 1.3c: Cleaned up Player.prefab hierarchy.");
            }
        }

        if (sceneChanged)
        {
            Debug.Log("Card 1.3c: Cleaned up active scene Tactical_Main.");
        }

        EditorApplication.delayCall -= ExecuteCleanup;
    }
}

