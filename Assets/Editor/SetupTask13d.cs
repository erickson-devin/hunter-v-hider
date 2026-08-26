using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class SetupTask13d
{
    [MenuItem("Tools/Setup Tasks/Task 13d")]
    public static void ApplyPrefabOverrides()
    {
        if (ParrelSync.ClonesManager.IsClone()) return;
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "Tactical_Main") return;

        GameObject playerGo = GameObject.Find("Player");
        if (playerGo == null) return;

        if (PrefabUtility.IsAnyPrefabInstanceRoot(playerGo))
        {
            if (PrefabUtility.HasPrefabInstanceAnyOverrides(playerGo, false))
            {
                PrefabUtility.ApplyPrefabInstance(playerGo, InteractionMode.AutomatedAction);
                Debug.Log("Card 1.3d: Successfully applied Player instance overrides to the Player Prefab!");
            }
            else
            {
                Debug.Log("Card 1.3d: No overrides found on Player instance.");
            }
        }
        else
        {
            Debug.LogWarning("Card 1.3d: Player is not a prefab instance. Ensure it's linked to Player.prefab.");
        }

        EditorApplication.delayCall -= ApplyPrefabOverrides;
    }
}
