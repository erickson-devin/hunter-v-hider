using UnityEngine;
using UnityEditor;
using HunterVsHider.Player;
using HunterVsHider.Weapons;

[InitializeOnLoad]
public class SetupTask14
{
    static SetupTask14()
    {
        EditorApplication.delayCall += AttachManager;
    }

    private static void AttachManager()
    {
        string prefabPath = "Assets/_Project/Prefabs/Player.prefab";
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabRoot != null)
        {
            HunterVsHider.Player.PlayerWeaponManager manager = prefabRoot.GetComponent<HunterVsHider.Player.PlayerWeaponManager>();
            if (manager == null)
            {
                manager = prefabRoot.AddComponent<HunterVsHider.Player.PlayerWeaponManager>();
                PrefabUtility.SavePrefabAsset(prefabRoot);
                Debug.Log("Card 1.4: Attached PlayerWeaponManager to Player.prefab");
            }
        }

        // Also check active scene in case it's unpacked or overridden
        GameObject playerGo = GameObject.Find("Player");
        if (playerGo != null)
        {
            HunterVsHider.Player.PlayerWeaponManager manager = playerGo.GetComponent<HunterVsHider.Player.PlayerWeaponManager>();
            if (manager == null)
            {
                manager = playerGo.AddComponent<HunterVsHider.Player.PlayerWeaponManager>();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log("Card 1.4: Attached PlayerWeaponManager to Player in active scene");
            }
        }

        EditorApplication.delayCall -= AttachManager;
    }
}

