using UnityEngine;
using HunterVsHider.Weapons;

namespace HunterVsHider.Player
{
    public class PlayerWeaponManager : MonoBehaviour
    {
        [SerializeField] private Gun pistol;
        [SerializeField] private Gun rifle;

        private Gun activeGun;

        private void Start()
        {
            activeGun = pistol;
            if (pistol != null) pistol.gameObject.SetActive(true);
            if (rifle != null) rifle.gameObject.SetActive(false);
            Debug.Log("Equipped Pistol");
        }

        private void Update()
        {
            HandleWeaponSwapping();

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

        private void HandleWeaponSwapping()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) && activeGun != pistol)
            {
                if (pistol != null) pistol.gameObject.SetActive(true);
                if (rifle != null) rifle.gameObject.SetActive(false);
                activeGun = pistol;
                Debug.Log("Equipped Pistol");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) && activeGun != rifle)
            {
                if (pistol != null) pistol.gameObject.SetActive(false);
                if (rifle != null) rifle.gameObject.SetActive(true);
                activeGun = rifle;
                Debug.Log("Equipped Rifle");
            }
        }
    }
}
