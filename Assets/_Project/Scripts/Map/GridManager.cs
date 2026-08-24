using System;
using System.Collections.Generic;
using UnityEngine;

namespace HunterVsHider.Map
{
    public enum BreachRoomFacing
    {
        SouthPerimeter, // Attached to South perimeter wall, door faces North into arena, horizontal privacy baffle
        WestPerimeter,  // Attached to West perimeter wall (Bottom-Left), door faces East into arena, vertical privacy baffle
        EastPerimeter   // Attached to East perimeter wall (Bottom-Right), door faces West into arena, vertical privacy baffle
    }

    public struct BreachRoomData
    {
        public string name;
        public Vector3 center;
        public BreachRoomFacing facing;

        public BreachRoomData(string name, Vector3 center, BreachRoomFacing facing)
        {
            this.name = name;
            this.center = center;
            this.facing = facing;
        }
    }

    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [Tooltip("The parent object for all walls.")]
        public Transform mapParent;
        [SerializeField] private float _cellSize = 1f;

        [Header("Procedural Police Breach Staging")]
        [Tooltip("Center coordinates of procedurally constructed Police Breach Rooms.")]
        public List<Vector3> PoliceBreachSpawnPositions { get; private set; } = new List<Vector3>();

        [Tooltip("Display names of procedurally constructed Police Breach Rooms (e.g. West Breach, Center Breach, East Breach).")]
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
        /// Returns full structural metadata for all tactical Breach Rooms based on active map size.
        /// Room counts & placement:
        /// - 50x50 (3 Rooms): West Breach, Center Breach (X=0), East Breach.
        /// - 100x100 (5 Rooms): West Breach, South-West (X=-25), Center (X=0), South-East (X=+25), East Breach.
        /// - 150x150 (7 Rooms): West Breach, SW2 (X=-50), SW1 (X=-25), Center (X=0), SE1 (X=+25), SE2 (X=+50), East Breach.
        /// Equal-interval math for South rooms: step = mapSize / (N + 1); xPos = -halfSize + (k * step);
        /// </summary>
        public static List<BreachRoomData> GetBreachRoomsData(int mapSize = 50)
        {
            int size = MapGenerator.ClampMapSize(mapSize);
            float halfSize = size * 0.5f;

            List<BreachRoomData> rooms = new List<BreachRoomData>();

            // 1. Bottom-Left Room (West Breach) attached to West exterior perimeter wall (X = -halfSize - 8m, Z = -halfSize + 6m)
            Vector3 westCenter = new Vector3(-halfSize - 8.0f, 1.0f, -halfSize + 6.0f);
            rooms.Add(new BreachRoomData("West Breach", westCenter, BreachRoomFacing.WestPerimeter));

            // 2. South Perimeter Rooms (N = TotalRooms - 2) distributed at equal intervals
            int totalRooms = (size == 50) ? 3 : ((size == 100) ? 5 : 7);
            int southRoomCount = totalRooms - 2;
            float step = (float)size / (southRoomCount + 1);
            float southRoomZ = -halfSize - 6.0f;

            for (int k = 1; k <= southRoomCount; k++)
            {
                float xPos = -halfSize + (k * step);
                string roomName;

                if (southRoomCount == 1)
                {
                    roomName = "Center Breach";
                }
                else if (southRoomCount == 3)
                {
                    if (k == 1) roomName = "South-West Breach";
                    else if (k == 2) roomName = "Center Breach";
                    else roomName = "South-East Breach";
                }
                else // 5 south rooms
                {
                    if (k == 1) roomName = "South-West 2 Breach";
                    else if (k == 2) roomName = "South-West 1 Breach";
                    else if (k == 3) roomName = "Center Breach";
                    else if (k == 4) roomName = "South-East 1 Breach";
                    else roomName = "South-East 2 Breach";
                }

                rooms.Add(new BreachRoomData(roomName, new Vector3(xPos, 1.0f, southRoomZ), BreachRoomFacing.SouthPerimeter));
            }

            // 3. Bottom-Right Room (East Breach) attached to East exterior perimeter wall (X = +halfSize + 8m, Z = -halfSize + 6m)
            Vector3 eastCenter = new Vector3(halfSize + 8.0f, 1.0f, -halfSize + 6.0f);
            rooms.Add(new BreachRoomData("East Breach", eastCenter, BreachRoomFacing.EastPerimeter));

            return rooms;
        }

        /// <summary>
        /// Calculates the procedural 16m x 12m Breach Room center coordinates.
        /// </summary>
        public static List<Vector3> GetBreachSpawnPositions(int mapSize = 50)
        {
            var data = GetBreachRoomsData(mapSize);
            List<Vector3> positions = new List<Vector3>(data.Count);
            foreach (var r in data)
            {
                positions.Add(r.center);
            }
            return positions;
        }

        /// <summary>
        /// Returns tactical callsign labels for the available breach rooms matching the map tier.
        /// </summary>
        public static List<string> GetBreachRoomNames(int mapSize = 50)
        {
            var data = GetBreachRoomsData(mapSize);
            List<string> names = new List<string>(data.Count);
            foreach (var r in data)
            {
                names.Add(r.name);
            }
            return names;
        }

        /// <summary>
        /// Generates a 3x3 multi-player spawn grid (9 distinct node positions) spaced 1.5m apart
        /// in the spacious back pocket behind the oriented privacy baffle:
        /// - South Rooms: SpawnPos(r, c) = RoomCenter + Vector3((c - 1) * 1.5m, 0, (row - 1) * 1.5m - 2.0m)
        /// - West Room: SpawnPos(r, c) = RoomCenter + Vector3((col - 1) * 1.5m - 2.0m, 0, (row - 1) * 1.5m)
        /// - East Room: SpawnPos(r, c) = RoomCenter + Vector3((col - 1) * 1.5m + 2.0m, 0, (row - 1) * 1.5m)
        /// </summary>
        public static List<Vector3> GetBreachRoomSpawnArray(int roomIndex, int mapSize = 50)
        {
            var roomsData = GetBreachRoomsData(mapSize);
            if (roomsData == null || roomsData.Count == 0)
            {
                return new List<Vector3> { new Vector3(0f, 1f, -(mapSize * 0.5f) - 6.0f) };
            }

            int clampedIndex = Mathf.Clamp(roomIndex, 0, roomsData.Count - 1);
            BreachRoomData room = roomsData[clampedIndex];
            Vector3 center = room.center;

            List<Vector3> spawnArray = new List<Vector3>(9);
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    Vector3 pos;
                    if (room.facing == BreachRoomFacing.WestPerimeter)
                    {
                        // Staged in western half of room behind vertical baffle
                        float offsetX = (c - 1) * 1.5f - 2.0f;
                        float offsetZ = (r - 1) * 1.5f;
                        pos = new Vector3(center.x + offsetX, 1.0f, center.z + offsetZ);
                    }
                    else if (room.facing == BreachRoomFacing.EastPerimeter)
                    {
                        // Staged in eastern half of room behind vertical baffle
                        float offsetX = (c - 1) * 1.5f + 2.0f;
                        float offsetZ = (r - 1) * 1.5f;
                        pos = new Vector3(center.x + offsetX, 1.0f, center.z + offsetZ);
                    }
                    else // SouthPerimeter
                    {
                        // Staged in southern half of room behind horizontal baffle
                        float offsetX = (c - 1) * 1.5f;
                        float offsetZ = (r - 1) * 1.5f - 2.0f;
                        pos = new Vector3(center.x + offsetX, 1.0f, center.z + offsetZ);
                    }

                    spawnArray.Add(pos);
                }
            }

            return spawnArray;
        }

        /// <summary>
        /// Calculates the exact world position for a specific 3x3 slot index (0..8) in the breach room.
        /// </summary>
        public static Vector3 GetBreachRoomSlotPositionStatic(int roomIndex, int slotIndex, int mapSize = 50)
        {
            var roomsData = GetBreachRoomsData(mapSize);
            if (roomsData == null || roomsData.Count == 0)
            {
                return new Vector3(0f, 1f, -(mapSize * 0.5f) - 6.0f);
            }

            int clampedIndex = Mathf.Clamp(roomIndex, 0, roomsData.Count - 1);
            BreachRoomData room = roomsData[clampedIndex];
            Vector3 center = room.center;

            int clampedSlot = Mathf.Abs(slotIndex) % 9;
            int row = clampedSlot / 3; // 0, 1, 2
            int col = clampedSlot % 3; // 0, 1, 2

            Vector3 offset;
            if (room.facing == BreachRoomFacing.WestPerimeter)
            {
                offset = new Vector3((col - 1) * 1.5f - 2.0f, 0f, (row - 1) * 1.5f);
            }
            else if (room.facing == BreachRoomFacing.EastPerimeter)
            {
                offset = new Vector3((col - 1) * 1.5f + 2.0f, 0f, (row - 1) * 1.5f);
            }
            else // SouthPerimeter
            {
                offset = new Vector3((col - 1) * 1.5f, 0f, (row - 1) * 1.5f - 2.0f);
            }

            return new Vector3(center.x + offset.x, 1.0f, center.z + offset.z);
        }

        /// <summary>
        /// Instance method to get breach room slot position.
        /// </summary>
        public Vector3 GetBreachRoomSlotPosition(int roomIndex, int slotIndex, int mapSize = 50)
        {
            return GetBreachRoomSlotPositionStatic(roomIndex, slotIndex, mapSize);
        }

        /// <summary>
        /// Returns a specific spawn coordinate from the room's 3x3 node array based on the squad slot index.
        /// </summary>
        public static Vector3 GetBreachSpawnPosition(int roomIndex, int slotIndex, int mapSize = 50)
        {
            return GetBreachRoomSlotPositionStatic(roomIndex, slotIndex, mapSize);
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
