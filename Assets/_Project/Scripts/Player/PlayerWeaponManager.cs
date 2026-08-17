using UnityEngine;
using HunterVsHider.Weapons;

namespace HunterVsHider.Player
{
    public class PlayerWeaponManager : MonoBehaviour
    {
        [SerializeField] private Gun pistol;
        [SerializeField] private Gun rifle;

        private Gun activeGun;

        public Gun ActiveGun => activeGun;
        public Transform ActiveMuzzlePoint => activeGun != null ? activeGun.MuzzlePoint : null;

        private void Awake()
        {
            Transform holder = transform.Find("WeaponHolder");
            if (holder != null)
            {
                Transform p = holder.Find("Gun_Pistol");
                Transform r = holder.Find("Gun_Rifle");
                if (p != null) pistol = p.GetComponent<Gun>();
                if (r != null) rifle = r.GetComponent<Gun>();
            }
        }

        private void Start()
        {
            if (pistol != null)
            {
                pistol.gameObject.SetActive(true);
                if (rifle != null) rifle.gameObject.SetActive(false);
                activeGun = pistol;
            }
        }

        private void Update()
        {
            // Weapon Swapping
            if (Input.GetKeyDown(KeyCode.Alpha1) && pistol != null)
            {
                EquipWeapon(pistol, rifle);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) && rifle != null)
            {
                EquipWeapon(rifle, pistol);
            }

            if (activeGun == null) return;

            // Firing & Reloading Input
            if (Input.GetButton("Fire1"))
            {
                activeGun.Shoot();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                activeGun.Reload();
            }
        }

        private void EquipWeapon(Gun toEnable, Gun toDisable)
        {
            if (toDisable != null) toDisable.gameObject.SetActive(false);
            if (toEnable != null) toEnable.gameObject.SetActive(true);
            activeGun = toEnable;
            Debug.Log($"Equipped: {activeGun.gameObject.name}");
        }
    }
}
