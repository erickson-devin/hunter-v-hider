using UnityEngine;
using UnityEditor;
using HunterVsHider.Player;
using HunterVsHider.Weapons;

public class SetupTask14
{
    [MenuItem("Tools/Setup Tasks/Task 14")]
    public static void AttachManager()
    {
        if (ParrelSync.ClonesManager.IsClone()) return;
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

