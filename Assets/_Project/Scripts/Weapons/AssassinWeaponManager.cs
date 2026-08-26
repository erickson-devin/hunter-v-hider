using Unity.Netcode;
using UnityEngine;
using HunterVsHider.Player;

namespace HunterVsHider.Weapons
{
    public class AssassinWeaponManager : NetworkBehaviour
    {
        /// <summary>
        /// Instantaneous hitscan throwing knife attack with zero physics impulses / knockback.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ExecuteThrowingKnifeRaycastServerRpc(Vector3 origin, Vector3 aimDirection)
        {
            float maxKnifeRange = 15f;
            LayerMask hitLayers = LayerMask.GetMask("Obstacle", "Player");
            if (hitLayers == 0) hitLayers = ~LayerMask.GetMask("Ignore Raycast", "UI");

            if (Physics.Raycast(origin, aimDirection.normalized, out RaycastHit hit, maxKnifeRange, hitLayers, QueryTriggerInteraction.Ignore))
            {
                // If obstacle wall is hit first, terminate line trace (no damage behind cover)
                if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Obstacle") || hit.collider.CompareTag("Obstacle") || hit.collider.gameObject.layer == 7)
                {
                    Debug.Log("[THROWING KNIFE] Hit wall obstacle. Attack terminated.");
                    return;
                }

                // If player entity is hit, apply clean 10 HP damage without physics impulses
                var targetHealth = hit.collider.GetComponent<HealthComponent>() ?? hit.collider.GetComponentInParent<HealthComponent>();
                if (targetHealth != null && targetHealth.gameObject != gameObject)
                {
                    targetHealth.TakeDamageServerRpc(10f); // 10 HP Damage per knife hit
                    Debug.Log($"[THROWING KNIFE HIT] Applied 10 HP to target. Current HP: {targetHealth.CurrentHealth.Value}");
                }
            }
        }
    }
}
