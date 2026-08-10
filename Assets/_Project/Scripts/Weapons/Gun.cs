using UnityEngine;
using System.Collections;

namespace HunterVsHider.Weapons
{
    public class Gun : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform muzzlePoint;

        [Header("Gun Stats")]
        [SerializeField] private int maxAmmo = 30;
        [SerializeField] private float fireRate = 0.15f;
        [SerializeField] private float reloadTime = 2.0f;

        private int currentAmmo;
        private float nextFireTime = 0f;
        private bool isReloading = false;

        private void Start()
        {
            currentAmmo = maxAmmo;
            
            if (muzzlePoint == null)
            {
                muzzlePoint = transform.Find("MuzzlePoint");
            }
        }

        public void TryFire()
        {
            Shoot();
        }

        public void Shoot()
        {
            if (isReloading || currentAmmo <= 0 || Time.time < nextFireTime)
            {
                return;
            }

            if (muzzlePoint == null)
            {
                Debug.LogWarning("MuzzlePoint is missing on Gun!");
                return;
            }

            // Update state
            nextFireTime = Time.time + fireRate;
            currentAmmo--;

            // Perform 3D Raycast
            Ray ray = new Ray(muzzlePoint.position, muzzlePoint.forward);
            int layerMask = ~(1 << 6); // Ignore Player layer
            float range = 50f;

            if (Physics.Raycast(ray, out RaycastHit hit, range, layerMask))
            {
                // Optional hit logic here
            }

            // Visualize the ray
            Debug.DrawRay(muzzlePoint.position, muzzlePoint.forward * range, Color.red, 2.0f);
            
            // Console output
            Debug.Log($"[{gameObject.name}] Fired | Ammo: {currentAmmo}/{maxAmmo}");
        }

        public void Reload()
        {
            if (isReloading || currentAmmo >= maxAmmo)
            {
                return;
            }

            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            isReloading = true;
            Debug.Log("Reloading...");

            yield return new WaitForSeconds(reloadTime);

            currentAmmo = maxAmmo;
            isReloading = false;
            Debug.Log($"Reload Complete! Ammo: {currentAmmo}/{maxAmmo}");
        }
    }
}
