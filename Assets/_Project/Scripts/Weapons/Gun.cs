using UnityEngine;

namespace HunterVsHider.Weapons
{
    public class Gun : MonoBehaviour
    {
        [Header("Configuration")]
        public WeaponDataSO weaponData;
        public Transform firePoint;

        private float nextFireTime = 0f;
        private int currentAmmo;
        private bool isReloading = false;

        private void Start()
        {
            if (weaponData != null)
            {
                currentAmmo = weaponData.maxAmmo;
            }
        }

        public void TryFire()
        {
            if (isReloading || weaponData == null) return;

            if (currentAmmo <= 0)
            {
                StartCoroutine(ReloadRoutine());
                return;
            }

            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + weaponData.fireRate;
                Fire();
            }
        }

        private void Fire()
        {
            currentAmmo--;
            
            // Perform 3D Raycast
            Ray ray = new Ray(firePoint.position, firePoint.forward);
            
            // We want to hit everything EXCEPT the Player. 
            // Player is layer 6. So we can use a LayerMask that ignores layer 6.
            int layerMask = ~(1 << 6);

            if (Physics.Raycast(ray, out RaycastHit hit, weaponData.range, layerMask))
            {
                Debug.Log($"[Gun] Hit {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}!");
                
                // TODO: Apply damage if the target has health
                // if (hit.collider.TryGetComponent(out Health targetHealth)) { targetHealth.TakeDamage(weaponData.damage); }
            }
            else
            {
                Debug.Log("[Gun] Fired, but hit nothing.");
            }
        }

        private System.Collections.IEnumerator ReloadRoutine()
        {
            isReloading = true;
            Debug.Log("[Gun] Reloading...");
            
            yield return new WaitForSeconds(weaponData.reloadTime);
            
            currentAmmo = weaponData.maxAmmo;
            isReloading = false;
            Debug.Log("[Gun] Reload complete.");
        }
    }
}
