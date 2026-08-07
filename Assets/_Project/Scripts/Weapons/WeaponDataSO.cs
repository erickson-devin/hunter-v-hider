using UnityEngine;

namespace HunterVsHider.Weapons
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "HunterVsHider/Weapon Data")]
    public class WeaponDataSO : ScriptableObject
    {
        [Header("Combat Stats")]
        public float damage = 25f;
        public float range = 50f;
        public float fireRate = 0.2f; // Time between shots in seconds

        [Header("Ammo")]
        public int maxAmmo = 30;
        public float reloadTime = 1.5f;
    }
}
