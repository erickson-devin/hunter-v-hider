using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HunterVsHider.Weapons;

[InitializeOnLoad]
public class SetupTask18
{
    static SetupTask18()
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
                    changed = true;
                }

                if (src.spatialBlend != 0.2f)
                {
                    src.spatialBlend = 0.2f;
                    changed = true;
                }

                if (src.playOnAwake)
                {
                    src.playOnAwake = false;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(gun.gameObject);
                }
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("Scene saved with AudioSource spatialBlend and playOnAwake configured.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
