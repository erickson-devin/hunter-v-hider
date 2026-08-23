using UnityEngine;

namespace HunterVsHider.Weapons
{
    public enum WeaponType
    {
        Firearm,
        Melee,
        Throwable
    }

    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "HunterVsHider/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("General Settings")]
        public string weaponName = "Weapon";
        public WeaponType weaponType = WeaponType.Firearm;

        [Header("Combat Stats")]
        [Tooltip("Damage applied per hit/projectile.")]
        public float damage = 25f;

        [Tooltip("Effective maximum range for firearms.")]
        public float range = 50f;

        [Tooltip("Attack cooldown interval in seconds (e.g. 0.5s for melee swing).")]
        public float fireRate = 0.2f;

        [Header("Shotgun Pellet Spread")]
        [Tooltip("Number of raycast pellets fired per shot (1 for Rifle/Pistol, 8 for Shotgun).")]
        public int pellets = 1;

        [Tooltip("Angular spread cone in degrees for multi-pellet spread.")]
        public float spreadAngle = 0.0f;

        [Header("Dynamic Vision Cone Settings")]
        [Tooltip("Field of view cone angle in degrees (Rifle: 45 deg, Shotgun: 110 deg, Pistol: 75 deg).")]
        public float viewAngle = 75.0f;

        [Tooltip("Maximum vision/light beam distance in meters (Rifle: 24m, Shotgun: 10m, Pistol: 16m).")]
        public float viewDistance = 16.0f;

        [Header("Melee Settings")]
        [Tooltip("Effective reach for melee strike in meters.")]
        public float meleeRange = 1.8f;

        [Tooltip("Cone angle in front of attacker in degrees (e.g. 90 deg).")]
        public float meleeArcAngle = 90.0f;

        [Header("Throwable Settings")]
        [Tooltip("Prefab instantiated and launched when thrown.")]
        public GameObject projectilePrefab;

        [Tooltip("Initial linear velocity speed in m/s.")]
        public float projectileSpeed = 22.0f;

        [Header("Ammo & Reload Settings")]
        [Tooltip("Maximum capacity of magazine/stock.")]
        public int maxAmmo = 30;

        [Tooltip("If true, ammo is not consumed and never runs out.")]
        public bool isInfiniteAmmo = false;

        [Tooltip("If true, reloading always restores full magazine without tracking finite reserves.")]
        public bool isInfiniteReserve = true;

        [Tooltip("If false, weapon cannot be reloaded (e.g. throwing knives).")]
        public bool canReload = true;

        [Tooltip("Reload duration in seconds.")]
        public float reloadTime = 1.5f;

        [Header("Audio Settings")]
        public AudioClip attackSFX;
        public AudioClip reloadSFX;
        public AudioClip dryFireSFX;
        public AudioClip impactSFX;
    }
}
