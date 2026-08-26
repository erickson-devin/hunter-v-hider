using System;
using Unity.Netcode;
using UnityEngine;

namespace HunterVsHider.Player
{
    /// <summary>
    /// Server-authoritative player health component.
    /// Manages networked health variables (MaxHealth, CurrentHealth, IsAlive),
    /// damage calculation via ServerRpc, and client-side elimination state visuals.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class HealthComponent : NetworkBehaviour
    {
        [Header("Synchronized Health State")]
        [Tooltip("Maximum health value synchronized from Server to all clients.")]
        public NetworkVariable<float> MaxHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        [Tooltip("Current health value synchronized from Server to all clients.")]
        public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        [Tooltip("Alive status synchronized from Server to all clients.")]
        public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Public query properties
        public float HealthValue => CurrentHealth.Value;
        public float MaxHealthValue => MaxHealth.Value;
        public bool IsPlayerAlive => IsAlive.Value;

        /// <summary>
        /// Event dispatched on clients when the synchronized health value changes (current, max).
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            CurrentHealth.OnValueChanged += HandleHealthValueChanged;
            IsAlive.OnValueChanged += HandleAliveValueChanged;

            if (IsServer)
            {
                CurrentHealth.Value = MaxHealth.Value;
                IsAlive.Value = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= HandleHealthValueChanged;
            IsAlive.OnValueChanged -= HandleAliveValueChanged;
            base.OnNetworkDespawn();
        }

        private void HandleHealthValueChanged(float previousValue, float newValue)
        {
            OnHealthChanged?.Invoke(newValue, MaxHealth.Value);
        }

        private void HandleAliveValueChanged(bool previousValue, bool isAlive)
        {
            if (!isAlive)
            {
                ApplyDeathState();
            }
            else
            {
                ApplyReviveState();
            }
        }

        /// <summary>
        /// Returns the normalized health percentage (0.0 to 1.0) without allowing external mutation.
        /// </summary>
        public float GetHealthNormalized()
        {
            return Mathf.Clamp01(CurrentHealth.Value / Mathf.Max(1f, MaxHealth.Value));
        }

        /// <summary>
        /// Applies damage directly on the server to CurrentHealth and triggers death if health reaches 0.
        /// </summary>
        public void ApplyDamageServerSide(float amount)
        {
            if (!IsServer || !IsAlive.Value || amount <= 0f) return;

            float newHealth = Mathf.Max(0f, CurrentHealth.Value - amount);
            CurrentHealth.Value = newHealth;
            Debug.Log($"[DAMAGE APPLIED] Target current health: {CurrentHealth.Value} HP");

            if (newHealth <= 0f && IsAlive.Value)
            {
                CurrentHealth.Value = 0f;
                IsAlive.Value = false;

                Debug.Log($"[HealthComponent] Player ClientId {OwnerClientId} eliminated!");
                OnDeathClientRpc();
            }
        }

        /// <summary>
        /// Server-authoritative direct raycast damage calculation and elimination trigger.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void TakeDamageServerRpc(float amount)
        {
            if (!IsAlive.Value || amount <= 0f) return;

            ApplyDamageServerSide(amount);
        }

        /// <summary>
        /// Invoked across all clients and host when this player entity dies.
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void OnDeathClientRpc()
        {
            ApplyDeathState();
        }

        public void ApplyDeathState()
        {
            // 1. Disable local movement inputs
            var playerMovement = GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.SetMovementEnabled(false);
                playerMovement.enabled = false;
            }
            var playerController = GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            // 2. Disable local weapon firing and hide active weapon visuals
            var weaponManager = GetComponent<PlayerWeaponManager>();
            if (weaponManager != null)
            {
                weaponManager.enabled = false;
                weaponManager.HideAllWeaponVisuals();
            }

            // 3. Disable main character collider so dead entities do not block bullets or movement
            var playerCollider = GetComponent<Collider>();
            if (playerCollider != null)
            {
                playerCollider.enabled = false;
            }

            // 4. Set local character visual mesh opacity/alpha to 30% translucent to indicate elimination
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null || r.material == null) continue;

                if (r.material.HasProperty("_Color"))
                {
                    Color c = r.material.color;
                    r.material.color = new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 0.3f);
                }
            }

            Debug.Log($"[HealthComponent] Applied death state on ClientId {OwnerClientId}");
        }

        public void ApplyReviveState()
        {
            var playerMovement = GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.enabled = true;
                playerMovement.SetMovementEnabled(true);
            }
            var playerController = GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = true;
            }
            var weaponManager = GetComponent<PlayerWeaponManager>();
            if (weaponManager != null)
            {
                weaponManager.enabled = true;
                weaponManager.ShowActiveWeaponVisual();
            }
            var playerCollider = GetComponent<Collider>();
            if (playerCollider != null)
            {
                playerCollider.enabled = true;
            }

            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null || r.material == null) continue;
                if (r.material.HasProperty("_Color"))
                {
                    Color c = r.material.color;
                    r.material.color = new Color(Mathf.Min(1f, c.r / 0.3f), Mathf.Min(1f, c.g / 0.3f), Mathf.Min(1f, c.b / 0.3f), 1f);
                }
            }
        }
    }
}
