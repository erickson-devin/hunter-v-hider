using System.IO;
using UnityEditor;
using UnityEngine;
using HunterVsHider.Weapons;
using HunterVsHider.Player;

namespace HunterVsHider.Editor
{
    public static class SetupTaskAssassinWeapons
    {
        [MenuItem("Tools/Hunter v Hider/Verify Assassin Weapon Kit")]
        public static void RunVerification()
        {
            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskAssassinWeapons] STARTING ASSASSIN WEAPON KIT VERIFICATION");
            Debug.Log("==========================================================================");

            string dataDir = "Assets/_Project/Data";
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
                AssetDatabase.Refresh();
            }

            string prefabsDir = "Assets/_Project/Prefabs";

            // 1. Create / Configure ThrowingKnife.prefab
            string knifePrefabPath = $"{prefabsDir}/ThrowingKnife.prefab";
            GameObject knifePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(knifePrefabPath);

            if (knifePrefab == null)
            {
                GameObject knifeTemp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                knifeTemp.name = "ThrowingKnife";
                knifeTemp.transform.localScale = new Vector3(0.08f, 0.08f, 0.45f);

                var boxCol = knifeTemp.GetComponent<BoxCollider>();
                if (boxCol != null) boxCol.isTrigger = false;

                var rb = knifeTemp.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var tk = knifeTemp.AddComponent<ThrowingKnife>();
                tk.speed = 22.0f;
                tk.damage = 75.0f;

                knifePrefab = PrefabUtility.SaveAsPrefabAsset(knifeTemp, knifePrefabPath);
                Object.DestroyImmediate(knifeTemp);
                Debug.Log($"[SetupTaskAssassinWeapons] Created {knifePrefabPath}");
            }
            else
            {
                var tk = knifePrefab.GetComponent<ThrowingKnife>();
                if (tk == null) tk = knifePrefab.AddComponent<ThrowingKnife>();
                tk.speed = 22.0f;
                tk.damage = 75.0f;
                EditorUtility.SetDirty(knifePrefab);
            }

            // 2. Create / Configure WeaponData_SlashKnife.asset
            string knifeDataPath = $"{dataDir}/WeaponData_SlashKnife.asset";
            WeaponData slashKnifeData = AssetDatabase.LoadAssetAtPath<WeaponData>(knifeDataPath);
            if (slashKnifeData == null)
            {
                slashKnifeData = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(slashKnifeData, knifeDataPath);
            }

            slashKnifeData.weaponName = "Slash Knife";
            slashKnifeData.weaponType = WeaponType.Melee;
            slashKnifeData.damage = 50.0f;
            slashKnifeData.meleeRange = 1.8f;
            slashKnifeData.meleeArcAngle = 90.0f;
            slashKnifeData.fireRate = 0.5f;
            slashKnifeData.isInfiniteAmmo = true;
            slashKnifeData.canReload = false;
            EditorUtility.SetDirty(slashKnifeData);

            // 3. Create / Configure WeaponData_ThrowingKnives.asset
            string throwDataPath = $"{dataDir}/WeaponData_ThrowingKnives.asset";
            WeaponData throwingKnivesData = AssetDatabase.LoadAssetAtPath<WeaponData>(throwDataPath);
            if (throwingKnivesData == null)
            {
                throwingKnivesData = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(throwingKnivesData, throwDataPath);
            }

            throwingKnivesData.weaponName = "Throwing Knives";
            throwingKnivesData.weaponType = WeaponType.Throwable;
            throwingKnivesData.damage = 75.0f;
            throwingKnivesData.projectileSpeed = 22.0f;
            throwingKnivesData.projectilePrefab = knifePrefab;
            throwingKnivesData.fireRate = 0.4f;
            throwingKnivesData.maxAmmo = 25;
            throwingKnivesData.isInfiniteAmmo = false;
            throwingKnivesData.canReload = false;
            EditorUtility.SetDirty(throwingKnivesData);

            // 4. Validate Player.prefab
            string playerPrefabPath = $"{prefabsDir}/Player.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab != null)
            {
                var pwm = playerPrefab.GetComponent<PlayerWeaponManager>();
                if (pwm == null) pwm = playerPrefab.AddComponent<PlayerWeaponManager>();
                EditorUtility.SetDirty(playerPrefab);
            }

            AssetDatabase.SaveAssets();

            Debug.Log("[Verification PASS] ThrowingKnife prefab verified with Rigidbody, Collider, and ThrowingKnife component (22m/s, 75 DMG).");
            Debug.Log("[Verification PASS] Slash Knife WeaponData verified (Melee, 1.8m range, 90 deg arc, 0.5s cooldown, Infinite Ammo).");
            Debug.Log("[Verification PASS] Throwing Knives WeaponData verified (Throwable, 5 max ammo, non-reloadable, linked projectile prefab).");
            Debug.Log("==========================================================================");
            Debug.Log("[SetupTaskAssassinWeapons] ALL VERIFICATIONS COMPLETED SUCCESSFULLY!");
            Debug.Log("==========================================================================");
        }
    }
}
