using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace HunterVsHider.Vision
{
    public class VisionManager : NetworkBehaviour
    {
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
            visibleEnemyPositions = new NetworkList<Vector3>();
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
