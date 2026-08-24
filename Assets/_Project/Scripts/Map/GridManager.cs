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
        /// Calculates the procedural 8m x 6m Breach Room center coordinates along the South perimeter (Z ≈ -halfSize - 3m).
        /// Scaling rules:
        /// - 50x50: 2 rooms (Alpha at -12m, Bravo at +12m)
        /// - 100x100: 3 rooms (Alpha at -25m, Bravo at 0m, Charlie at +25m)
        /// - 150x150: 4 rooms (Alpha at -45m, Bravo at -15m, Charlie at +15m, Delta at +45m)
        /// </summary>
        public static List<Vector3> GetBreachSpawnPositions(int mapSize = 50)
        {
            int size = MapGenerator.ClampMapSize(mapSize);
            float halfSize = size * 0.5f;
            float roomZ = -halfSize - 3.0f; // 3m south of perimeter wall, centered inside 6m deep room

            List<Vector3> positions = new List<Vector3>();

            if (size == 50)
            {
                // 2 Breach Rooms across 50m arena: Alpha (-12m), Bravo (+12m)
                positions.Add(new Vector3(-12.0f, 1.0f, roomZ));
                positions.Add(new Vector3(12.0f, 1.0f, roomZ));
            }
            else if (size == 100)
            {
                // 3 Breach Rooms across 100m arena: Alpha (-25m), Bravo (0m), Charlie (+25m)
                positions.Add(new Vector3(-25.0f, 1.0f, roomZ));
                positions.Add(new Vector3(0.0f, 1.0f, roomZ));
                positions.Add(new Vector3(25.0f, 1.0f, roomZ));
            }
            else // 150
            {
                // 4 Breach Rooms across 150m arena: Alpha (-45m), Bravo (-15m), Charlie (+15m), Delta (+45m)
                positions.Add(new Vector3(-45.0f, 1.0f, roomZ));
                positions.Add(new Vector3(-15.0f, 1.0f, roomZ));
                positions.Add(new Vector3(15.0f, 1.0f, roomZ));
                positions.Add(new Vector3(45.0f, 1.0f, roomZ));
            }

            return positions;
        }

        /// <summary>
        /// Returns tactical callsign labels for the available breach rooms matching the map tier.
        /// </summary>
        public static List<string> GetBreachRoomNames(int mapSize = 50)
        {
            int size = MapGenerator.ClampMapSize(mapSize);
            if (size == 50)
            {
                return new List<string>
                {
                    "Breach Alpha",
                    "Breach Bravo"
                };
            }
            else if (size == 100)
            {
                return new List<string>
                {
                    "Breach Alpha",
                    "Breach Bravo",
                    "Breach Charlie"
                };
            }
            else // 150
            {
                return new List<string>
                {
                    "Breach Alpha",
                    "Breach Bravo",
                    "Breach Charlie",
                    "Breach Delta"
                };
            }
        }

        /// <summary>
        /// Generates a 3x3 multi-player spawn grid (9 distinct node positions) in the southern pocket behind the privacy baffle:
        /// SpawnPos(r, c) = RoomCenter + Vector3((c - 1) * 1.5m, 0, (r - 1) * 1.5m - 1.0m)
        /// where r, c in {0, 1, 2}.
        /// </summary>
        public static List<Vector3> GetBreachRoomSpawnArray(int roomIndex, int mapSize = 50)
        {
            var roomCenters = GetBreachSpawnPositions(mapSize);
            if (roomCenters == null || roomCenters.Count == 0)
            {
                return new List<Vector3> { new Vector3(0f, 1f, -(mapSize * 0.5f) - 3.0f) };
            }

            int clampedRoomIndex = Mathf.Clamp(roomIndex, 0, roomCenters.Count - 1);
            Vector3 center = roomCenters[clampedRoomIndex];

            List<Vector3> spawnArray = new List<Vector3>(9);
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    float offsetX = (c - 1) * 1.5f;
                    float offsetZ = (r - 1) * 1.5f - 1.0f;
                    spawnArray.Add(new Vector3(center.x + offsetX, 1.0f, center.z + offsetZ));
                }
            }

            return spawnArray;
        }

        /// <summary>
        /// Returns a specific spawn coordinate from the room's 3x3 node array based on the squad slot index.
        /// </summary>
        public static Vector3 GetBreachSpawnPosition(int roomIndex, int slotIndex, int mapSize = 50)
        {
            var spawns = GetBreachRoomSpawnArray(roomIndex, mapSize);
            if (spawns == null || spawns.Count == 0) return new Vector3(0f, 1f, -(mapSize * 0.5f) - 3.0f);
            int clampedSlot = Mathf.Abs(slotIndex) % spawns.Count;
            return spawns[clampedSlot];
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
