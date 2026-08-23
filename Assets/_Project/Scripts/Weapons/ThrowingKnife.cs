using UnityEngine;
using Unity.Netcode;

namespace HunterVsHider.Weapons
{
    [RequireComponent(typeof(Collider))]
    public class ThrowingKnife : MonoBehaviour
    {
        [Header("Knife Stats")]
        public float speed = 22.0f;
        public float damage = 75.0f;
        public float maxLifetime = 15.0f;

        [Header("Audio Settings")]
        [SerializeField] private AudioClip impactSFX;

        private Rigidbody rb;
        private Collider knifeCollider;
        private bool hasStuck = false;
        private AudioSource audioSource;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            knifeCollider = GetComponent<Collider>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }

            if (impactSFX == null)
            {
                impactSFX = CreateSynthImpact("KnifeImpact", 300f, 0.1f);
            }
        }

        private void Start()
        {
            if (rb != null && !hasStuck)
            {
                rb.linearVelocity = transform.forward * speed;
            }

            Destroy(gameObject, maxLifetime);
        }

        public void Launch(Vector3 direction, float launchSpeed, float knifeDamage)
        {
            speed = launchSpeed;
            damage = knifeDamage;
            transform.forward = direction;

            if (rb != null && !hasStuck)
            {
                rb.linearVelocity = direction.normalized * speed;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasStuck) return;

            GameObject hitObj = collision.gameObject;

            // Check for IDamageable (Player / Enemy)
            IDamageable damageable = hitObj.GetComponent<IDamageable>();
            if (damageable == null) damageable = hitObj.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                StickToTarget(collision.transform, collision.contacts[0].point);
                PlayImpactAudio();
                return;
            }

            // Check for Obstacle / Wall
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (hitObj.layer == obstacleLayer || hitObj.CompareTag("Obstacle") || hitObj.name.Contains("TacticalWall") || hitObj.name.Contains("Wall"))
            {
                Vector3 contactPoint = (collision.contactCount > 0) ? collision.contacts[0].point : transform.position;
                StickToTarget(collision.transform, contactPoint);
                PlayImpactAudio();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasStuck) return;

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null) damageable = other.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                StickToTarget(other.transform, transform.position);
                PlayImpactAudio();
            }
        }

        private void StickToTarget(Transform parentTarget, Vector3 contactPoint)
        {
            hasStuck = true;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            if (knifeCollider != null)
            {
                knifeCollider.enabled = false;
            }

            transform.position = contactPoint;
            if (parentTarget != null)
            {
                transform.SetParent(parentTarget, true);
            }
        }

        private void PlayImpactAudio()
        {
            if (audioSource != null && impactSFX != null)
            {
                audioSource.PlayOneShot(impactSFX, 0.7f);
            }
        }

        private AudioClip CreateSynthImpact(string name, float freq, float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                data[i] = Mathf.Sin(2 * Mathf.PI * freq * i / sampleRate) * (1f - ((float)i / samples));
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
