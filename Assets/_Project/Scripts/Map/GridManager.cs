using System.Collections.Generic;
using UnityEngine;

namespace HunterVsHider.Map
{
    public class GridManager : MonoBehaviour
    {
        [Tooltip("The parent object for all walls. MUST have a CompositeCollider2D and a Static Rigidbody2D.")]
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private float _cellSize = 1f;
        
        // The source of truth for our grid
        private Dictionary<Vector2Int, GridCellData> _grid = new Dictionary<Vector2Int, GridCellData>();

        /// <summary>
        /// Converts an arbitrary world position into a snapped grid coordinate.
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector3 worldPosition)
        {
            int x = Mathf.RoundToInt(worldPosition.x / _cellSize);
            int y = Mathf.RoundToInt(worldPosition.y / _cellSize);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Returns the exact world position for the center of a given grid cell.
        /// </summary>
        public Vector3 GridToWorldPosition(Vector2Int gridPosition)
        {
            return new Vector3(gridPosition.x * _cellSize, gridPosition.y * _cellSize, 0f);
        }

        /// <summary>
        /// Checks if a cell is currently occupied.
        /// </summary>
        public bool IsCellEmpty(Vector2Int gridPosition)
        {
            if (_grid.TryGetValue(gridPosition, out GridCellData data))
            {
                return data.Occupant == CellOccupant.Empty;
            }
            return true; // Not in dictionary = empty
        }

        /// <summary>
        /// Places a wall at the specified grid position if it's empty.
        /// </summary>
        public bool PlaceWall(Vector2Int gridPosition, GameObject wallPrefab)
        {
            if (!IsCellEmpty(gridPosition)) return false;

            Vector3 spawnPos = GridToWorldPosition(gridPosition);
            
            // Instantiate as a child of the GridContainer to utilize CompositeCollider2D
            GameObject newWall = Instantiate(wallPrefab, spawnPos, Quaternion.identity, _gridContainer);
            
            // Update grid data
            _grid[gridPosition] = new GridCellData(CellOccupant.Wall, newWall);
            
            return true;
        }

        /// <summary>
        /// Gets the current cell size.
        /// </summary>
        public float GetCellSize() => _cellSize;
    }
}
