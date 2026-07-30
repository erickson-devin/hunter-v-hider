using UnityEngine;

namespace HunterVsHider.Weapons
{
    public class Shooter : MonoBehaviour
    {
        [Header("Shooting Settings")]
        public GameObject projectilePrefab;
        public Transform firePoint;
        public float fireRate = 0.5f;

        private float nextFireTime = 0f;

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) && Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + fireRate;
            }
        }

        private void Shoot()
        {
            if (projectilePrefab != null && firePoint != null)
            {
                Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            }
            else
            {
                Debug.LogWarning("Projectile Prefab or Fire Point is not assigned on " + gameObject.name);
            }
        }
    }
}
