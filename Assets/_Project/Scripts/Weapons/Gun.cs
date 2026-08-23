using UnityEngine;

namespace HunterVsHider.Weapons
{
    public class Gun : Weapon
    {
        [Header("Legacy Gun Stats")]
        [SerializeField] private int legacyMaxAmmo = 30;
        [SerializeField] private float legacyFireRate = 0.15f;
        [SerializeField] private float legacyReloadTime = 2.0f;
        [SerializeField] private float legacyMaxRange = 50f;
        [SerializeField] private float legacyDamage = 25f;

        protected override void Awake()
        {
            if (weaponData == null)
            {
                weaponData = ScriptableObject.CreateInstance<WeaponData>();
                weaponData.weaponName = gameObject.name;
                weaponData.weaponType = WeaponType.Firearm;
                weaponData.maxAmmo = legacyMaxAmmo;
                weaponData.fireRate = legacyFireRate;
                weaponData.reloadTime = legacyReloadTime;
                weaponData.range = legacyMaxRange;
                weaponData.damage = legacyDamage;
                weaponData.isInfiniteReserve = true;
                weaponData.canReload = true;
            }

            base.Awake();
        }
    }
}
