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
            Transform weaponHolder = player.transform.Find("WeaponHolder");
            if (weaponHolder != null)
            {
                // Pistol Mesh Setup
                Transform pistol = weaponHolder.Find("Gun_Pistol");
                if (pistol != null)
                {
                    Transform pistolMesh = pistol.Find("Pistol_Mesh");
                    if (pistolMesh == null)
                    {
                        GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        meshObj.name = "Pistol_Mesh";
                        meshObj.transform.SetParent(pistol, false);
                        pistolMesh = meshObj.transform;
                        changed = true;
                    }

                    // Enforce Transforms
                    if (pistolMesh.localPosition != new Vector3(0.3f, 0f, 0.3f))
                    {
                        pistolMesh.localPosition = new Vector3(0.3f, 0f, 0.3f);
                        changed = true;
                    }
                    if (pistolMesh.localScale != new Vector3(0.15f, 0.15f, 0.4f))
                    {
                        pistolMesh.localScale = new Vector3(0.15f, 0.15f, 0.4f);
                        changed = true;
                    }

                    // Strip Collider
                    Collider meshCol = pistolMesh.GetComponent<Collider>();
                    if (meshCol != null)
                    {
                        Object.DestroyImmediate(meshCol, true);
                        changed = true;
                    }

                    // Enforce AudioSource on Pistol
                    AudioSource pAudio = pistol.GetComponent<AudioSource>();
                    if (pAudio == null)
                    {
                        pAudio = pistol.gameObject.AddComponent<AudioSource>();
                        pAudio.playOnAwake = false;
                        pAudio.spatialBlend = 0.2f;
                        changed = true;
                    }
                }

                // Enforce AudioSource on Rifle
                Transform rifle = weaponHolder.Find("Gun_Rifle");
                if (rifle != null)
                {
                    AudioSource rAudio = rifle.GetComponent<AudioSource>();
                    if (rAudio == null)
                    {
                        rAudio = rifle.gameObject.AddComponent<AudioSource>();
                        rAudio.playOnAwake = false;
                        rAudio.spatialBlend = 0.2f;
                        changed = true;
                    }
                }
            }
        }

        if (changed)
        {
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
            }
            Debug.Log("Configured Pistol Mesh and AudioSources");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
