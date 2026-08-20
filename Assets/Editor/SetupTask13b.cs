using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HunterVsHider.Weapons;

public class SetupTask13b
{
    [MenuItem("Tools/Setup Tasks/Task 13b")]
    public static void ExecuteSetup()
    {
        if (ParrelSync.ClonesManager.IsClone()) return;
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "Tactical_Main")
        {
            return; // Only execute if Tactical_Main is open
        }

        GameObject playerGo = GameObject.Find("Player");
        if (playerGo == null)
        {
            return;
        }

        bool changed = false;

        // 1. Ensure WeaponHolder is direct child of Player and aligned
        Transform weaponHolder = playerGo.transform.Find("WeaponHolder");
        if (weaponHolder == null)
        {
            // Maybe it exists somewhere else? Find it by name in scene
            GameObject whGo = GameObject.Find("WeaponHolder");
            if (whGo != null)
            {
                weaponHolder = whGo.transform;
            }
            else
            {
                weaponHolder = new GameObject("WeaponHolder").transform;
            }
        }

        if (weaponHolder.parent != playerGo.transform)
        {
            weaponHolder.SetParent(playerGo.transform, true);
            changed = true;
        }

        if (weaponHolder.localPosition != Vector3.zero || weaponHolder.localRotation != Quaternion.identity)
        {
            weaponHolder.localPosition = Vector3.zero;
            weaponHolder.localRotation = Quaternion.identity;
            changed = true;
        }

        // 2. Ensure Gun_Pistol is childed to WeaponHolder
        Transform gunPistol = weaponHolder.Find("Gun_Pistol");
        if (gunPistol == null)
        {
            GameObject gunGo = GameObject.Find("Gun_Pistol");
            if (gunGo != null)
            {
                gunPistol = gunGo.transform;
            }
            else
            {
                gunPistol = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                gunPistol.name = "Gun_Pistol";
                gunPistol.localScale = new Vector3(0.15f, 0.15f, 0.4f);
                Collider col = gunPistol.GetComponent<Collider>();
                if (col != null) GameObject.DestroyImmediate(col, true);
            }
        }

        if (gunPistol.parent != weaponHolder)
        {
            gunPistol.SetParent(weaponHolder, true);
            changed = true;
        }

        // Set gun relative offset
        if (gunPistol.localPosition != new Vector3(0.3f, 0f, 0.5f))
        {
            gunPistol.localPosition = new Vector3(0.3f, 0f, 0.5f);
            changed = true;
        }

        // 3. Ensure MuzzlePoint is childed to Gun_Pistol
        Transform muzzlePoint = gunPistol.Find("MuzzlePoint");
        if (muzzlePoint == null)
        {
            GameObject mzGo = GameObject.Find("MuzzlePoint");
            if (mzGo != null)
            {
                muzzlePoint = mzGo.transform;
            }
            else
            {
                muzzlePoint = new GameObject("MuzzlePoint").transform;
            }
        }

        if (muzzlePoint.parent != gunPistol)
        {
            muzzlePoint.SetParent(gunPistol, true);
            changed = true;
        }

        if (muzzlePoint.localPosition != new Vector3(0f, 0f, 0.2f))
        {
            muzzlePoint.localPosition = new Vector3(0f, 0f, 0.2f);
            changed = true;
        }

        // 4. Ensure Gun script is attached
        Gun gunScript = gunPistol.GetComponent<Gun>();
        if (gunScript == null)
        {
            gunPistol.gameObject.AddComponent<Gun>();
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(playerGo);
            if (!UnityEngine.Application.isPlaying)
            {
                
            EditorSceneManager.MarkSceneDirty(activeScene);
            }
            Debug.Log("Card 1.3b: Successfully re-parented and aligned the Weapon Hierarchy in Tactical_Main!");
        }

        // Unregister
        EditorApplication.delayCall -= ExecuteSetup;
    }
}

