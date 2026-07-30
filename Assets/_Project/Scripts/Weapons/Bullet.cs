using UnityEngine;
using HunterVsHider.Player;

namespace HunterVsHider.Weapons
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet : MonoBehaviour
    {
        [Header("Bullet Settings")]
        public float speed = 20f;
        public float lifetime = 3f;
        public int damage = 10;

        private Rigidbody2D rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            // Destroy after lifetime
            Destroy(gameObject, lifetime);
        }

        private void FixedUpdate()
        {
            // Move forward (using 'up' instead of 'right' for 2D top-down)
            rb.linearVelocity = transform.up * speed;
        }

        // CRITICAL FIX: Changed from OnCollisionEnter2D to OnTriggerEnter2D
        private void OnTriggerEnter2D(Collider2D collision)
        {
            // Prevent the bullet from destroying itself if it touches the Player!
            if (collision.gameObject.CompareTag("Player"))
            {
                return;
            }

            // Apply damage if possible
            if (collision.gameObject.TryGetComponent(out Health health))
            {
                health.TakeDamage(damage);
            }

            // Destroy on collision
            Destroy(gameObject);
        }
    }
}