using System.Collections.Generic;
using UnityEngine;

namespace HunterVsHider.Map
{
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [Tooltip("The parent object for all walls.")]
        public Transform mapParent;
        [SerializeField] private float _cellSize = 1f;

        [Header("Procedural Police Breach Staging")]
        [Tooltip("Center coordinates of procedurally constructed south Police Breach Rooms.")]
        public List<Vector3> PoliceBreachSpawnPositions { get; private set; } = new List<Vector3>();

        [Tooltip("Display names of procedurally constructed south Police Breach Rooms (e.g. Breach Alpha, Bravo, Charlie).")]
        public List<string> PoliceBreachRoomNames { get; private set; } = new List<string>();

        // The source of truth for 2D cell grid
        private Dictionary<Vector2Int, GridCellData> _grid = new Dictionary<Vector2Int, GridCellData>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Populate fallback breach positions if empty
            if (PoliceBreachSpawnPositions == null || PoliceBreachSpawnPositions.Count == 0)
            {
                RegisterBreachRooms(GetBreachSpawnPositions(50), GetBreachRoomNames(50));
            }
        }

        /// <summary>
        /// Registers or updates active breach room spawn positions and display names.
        /// </summary>
        public void RegisterBreachRooms(List<Vector3> positions, List<string> names)
        {
            PoliceBreachSpawnPositions = positions != null ? new List<Vector3>(positions) : new List<Vector3>();
            PoliceBreachRoomNames = names != null ? new List<string>(names) : new List<string>();
        }

        public void ClearBreachRooms()
        {
            PoliceBreachSpawnPositions.Clear();
            PoliceBreachRoomNames.Clear();
        }

        /// <summary>
        /// Calculates the procedural 4m x 4m Breach Room center coordinates along the South perimeter (Z ≈ -25m).
        /// </summary>
        public static List<Vector3> GetBreachSpawnPositions(int mapSize = 50)
        {
            int size = MapGenerator.ClampMapSize(mapSize);
            float halfSize = size * 0.5f;
            float roomZ = -halfSize - 2.0f; // 2m south of perimeter wall, centered inside 4m deep room

            List<Vector3> positions = new List<Vector3>();

            if (size == 50)
            {
                // 3 Breach Rooms across 50m arena: Alpha (-15m), Bravo (0m), Charlie (+15m)
                positions.Add(new Vector3(-15.0f, 1.0f, roomZ));
                positions.Add(new Vector3(0.0f, 1.0f, roomZ));
                positions.Add(new Vector3(15.0f, 1.0f, roomZ));
            }
            else if (size == 100)
            {
                // 3 Breach Rooms across 100m arena: Alpha (-30m), Bravo (0m), Charlie (+30m)
                positions.Add(new Vector3(-30.0f, 1.0f, roomZ));
                positions.Add(new Vector3(0.0f, 1.0f, roomZ));
                positions.Add(new Vector3(30.0f, 1.0f, roomZ));
            }
            else // 150
            {
                // 3 Breach Rooms across 150m arena: Alpha (-45m), Bravo (0m), Charlie (+45m)
                positions.Add(new Vector3(-45.0f, 1.0f, roomZ));
                positions.Add(new Vector3(0.0f, 1.0f, roomZ));
                positions.Add(new Vector3(45.0f, 1.0f, roomZ));
            }

            return positions;
        }

        /// <summary>
        /// Returns tactical callsign labels for the available breach rooms.
        /// </summary>
        public static List<string> GetBreachRoomNames(int mapSize = 50)
        {
            return new List<string>
            {
                "Breach Alpha",
                "Breach Bravo",
                "Breach Charlie"
            };
        }

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
            
            GameObject newWall;
            if (mapParent != null)
            {
                newWall = Instantiate(wallPrefab, spawnPos, Quaternion.identity, mapParent);
            }
            else
            {
                newWall = Instantiate(wallPrefab, spawnPos, Quaternion.identity);
            }
            
            _grid[gridPosition] = new GridCellData(CellOccupant.Wall, newWall);
            return true;
        }

        /// <summary>
        /// Gets the current cell size.
        /// </summary>
        public float GetCellSize() => _cellSize;
    }
}
