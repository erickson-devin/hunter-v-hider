using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace HunterVsHider.Vision
{
    public class VisionManager : NetworkBehaviour
    {
        public static VisionManager Instance { get; private set; }

        [Header("Grid Settings")]
        public float cellSize = 1f;
        public int gridWidth = 100;
        public int gridHeight = 100;

        [Header("Vision State")]
        // For a true 3-tier fog of war (Unexplored, Explored, Visible)
        // We'll stub this out to be handled server-side
        private byte[,] visionGrid; 
        
        // List of currently visible enemy positions to sync to the client
        private NetworkList<Vector3> visibleEnemyPositions;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            visibleEnemyPositions = new NetworkList<Vector3>();
        }

        /// <summary>
        /// Evaluates if the target entity is within the attacker's dynamic FOV cone and unoccluded by obstacles.
        /// </summary>
        public bool IsEntityInLocalFOV(Transform attackerTransform, Transform targetTransform)
        {
            if (attackerTransform == null || targetTransform == null) return false;

            var dynFov = attackerTransform.GetComponentInChildren<DynamicFOV>(true);
            float viewAngle = (dynFov != null) ? dynFov.viewAngle : 90f;
            float viewRadius = (dynFov != null) ? dynFov.viewRadius : 20f;
            float proxRadius = (dynFov != null) ? dynFov.proximityRadius : 2.5f;

            var playerState = attackerTransform.GetComponent<HunterVsHider.Player.PlayerNetworkState>();
            if (playerState != null && playerState.Role == HunterVsHider.Player.PlayerRole.Assassin)
            {
                viewAngle = 360f;
                viewRadius = (dynFov != null) ? dynFov.viewRadius : 15f;
                proxRadius = 15f;
            }

            Vector3 attackerEye = attackerTransform.position + Vector3.up * 1.0f;
            Vector3 targetEye = targetTransform.position + Vector3.up * 1.0f;
            Vector3 diff = targetTransform.position - attackerTransform.position;
            float dist = diff.magnitude;

            int obstacleMask = 1 << 7; // Layer 7: Obstacle

            if (dist <= proxRadius)
            {
                return !Physics.Linecast(attackerEye, targetEye, obstacleMask);
            }

            if (dist <= viewRadius)
            {
                Vector3 flatDiff = diff;
                flatDiff.y = 0f;
                float angle = Vector3.Angle(attackerTransform.forward, flatDiff);

                if (angle <= viewAngle * 0.5f)
                {
                    return !Physics.Linecast(attackerEye, targetEye, obstacleMask);
                }
            }

            return false;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                InitializeGrid();
            }
        }

        private void InitializeGrid()
        {
            visionGrid = new byte[gridWidth, gridHeight];
            // 0 = Unexplored, 1 = Explored (no enemies, gray), 2 = Visible (active vision)
        }

        private void Update()
        {
            if (!IsServer) return;

            // TODO: Update vision grid based on player positions and line of sight
            // TODO: Sync visible enemy positions to clients that should see them
        }

        /// <summary>
        /// Example method the server would use to grant vision of an area.
        /// </summary>
        public void RevealArea(Vector3 worldPosition, float radius)
        {
            if (!IsServer) return;
            // Map world space to grid space and update visionGrid
        }
    }
}
