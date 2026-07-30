using UnityEngine;

public class Shooter : MonoBehaviour
{
    [Header("Weapon Setup")]
    public Transform firePoint;     // Where the bullet spawns
    public GameObject bulletPrefab; // The bullet blueprint
    public float bulletForce = 20f; // How fast the bullet flies

    void Update()
    {
        // Fire1 is typically the Left Mouse Button or Left Ctrl
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        // Safety check to prevent errors
        if (firePoint == null || bulletPrefab == null) 
        {
            Debug.LogWarning("Shooter script is missing the FirePoint or BulletPrefab in the Inspector!");
            return;
        }

        // 1. Create the bullet at the firePoint's exact position and rotation
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // Note: We no longer need to add force here, because the Antigravity Agent
        // updated Bullet.cs to handle its own movement in FixedUpdate using rb.linearVelocity!
    }
}