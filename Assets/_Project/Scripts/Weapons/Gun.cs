using UnityEngine;

namespace HunterVsHider.Weapons
{
    public class Gun : MonoBehaviour
    {
        [Header("Configuration")]
        public WeaponDataSO weaponData;
        
        [SerializeField] private Transform muzzlePoint;

        private float nextFireTime = 0f;
        private int currentAmmo;
        private bool isReloading = false;

        private void Start()
        {
            if (weaponData != null)
            {
                currentAmmo = weaponData.maxAmmo;
            }

            if (muzzlePoint == null)
            {
                muzzlePoint = transform.Find("MuzzlePoint");
            }
        }

        private void Update()
        {
            if (Input.GetButtonDown("Fire1"))
            {
                if (weaponData != null)
                {
                    TryFire();
                }
                else
                {
                    Shoot();
                }
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
                currentAmmo--;
                Shoot();
            }
        }

        public void Shoot()
        {
            if (muzzlePoint == null)
            {
                Debug.LogWarning("MuzzlePoint is missing on Gun!");
                return;
            }

            // Perform 3D Raycast
            Ray ray = new Ray(muzzlePoint.position, muzzlePoint.forward);
            int layerMask = ~(1 << 6); // Ignore Player layer

            float range = weaponData != null ? weaponData.range : 50f;

            if (Physics.Raycast(ray, out RaycastHit hit, range, layerMask))
            {
                Debug.Log($"[Gun] Hit {hit.collider.name}");
            }

            // Visualize the ray
            Debug.DrawRay(muzzlePoint.position, muzzlePoint.forward * range, Color.red, 2.0f);
            Debug.Log("Pistol Fired from: " + muzzlePoint.position);
        }

        private System.Collections.IEnumerator ReloadRoutine()
        {
            isReloading = true;
            Debug.Log("[Gun] Reloading...");
            
            float reloadTime = weaponData != null ? weaponData.reloadTime : 2f;
            yield return new WaitForSeconds(reloadTime);
            
            if (weaponData != null) currentAmmo = weaponData.maxAmmo;
            isReloading = false;
            Debug.Log("[Gun] Reload complete.");
        }
    }
}
