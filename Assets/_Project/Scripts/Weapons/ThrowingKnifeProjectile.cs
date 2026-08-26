using System.Collections;
using Unity.Netcode;
using UnityEngine;
using HunterVsHider.Player;

namespace HunterVsHider.Weapons
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowingKnifeProjectile : NetworkBehaviour
    {
        [Header("Projectile Stats")]
        [SerializeField] private float throwSpeed = 20f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float maxLifetime = 5f;

        [Header("Impact FX")]
        [SerializeField] private AudioClip impactSFX;

        public ulong ownerClientId;
        private Rigidbody rb;
        private bool hasHit = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (rb != null)
            {
                rb.linearVelocity = transform.forward * throwSpeed;
            }

            if (IsServer)
            {
                StartCoroutine(DespawnAfterLifetime(maxLifetime));
            }
        }

        public void Initialize(ulong attackerId, float speed = 20f, float dmg = 10f)
        {
            ownerClientId = attackerId;
            throwSpeed = speed;
            damage = dmg;
        }

        private IEnumerator DespawnAfterLifetime(float lifetime)
        {
            yield return new WaitForSeconds(lifetime);
            if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || hasHit) return;

            ProcessImpact(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || hasHit) return;

            ProcessImpact(other.gameObject);
        }

        private void ProcessImpact(GameObject hitObj)
        {
            if (hitObj == null) return;

            // Ignore hitting the thrower
            var netObj = hitObj.GetComponent<NetworkObject>() ?? hitObj.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == ownerClientId)
            {
                return;
            }

            hasHit = true;

            // Check for HealthComponent on target
            var targetHealth = hitObj.GetComponent<HealthComponent>() ?? hitObj.GetComponentInParent<HealthComponent>();
            if (targetHealth != null)
            {
                targetHealth.ApplyDamageServerSide(damage);
                Debug.Log($"[ThrowingKnife] Hit player {targetHealth.name} for {damage} HP damage.");
            }

            // Despawn projectile on server
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
    }
}
