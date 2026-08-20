using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HunterVsHider.Weapons;
using System.Reflection;

public class SetupTask13
{
    [MenuItem("Tools/Setup Tasks/Task 13")]
    public static void ExecuteSetup()
    {
        if (ParrelSync.ClonesManager.IsClone()) return;
        string prefabPath = "Assets/_Project/Prefabs/Player.prefab";
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabRoot == null) return;

        bool changed = false;

        // 1. Find or create WeaponHolder
        Transform weaponHolder = prefabRoot.transform.Find("WeaponHolder");
        if (weaponHolder == null)
        {
            GameObject holder = new GameObject("WeaponHolder");
            holder.transform.SetParent(prefabRoot.transform, false);
            holder.transform.localPosition = Vector3.zero;
            weaponHolder = holder.transform;
            changed = true;
        }

        // 2. Find or create Gun_Pistol
        Transform gunPistol = weaponHolder.Find("Gun_Pistol");
        if (gunPistol == null)
        {
            GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = "Gun_Pistol";
            gun.transform.SetParent(weaponHolder, false);
            gun.transform.localPosition = new Vector3(0.3f, 0.0f, 0.5f);
            gun.transform.localScale = new Vector3(0.15f, 0.15f, 0.4f);
            
            Collider col = gun.GetComponent<Collider>();
            if (col != null) GameObject.DestroyImmediate(col, true);
            
            gunPistol = gun.transform;
            changed = true;
        }

        // 3. Find or create MuzzlePoint
        Transform muzzlePoint = gunPistol.Find("MuzzlePoint");
        if (muzzlePoint == null)
        {
            GameObject muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(gunPistol, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            muzzlePoint = muzzle.transform;
            changed = true;
        }

        // 4. Attach Gun script and configure serialized field
        Gun gunScript = gunPistol.GetComponent<Gun>();
        if (gunScript == null)
        {
            gunScript = gunPistol.gameObject.AddComponent<Gun>();
            changed = true;
        }

        // Assign muzzlePoint using SerializedObject to modify private serialized field
        SerializedObject serializedGun = new SerializedObject(gunScript);
        SerializedProperty prop = serializedGun.FindProperty("muzzlePoint");
        if (prop != null && prop.objectReferenceValue != muzzlePoint)
        {
            prop.objectReferenceValue = muzzlePoint; // Assign the Transform component
            serializedGun.ApplyModifiedProperties();
            changed = true;
        }

        if (changed)
        {
            PrefabUtility.SavePrefabAsset(prefabRoot);
            Debug.Log("Card 1.3: Successfully configured Gun_Pistol inside Player.prefab");
        }

        // Clean up the editor script hook
        EditorApplication.delayCall -= ExecuteSetup;
    }
}
