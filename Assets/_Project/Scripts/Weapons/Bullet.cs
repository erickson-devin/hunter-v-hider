using UnityEngine;

namespace HunterVsHider.Weapons
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet : MonoBehaviour
    {
        [Header("Bullet Settings")]
        public float speed = 20f;
        public float lifetime = 3f;

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
            // Move forward
            rb.velocity = transform.right * speed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Destroy on collision
            Destroy(gameObject);
        }
    }
}
