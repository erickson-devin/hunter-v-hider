using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HunterVsHider.Weapons;

[InitializeOnLoad]
public class SetupTask17
{
    static SetupTask17()
    {
        EditorApplication.delayCall += ExecuteSetup;
    }

    private static void ExecuteSetup()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "Tactical_Main")
        {
            return;
        }

        bool changed = false;

        GameObject player = GameObject.Find("Player") ?? GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Gun[] guns = player.GetComponentsInChildren<Gun>(true);
            
            foreach (Gun gun in guns)
            {
                AudioSource src = gun.GetComponent<AudioSource>();
                if (src == null)
                {
                    src = gun.gameObject.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    changed = true;
                    Debug.Log($"Added AudioSource to {gun.gameObject.name}");
                }
                else if (src.playOnAwake)
                {
                    src.playOnAwake = false;
                    changed = true;
                    Debug.Log($"Set playOnAwake=false on {gun.gameObject.name}");
                }
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("Scene saved with AudioSource components added to weapons.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
