using UnityEngine;
using HunterVsHider.Weapons;

namespace HunterVsHider.Player
{
    public class PlayerWeaponManager : MonoBehaviour
    {
        [SerializeField] private Gun activeGun;

        private void Awake()
        {
            if (activeGun == null)
            {
                activeGun = GetComponentInChildren<Gun>();
            }
        }

        private void Update()
        {
            if (activeGun == null) return;

            // Handle Firing (Holding down button for automatic fire)
            if (Input.GetButton("Fire1"))
            {
                activeGun.Shoot();
            }

            // Handle Reloading
            if (Input.GetKeyDown(KeyCode.R))
            {
                activeGun.Reload();
            }
        }
    }
}
