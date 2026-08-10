using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HunterVsHider.Weapons;
using HunterVsHider.Player;

[InitializeOnLoad]
public class SetupTask16
{
    static SetupTask16()
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
            PlayerMovement pm = player.GetComponent<PlayerMovement>();
            Transform weaponHolder = pm != null ? pm.WeaponHolder : player.transform.Find("WeaponHolder");
            
            if (weaponHolder != null)
            {
                // Ensure Gun_Pistol exists
                Transform pistolTrans = weaponHolder.Find("Gun_Pistol");
                if (pistolTrans == null)
                {
                    Transform legacyGun = weaponHolder.Find("Gun");
                    if (legacyGun != null)
                    {
                        legacyGun.name = "Gun_Pistol";
                        pistolTrans = legacyGun;
                    }
                }
                
                GameObject pistolObj = null;
                if (pistolTrans != null)
                {
                    pistolObj = pistolTrans.gameObject;
                }

                Gun pistolGun = pistolObj != null ? pistolObj.GetComponent<Gun>() : null;

                // Create Gun_Rifle
                Transform rifleTrans = weaponHolder.Find("Gun_Rifle");
                GameObject rifleObj = null;
                
                if (rifleTrans == null)
                {
                    rifleObj = new GameObject("Gun_Rifle");
                    rifleObj.transform.SetParent(weaponHolder, false);
                    rifleObj.transform.localPosition = Vector3.zero;
                    
                    Gun rifleGun = rifleObj.AddComponent<Gun>();
                    
                    SerializedObject so = new SerializedObject(rifleGun);
                    so.FindProperty("maxAmmo").intValue = 20;
                    so.FindProperty("fireRate").floatValue = 0.08f;
                    so.FindProperty("reloadTime").floatValue = 1.5f;
                    
                    GameObject muzzle = new GameObject("MuzzlePoint");
                    muzzle.transform.SetParent(rifleObj.transform, false);
                    muzzle.transform.localPosition = new Vector3(0, 0, 1f); // front barrel tip
                    
                    so.FindProperty("muzzlePoint").objectReferenceValue = muzzle.transform;
                    so.ApplyModifiedProperties();

                    rifleObj.SetActive(false); // Inactive by default
                    changed = true;
                    Debug.Log("Created and configured Gun_Rifle");
                }
                else
                {
                    rifleObj = rifleTrans.gameObject;
                }

                Gun rifleGunComp = rifleObj.GetComponent<Gun>();

                // Assign to PlayerWeaponManager
                PlayerWeaponManager pwm = player.GetComponent<PlayerWeaponManager>();
                if (pwm != null && pistolGun != null && rifleGunComp != null)
                {
                    SerializedObject pwmSo = new SerializedObject(pwm);
                    var pistolProp = pwmSo.FindProperty("pistol");
                    var rifleProp = pwmSo.FindProperty("rifle");

                    if (pistolProp.objectReferenceValue != pistolGun || rifleProp.objectReferenceValue != rifleGunComp)
                    {
                        pistolProp.objectReferenceValue = pistolGun;
                        rifleProp.objectReferenceValue = rifleGunComp;
                        pwmSo.ApplyModifiedProperties();
                        changed = true;
                        Debug.Log("Assigned pistol and rifle to PlayerWeaponManager");
                    }
                }
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("Scene saved with secondary weapon setup.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
