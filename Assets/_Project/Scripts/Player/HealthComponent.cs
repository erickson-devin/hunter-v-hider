using System;
using Unity.Netcode;
using UnityEngine;

namespace HunterVsHider.Player
{
    /// <summary>
    /// Server-authoritative player health component.
    /// Manages networked health variables (MaxHealth, CurrentHealth, IsAlive)
    /// and provides normalized query access for local and observer UI HUDs.
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

            if (IsServer)
            {
                CurrentHealth.Value = MaxHealth.Value;
                IsAlive.Value = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= HandleHealthValueChanged;
            base.OnNetworkDespawn();
        }

        private void HandleHealthValueChanged(float previousValue, float newValue)
        {
            OnHealthChanged?.Invoke(newValue, MaxHealth.Value);
        }

        /// <summary>
        /// Returns the normalized health percentage (0.0 to 1.0) without allowing external mutation.
        /// </summary>
        public float GetHealthNormalized()
        {
            return Mathf.Clamp01(CurrentHealth.Value / Mathf.Max(1f, MaxHealth.Value));
        }
    }
}
