using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HunterVsHider.Player
{
    /// <summary>
    /// Manages client-side visibility culling. Hides enemy meshes and UI when blocked by
    /// walls or outside the local player's field of view, preventing silhouette leaks in memory fog.
    /// </summary>
    [RequireComponent(typeof(PlayerNetworkState))]
    public class PlayerVisibilityManager : NetworkBehaviour
    {
        [Header("Occlusion Layer")]
        public LayerMask obstacleMask = 1 << 7; // Layer 7: Obstacle

        private PlayerNetworkState localState;
        private int visionMaskLayer;

        private void Awake()
        {
            localState = GetComponent<PlayerNetworkState>();
            visionMaskLayer = LayerMask.NameToLayer("VisionMask");
        }

        private void Update()
        {
            // Only the local player calculates line-of-sight against remote players
            if (!IsOwner) return;

            var matchMgr = HunterVsHider.Managers.MatchManager.Instance ?? HunterVsHider.Managers.MatchManager.Singleton;
            bool isNonCombatState = matchMgr == null || 
                                    matchMgr.CurrentState == HunterVsHider.Managers.MatchState.WaitingForPlayers ||
                                    matchMgr.CurrentState == HunterVsHider.Managers.MatchState.PrepPhase ||
                                    matchMgr.CurrentState == HunterVsHider.Managers.MatchState.RoleAssignment;

            var dynFov = GetComponentInChildren<HunterVsHider.Vision.DynamicFOV>(true);
            bool isSuppressed = isNonCombatState || (dynFov != null && dynFov.isVisionMaskSuppressed);

            // Find all active player characters in the scene
            PlayerNetworkState[] allPlayers = Object.FindObjectsByType<PlayerNetworkState>(FindObjectsInactive.Include);

            if (isSuppressed)
            {
                // In Lobby & PrepPhase: full 360 degree 100% mutual visibility bypass
                for (int i = 0; i < allPlayers.Length; i++)
                {
                    PlayerNetworkState other = allPlayers[i];
                    if (other == null) continue;
                    SetPlayerVisualsVisible(other.gameObject, true);
                }
                return;
            }

            // In CombatPhase: evaluate dynamic vision cones and line-of-sight
            float viewRadius = (dynFov != null) ? dynFov.viewRadius : 15f;
            float viewAngle = (dynFov != null) ? dynFov.viewAngle : 90f;
            float proximityRadius = (dynFov != null) ? dynFov.proximityRadius : 2.5f;

            if (localState != null && localState.Role == PlayerRole.Assassin)
            {
                viewRadius = (dynFov != null) ? dynFov.viewRadius : 12f;
                viewAngle = 360f;
                proximityRadius = (dynFov != null) ? dynFov.proximityRadius : 12f;
            }

            Vector3 localEye = transform.position + Vector3.up * 1.0f;
            Vector3 forward = transform.forward;

            for (int i = 0; i < allPlayers.Length; i++)
            {
                PlayerNetworkState other = allPlayers[i];
                if (other == null || other == localState) continue;

                Vector3 targetPos = other.transform.position;
                Vector3 targetEye = targetPos + Vector3.up * 1.0f;
                Vector3 diff = targetPos - transform.position;
                float dist = diff.magnitude;

                bool isVisible = false;

                if (dist <= proximityRadius)
                {
                    // Within immediate 360-degree proximity radius: check line-of-sight
                    if (!Physics.Linecast(localEye, targetEye, obstacleMask))
                    {
                        isVisible = true;
                    }
                }
                else if (dist <= viewRadius)
                {
                    // Within view cone: check angle then line-of-sight
                    Vector3 flatDiff = diff;
                    flatDiff.y = 0f;
                    float angle = Vector3.Angle(forward, flatDiff);

                    if (angle <= viewAngle * 0.5f)
                    {
                        if (!Physics.Linecast(localEye, targetEye, obstacleMask))
                        {
                            isVisible = true;
                        }
                    }
                }

                // Apply visibility state to the other player's visible renderers & UI
                SetPlayerVisualsVisible(other.gameObject, isVisible);
            }
        }

        private void SetPlayerVisualsVisible(GameObject targetPlayer, bool isVisible)
        {
            Renderer[] renderers = targetPlayer.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                // Do not interfere with VisionMask layer objects (like the FOV mesh)
                if (r.gameObject.layer == visionMaskLayer) continue;

                if (r.enabled != isVisible)
                {
                    r.enabled = isVisible;
                }
            }

            Canvas[] canvases = targetPlayer.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].enabled != isVisible)
                {
                    canvases[i].enabled = isVisible;
                }
            }
        }
    }
}
