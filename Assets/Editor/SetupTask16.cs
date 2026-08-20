using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HunterVsHider.Weapons;

public class SetupTask16
{
    [MenuItem("Tools/Setup Tasks/Task 16")]
    public static void ExecuteSetup()
    {
        if (ParrelSync.ClonesManager.IsClone()) return;
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
                Transform rifleTrans = weaponHolder.Find("Gun_Rifle");
                GameObject rifleObj;
                
                if (rifleTrans == null)
                {
                    rifleObj = new GameObject("Gun_Rifle");
                    rifleObj.transform.SetParent(weaponHolder, false);
                    rifleTrans = rifleObj.transform;
                    rifleObj.SetActive(false);
                    changed = true;
                }
                else
                {
                    rifleObj = rifleTrans.gameObject;
                }

                // Enforce Gun_Rifle parent transforms
                rifleTrans.localPosition = Vector3.zero;
                rifleTrans.localScale = Vector3.one;

                Transform rifleMeshTrans = rifleTrans.Find("Rifle_Mesh");
                if (rifleMeshTrans == null)
                {
                    GameObject rifleMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rifleMesh.name = "Rifle_Mesh";
                    rifleMesh.transform.SetParent(rifleTrans, false);
                    rifleMeshTrans = rifleMesh.transform;
                    changed = true;
                }

                // Enforce Rifle_Mesh transforms
                rifleMeshTrans.localPosition = new Vector3(0.3f, 0f, 0.5f);
                rifleMeshTrans.localRotation = Quaternion.identity;
                rifleMeshTrans.localScale = new Vector3(0.15f, 0.15f, 0.9f);

                // Strip collider
                Collider meshCol = rifleMeshTrans.GetComponent<Collider>();
                if (meshCol != null) 
                {
                    Object.DestroyImmediate(meshCol, true);
                    changed = true;
                }

                Transform muzzlePointTrans = rifleTrans.Find("MuzzlePoint");
                if (muzzlePointTrans == null)
                {
                    GameObject muzzlePoint = new GameObject("MuzzlePoint");
                    muzzlePoint.transform.SetParent(rifleTrans, false);
                    muzzlePointTrans = muzzlePoint.transform;
                    changed = true;
                }

                // Enforce MuzzlePoint transforms
                muzzlePointTrans.localPosition = new Vector3(0.3f, 0f, 1.0f);

                Gun gunScript = rifleObj.GetComponent<Gun>();
                if (gunScript == null)
                {
                    gunScript = rifleObj.AddComponent<Gun>();
                    changed = true;
                }

                SerializedObject gunSo = new SerializedObject(gunScript);
                if (gunSo.FindProperty("maxAmmo").intValue != 20)
                {
                    gunSo.FindProperty("maxAmmo").intValue = 20;
                    gunSo.FindProperty("fireRate").floatValue = 0.08f;
                    gunSo.FindProperty("reloadTime").floatValue = 1.5f;
                    gunSo.FindProperty("muzzlePoint").objectReferenceValue = muzzlePointTrans;
                    gunSo.ApplyModifiedProperties();
                    changed = true;
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
            Debug.Log("Created and configured Gun_Rifle");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
