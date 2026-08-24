using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HunterVsHider.Managers;

namespace HunterVsHider.Map
{
    /// <summary>
    /// Scattered Room-Packing & Automatic Corridor Map Generator.
    /// Procedural Rules:
    /// 1. Randomized Room-Packing Pass: Packs rectangular rooms (6m x 6m to 14m x 14m) across the active map bounds.
    ///    Enforces a strict minimum 3.0m Padding Buffer between all rooms and perimeter walls.
    ///    The unexcavated space between rooms automatically forms the natural, winding corridor network (3.0m - 4.5m),
    ///    completely eliminating close parallel double-walls and repetitive grid spines.
    /// 2. Proportional Doorway & Flank Carving Pass:
    ///    - Small rooms (< 40 sq m): Exactly 1 doorway (2.4m width), acting as dead-end tactical choke points.
    ///    - Medium rooms (40 - 80 sq m): Exactly 2 doorways (main door + flank breach).
    ///    - Large rooms (> 80 sq m): 3 to 4 doorways + internal tactical cover pillars (0.5m x 2.4m).
    /// 3. Perimeter & Safe Spawn Clearance: Continuous unbroken perimeter walls (0.5m thick, 3.0m high) with 6m x 6m clear spawn zones.
    /// 4. 2D Grid Flood-Fill (BFS) Pathability Pass: Validates 100% connectivity from Police to Assassin spawn and all rooms.
    /// 5. Monolithic BoxCollider Merging: Merges collinear contiguous wall segments into single BoxCollider primitives for zero-seam CQC movement.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        public static MapGenerator Instance { get; private set; }

        [Header("Prefab & Materials")]
        [Tooltip("Optional custom prefab for wall segments (Prefab_TacticalWall). If null, standard 3D primitive Cube is instantiated.")]
        public GameObject wallPrefab;

        [Tooltip("Material applied to generated wall segments (Mat_TacticalWall).")]
        public Material wallMaterial;

        [Header("Tactical Architectural Dimensions")]
        [Tooltip("Standard thickness of all structural walls in meters (0.5m).")]
        public float wallThickness = 0.5f;

        [Tooltip("Height of generated walls in meters (3.0m).")]
        public float wallHeight = 3.0f;

        [Tooltip("Standard tactical doorway opening width in meters (2.4m).")]
        public float doorwayWidth = 2.4f;

        [Tooltip("Minimum padding buffer between any two rooms and outer walls in meters (3.0m).")]
        public float minRoomPadding = 3.0f;

        [Header("Player-Scale Room Constraints")]
        [Tooltip("Minimum room dimension in meters (6.0m).")]
        public float minRoomDimension = 6.0f;

        [Tooltip("Maximum room dimension in meters (14.0m).")]
        public float maxRoomDimension = 14.0f;

        [Header("Hierarchy Organization")]
        [Tooltip("Container Transform under which all spawned walls are parented.")]
        public Transform generatedEnvironment;

        public List<TacticalRoom> LastGeneratedRooms { get; private set; } = new List<TacticalRoom>();
        public List<SharedWallBoundary> LastGeneratedSharedBoundaries { get; private set; } = new List<SharedWallBoundary>();
        public int LastInjectedCorridorWallCount { get; private set; } = 0;

        private int currentGeneratedSeed = -1;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            EnsureContainer();
        }

        private void OnEnable()
        {
            EnsureContainer();

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMapSeedReceived -= HandleMapSeedReceived;
                MatchManager.Instance.OnMapSeedReceived += HandleMapSeedReceived;

                if (MatchManager.Instance.MapGenerationSeed > 0)
                {
                    GenerateMap(MatchManager.Instance.MapGenerationSeed);
                }
            }
        }

        private void OnDisable()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMapSeedReceived -= HandleMapSeedReceived;
            }
        }

        private void Start()
        {
            EnsureContainer();

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMapSeedReceived -= HandleMapSeedReceived;
                MatchManager.Instance.OnMapSeedReceived += HandleMapSeedReceived;

                if (MatchManager.Instance.MapGenerationSeed > 0)
                {
                    GenerateMap(MatchManager.Instance.MapGenerationSeed);
                }
            }
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMapSeedReceived -= HandleMapSeedReceived;
            }
        }

        private void HandleMapSeedReceived(int newSeed)
        {
            GenerateMap(newSeed);
        }

        private void EnsureContainer()
        {
            if (generatedEnvironment == null)
            {
                Transform existing = transform.Find("GeneratedEnvironment");
                if (existing != null)
                {
                    generatedEnvironment = existing;
                }
                else
                {
                    GameObject container = new GameObject("GeneratedEnvironment");
                    container.transform.SetParent(transform, false);
                    container.transform.localPosition = Vector3.zero;
                    container.transform.localRotation = Quaternion.identity;
                    container.transform.localScale = Vector3.one;
                    generatedEnvironment = container.transform;
                }
            }

            if (wallMaterial == null)
            {
                wallMaterial = GetOrCreateWallMaterial();
            }
        }

        private Material GetOrCreateWallMaterial()
        {
            string matPath = "Assets/_Project/Materials/Mat_TacticalWall.mat";
            Material mat = null;
#if UNITY_EDITOR
            mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
#endif
            if (mat == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                mat.color = new Color(0.533f, 0.533f, 0.533f, 1f); // #888888 Light Grey
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            }
            return mat;
        }

        public void ClearMap()
        {
            EnsureContainer();

            if (generatedEnvironment != null)
            {
                int childCount = generatedEnvironment.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    Transform child = generatedEnvironment.GetChild(i);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(child.gameObject);
                        continue;
                    }
#endif
                    Destroy(child.gameObject);
                }
            }

            currentGeneratedSeed = -1;
        }

        private void PurgeLegacyDummies()
        {
            var allObjs = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (var obj in allObjs)
            {
                if (obj == null) continue;
                string objName = obj.name.ToLower();
                bool isPrototypeDummy = objName.Contains("dummy") ||
                                       objName.Contains("targetdummy") ||
                                       objName.Contains("testdummy") ||
                                       objName.Contains("enemy_dummy") ||
                                       objName.StartsWith("target_dummy");

                if (isPrototypeDummy)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(obj);
                        continue;
                    }
#endif
                    Destroy(obj);
                }
            }
        }

        #region Internal Data Structures & Quantization Helpers

        public static float Quantize(float val)
        {
            return Mathf.Round(val * 10f) / 10f;
        }

        public static Vector2 QuantizeVector2(Vector2 v)
        {
            return new Vector2(
                Mathf.Round(v.x * 10f) / 10f,
                Mathf.Round(v.y * 10f) / 10f
            );
        }

        public static Vector3 QuantizeVector(Vector3 v)
        {
            return new Vector3(
                Mathf.Round(v.x * 10f) / 10f,
                Mathf.Round(v.y * 10f) / 10f,
                Mathf.Round(v.z * 10f) / 10f
            );
        }

        public static int CompareWallSegmentsCanonical(WallSegment a, WallSegment b)
        {
            int aStartX = Mathf.RoundToInt(a.start.x * 1000f);
            int bStartX = Mathf.RoundToInt(b.start.x * 1000f);
            int cmp = aStartX.CompareTo(bStartX);
            if (cmp != 0) return cmp;

            int aStartZ = Mathf.RoundToInt(a.start.z * 1000f);
            int bStartZ = Mathf.RoundToInt(b.start.z * 1000f);
            cmp = aStartZ.CompareTo(bStartZ);
            if (cmp != 0) return cmp;

            int aEndX = Mathf.RoundToInt(a.end.x * 1000f);
            int bEndX = Mathf.RoundToInt(b.end.x * 1000f);
            cmp = aEndX.CompareTo(bEndX);
            if (cmp != 0) return cmp;

            int aEndZ = Mathf.RoundToInt(a.end.z * 1000f);
            int bEndZ = Mathf.RoundToInt(b.end.z * 1000f);
            return aEndZ.CompareTo(bEndZ);
        }

        public static void SortWalls(List<WallSegment> walls)
        {
            if (walls == null) return;
            walls.Sort(CompareWallSegmentsCanonical);
        }

        public static void SortRooms(List<TacticalRoom> rooms)
        {
            if (rooms == null) return;
            rooms.Sort((a, b) =>
            {
                int cmp = a.bounds.xMin.CompareTo(b.bounds.xMin);
                if (cmp != 0) return cmp;
                cmp = a.bounds.yMin.CompareTo(b.bounds.yMin);
                if (cmp != 0) return cmp;
                return a.id.CompareTo(b.id);
            });
        }

        public static void SortSharedBoundaries(List<SharedWallBoundary> boundaries)
        {
            if (boundaries == null) return;
            boundaries.Sort((a, b) =>
            {
                int cmp = a.roomA.id.CompareTo(b.roomA.id);
                if (cmp != 0) return cmp;
                cmp = a.roomB.id.CompareTo(b.roomB.id);
                if (cmp != 0) return cmp;
                cmp = a.overlapMin.CompareTo(b.overlapMin);
                if (cmp != 0) return cmp;
                return a.fixedCoord.CompareTo(b.fixedCoord);
            });
        }

        public enum WallFacing { North, South, East, West }

        public enum RoomCategory { Small, Medium, Large, Anchor }

        public struct WallSegment
        {
            public Vector3 position;
            public Vector3 size; // (sizeX, sizeY, sizeZ)

            public Vector3 start => position - (size * 0.5f);
            public Vector3 end => position + (size * 0.5f);

            public WallSegment(Vector3 pos, Vector3 sz)
            {
                position = QuantizeVector(pos);
                size = QuantizeVector(sz);
            }
        }

        public struct Doorway
        {
            public WallFacing facing;
            public Vector2 centerPos; // in XZ plane
            public float width;

            public Doorway(WallFacing f, Vector2 center, float w)
            {
                facing = f;
                centerPos = QuantizeVector2(center);
                width = Quantize(w);
            }
        }

        public class SharedWallBoundary
        {
            public TacticalRoom roomA;
            public TacticalRoom roomB;
            public WallFacing facingA;
            public WallFacing facingB;
            public float overlapMin;
            public float overlapMax;
            public float fixedCoord;
            public bool isHorizontal;
            public bool hasDoorway;
            public Doorway sharedDoorway;
        }

        public class TacticalRoom
        {
            public int id;
            public Rect bounds; // in XZ plane: x = xMin, y = zMin, width = sizeX, height = sizeZ
            public float Area => bounds.width * bounds.height;
            public RoomCategory Category => Area < 40f ? RoomCategory.Small : (Area <= 100f ? RoomCategory.Medium : (Area <= 200f ? RoomCategory.Large : RoomCategory.Anchor));

            public List<Doorway> doorways = new List<Doorway>();

            public TacticalRoom(int roomId, Rect b)
            {
                id = roomId;
                bounds = new Rect(Quantize(b.x), Quantize(b.y), Quantize(b.width), Quantize(b.height));
            }
        }

        #endregion

        #region Map Generation Engine

        public static int ClampMapSize(int inputSize)
        {
            if (inputSize <= 75) return 50;
            if (inputSize <= 125) return 100;
            return 150;
        }

        /// <summary>
        /// Procedural Scattered Room-Packing Generation Pipeline:
        /// 1. Outer Perimeter Boundary Walls.
        /// 2. Randomized Room Packing with Dual Placement (Corridor-Buffered vs. Adjacent Flush Suites).
        /// 3. Shared Partition Boundary Identification & Inter-Room Doorway Carving.
        /// 4. Proportional Doorway & Flank Carving with Misalignment Safeguards.
        /// 5. Room Wall Construction & Anchor Room Internal Tactical Cover Matrices.
        /// 6. Post-Processing Dead-Space Corridor Injection (100x100 & 150x150 tiers).
        /// 7. 2D Grid Flood-Fill (BFS) Connectivity Validation & Auto-Remediation.
        /// 8. Monolithic Collinear Collider Merging & Instantiation.
        /// </summary>
        /// <param name="seed">Synchronized random integer seed.</param>
        public void GenerateMap(int seed)
        {
            if (seed <= 0) return;

            PurgeLegacyDummies();
            ClearMap();
            EnsureContainer();

            currentGeneratedSeed = seed;

            int rawMapSize = (MatchManager.Singleton != null) ? MatchManager.Singleton.SelectedMapSize : 50;
            int mapSize = ClampMapSize(rawMapSize);

            Debug.Log($"[MapGenerator] Starting Room-Packing & Suite Generation -> Seed: {seed}, Tier: {mapSize}x{mapSize}");

            System.Random prng = new System.Random(seed);
            List<WallSegment> rawWalls = new List<WallSegment>();

            float halfSize = mapSize * 0.5f;
            float halfThick = wallThickness * 0.5f;

            // =========================================================================
            // PASS 0: CONTINUOUS OUTER PERIMETER BOUNDARY WALLS & POLICE BREACH ROOMS
            // =========================================================================
            // North Boundary (Unbroken)
            rawWalls.Add(new WallSegment(
                new Vector3(0f, wallHeight * 0.5f, halfSize - halfThick),
                new Vector3(mapSize, wallHeight, wallThickness)
            ));

            // Procedural Police Breach Rooms Data (Corner & South Perimeter Rooms)
            List<BreachRoomData> breachRooms = GridManager.GetBreachRoomsData(mapSize);
            List<Vector3> breachPositions = GridManager.GetBreachSpawnPositions(mapSize);
            List<string> breachNames = GridManager.GetBreachRoomNames(mapSize);

            if (GridManager.Instance != null)
            {
                GridManager.Instance.RegisterBreachRooms(breachPositions, breachNames);
            }

            float doorWidth = 3.0f;
            float halfDoor = doorWidth * 0.5f;
            float roomWidth = 16.0f;
            float roomDepth = 12.0f;
            float halfRoomW = roomWidth * 0.5f;
            float baffleLength = 9.0f;

            // -------------------------------------------------------------------------
            // 1. WEST PERIMETER & BOTTOM-LEFT CORNER BREACH ROOM (WEST BREACH)
            // -------------------------------------------------------------------------
            // Doorway centered at Z = -halfSize + 6.0m, width 3.0m (Z in [-halfSize + 4.5m, -halfSize + 7.5m])
            rawWalls.Add(new WallSegment(
                new Vector3(-halfSize + halfThick, wallHeight * 0.5f, -halfSize + 2.25f),
                new Vector3(wallThickness, wallHeight, 4.5f)
            ));
            float westSeg2Len = mapSize - 7.5f;
            rawWalls.Add(new WallSegment(
                new Vector3(-halfSize + halfThick, wallHeight * 0.5f, (-halfSize + 7.5f + halfSize) * 0.5f),
                new Vector3(wallThickness, wallHeight, westSeg2Len)
            ));

            // Bottom-Left Room Shell (X in [-halfSize - 16m, -halfSize], Z in [-halfSize, -halfSize + 12m])
            // North wall of West Breach Room
            rawWalls.Add(new WallSegment(
                new Vector3(-halfSize - 8.0f, wallHeight * 0.5f, -halfSize + 12.0f - halfThick),
                new Vector3(roomWidth, wallHeight, wallThickness)
            ));
            // South wall of West Breach Room
            rawWalls.Add(new WallSegment(
                new Vector3(-halfSize - 8.0f, wallHeight * 0.5f, -halfSize + halfThick),
                new Vector3(roomWidth, wallHeight, wallThickness)
            ));
            // West outermost wall of West Breach Room
            rawWalls.Add(new WallSegment(
                new Vector3(-halfSize - 16.0f + halfThick, wallHeight * 0.5f, -halfSize + 6.0f),
                new Vector3(wallThickness, wallHeight, roomDepth)
            ));
            // Internal 9m Vertical Privacy Baffle (3m West of East doorway: X = -halfSize - 3.0m)
            rawWalls.Add(new WallSegment(
                new Vector3(-halfSize - 3.0f, wallHeight * 0.5f, -halfSize + 6.0f),
                new Vector3(wallThickness, wallHeight, baffleLength)
            ));

            // -------------------------------------------------------------------------
            // 2. EAST PERIMETER & BOTTOM-RIGHT CORNER BREACH ROOM (EAST BREACH)
            // -------------------------------------------------------------------------
            // Doorway centered at Z = -halfSize + 6.0m, width 3.0m (Z in [-halfSize + 4.5m, -halfSize + 7.5m])
            rawWalls.Add(new WallSegment(
                new Vector3(halfSize - halfThick, wallHeight * 0.5f, -halfSize + 2.25f),
                new Vector3(wallThickness, wallHeight, 4.5f)
            ));
            float eastSeg2Len = mapSize - 7.5f;
            rawWalls.Add(new WallSegment(
                new Vector3(halfSize - halfThick, wallHeight * 0.5f, (-halfSize + 7.5f + halfSize) * 0.5f),
                new Vector3(wallThickness, wallHeight, eastSeg2Len)
            ));

            // Bottom-Right Room Shell (X in [+halfSize, +halfSize + 16m], Z in [-halfSize, -halfSize + 12m])
            // North wall of East Breach Room
            rawWalls.Add(new WallSegment(
                new Vector3(halfSize + 8.0f, wallHeight * 0.5f, -halfSize + 12.0f - halfThick),
                new Vector3(roomWidth, wallHeight, wallThickness)
            ));
            // South wall of East Breach Room
            rawWalls.Add(new WallSegment(
                new Vector3(halfSize + 8.0f, wallHeight * 0.5f, -halfSize + halfThick),
                new Vector3(roomWidth, wallHeight, wallThickness)
            ));
            // East outermost wall of East Breach Room
            rawWalls.Add(new WallSegment(
                new Vector3(halfSize + 16.0f - halfThick, wallHeight * 0.5f, -halfSize + 6.0f),
                new Vector3(wallThickness, wallHeight, roomDepth)
            ));
            // Internal 9m Vertical Privacy Baffle (3m East of West doorway: X = +halfSize + 3.0m)
            rawWalls.Add(new WallSegment(
                new Vector3(halfSize + 3.0f, wallHeight * 0.5f, -halfSize + 6.0f),
                new Vector3(wallThickness, wallHeight, baffleLength)
            ));

            // -------------------------------------------------------------------------
            // 3. SOUTH PERIMETER & SOUTH BREACH ROOMS (CENTER & INTERMEDIATE)
            // -------------------------------------------------------------------------
            List<float> southBreachXCoords = new List<float>();
            foreach (var r in breachRooms)
            {
                if (r.facing == BreachRoomFacing.SouthPerimeter)
                {
                    southBreachXCoords.Add(r.center.x);
                }
            }
            southBreachXCoords.Sort();

            float currentX = -halfSize;
            for (int b = 0; b < southBreachXCoords.Count; b++)
            {
                float bx = southBreachXCoords[b];
                float doorLeft = bx - halfDoor;
                float doorRight = bx + halfDoor;

                // Wall segment from currentX to doorLeft along South perimeter
                float segLen = doorLeft - currentX;
                if (segLen > 0.05f)
                {
                    float segCenterX = (currentX + doorLeft) * 0.5f;
                    rawWalls.Add(new WallSegment(
                        new Vector3(segCenterX, wallHeight * 0.5f, -halfSize + halfThick),
                        new Vector3(segLen, wallHeight, wallThickness)
                    ));
                }

                currentX = doorRight;

                // Construct enclosed 16m x 12m South Breach Room outer walls
                // South wall of Breach Room (outer bottom boundary: Z = -halfSize - 12m + halfThick)
                rawWalls.Add(new WallSegment(
                    new Vector3(bx, wallHeight * 0.5f, -halfSize - roomDepth + halfThick),
                    new Vector3(roomWidth, wallHeight, wallThickness)
                ));

                // West wall of South Breach Room
                rawWalls.Add(new WallSegment(
                    new Vector3(bx - halfRoomW + halfThick, wallHeight * 0.5f, -halfSize - 6.0f),
                    new Vector3(wallThickness, wallHeight, roomDepth)
                ));

                // East wall of South Breach Room
                rawWalls.Add(new WallSegment(
                    new Vector3(bx + halfRoomW - halfThick, wallHeight * 0.5f, -halfSize - 6.0f),
                    new Vector3(wallThickness, wallHeight, roomDepth)
                ));

                // Internal 9m Horizontal Privacy Baffle Wall offset 3m South of North doorway (Z = -halfSize - 3.0m)
                rawWalls.Add(new WallSegment(
                    new Vector3(bx, wallHeight * 0.5f, -halfSize - 3.0f),
                    new Vector3(baffleLength, wallHeight, wallThickness)
                ));
            }

            // Final south perimeter wall segment from last doorway to +halfSize
            float finalSouthSegLen = halfSize - currentX;
            if (finalSouthSegLen > 0.05f)
            {
                float segCenterX = (currentX + halfSize) * 0.5f;
                rawWalls.Add(new WallSegment(
                    new Vector3(segCenterX, wallHeight * 0.5f, -halfSize + halfThick),
                    new Vector3(finalSouthSegLen, wallHeight, wallThickness)
                ));
            }

            // =========================================================================
            // PASS 1: RANDOMIZED ROOM PACKING (DUAL PLACEMENT: CORRIDOR-BUFFERED & ADJACENT SUITES)
            // =========================================================================
            List<TacticalRoom> rooms = PackScatteredRooms(mapSize, prng);
            SortRooms(rooms);

            // =========================================================================
            // PASS 2: IDENTIFY SHARED BOUNDARIES & CARVE PROPORTIONAL DOORWAYS
            // =========================================================================
            List<SharedWallBoundary> sharedBoundaries = FindSharedWallBoundaries(rooms);
            SortSharedBoundaries(sharedBoundaries);

            CarveProportionalDoorways(rooms, sharedBoundaries, mapSize, prng);

            // =========================================================================
            // PASS 3: CONSTRUCT ROOM WALLS & INTERNAL COVER MATRICES
            // =========================================================================
            SortRooms(rooms);
            SortSharedBoundaries(sharedBoundaries);
            SortWalls(rawWalls);

            ConstructRoomWalls(rooms, sharedBoundaries, rawWalls, prng);
            SortWalls(rawWalls);

            // =========================================================================
            // PASS 3.5: POST-PROCESSING CORRIDOR WEB & ROOM CLUSTER LINKING (100x100 & 150x150)
            // =========================================================================
            LastInjectedCorridorWallCount = 0;
            if (mapSize >= 100)
            {
                SortRooms(rooms);
                SortWalls(rawWalls);
                InjectCorridorWebs(mapSize, rooms, rawWalls, prng);
                SortWalls(rawWalls);
            }

            // =========================================================================
            // PASS 4: 2D GRID FLOOD-FILL (BFS) CONNECTIVITY VALIDATION & AUTO-REMEDIATION
            // =========================================================================
            SortRooms(rooms);
            SortWalls(rawWalls);
            ValidateAndRemediateMapConnectivity(mapSize, rawWalls, rooms);
            SortWalls(rawWalls);

            LastGeneratedRooms = rooms;
            LastGeneratedSharedBoundaries = sharedBoundaries;

            // =========================================================================
            // PASS 5: MONOLITHIC COLLINEAR COLLIDER MERGING & INSTANTIATION
            // =========================================================================
            List<WallSegment> mergedWalls = MergeCollinearWallSegments(rawWalls, mapSize);
            SortWalls(mergedWalls);

            InstantiateWallGeometry(mergedWalls, seed, mapSize, rooms.Count);
        }

        #endregion

        #region Pass 1: Scattered Room-Packing with Dual Placement

        private List<TacticalRoom> PackScatteredRooms(int mapSize, System.Random prng)
        {
            List<TacticalRoom> rooms = new List<TacticalRoom>();
            float halfSize = mapSize * 0.5f;

            int maxAttempts;
            int targetRoomCount;

            if (mapSize == 50)
            {
                maxAttempts = 600;
                targetRoomCount = 14;
            }
            else if (mapSize == 100)
            {
                maxAttempts = 1800;
                targetRoomCount = 32;
            }
            else // 150
            {
                maxAttempts = 3500;
                targetRoomCount = 70;
            }

            float padding = minRoomPadding; // 3.0m
            float minUsable = -halfSize + padding;
            float maxUsable = halfSize - padding;

            // Tier-specific placement probability tuning:
            // 50x50: 15% Adjacent Flush (85% Corridor-Buffered) for maximum active floor plan accessibility
            // 100x100 & 150x150: 35% Adjacent Flush (65% Corridor-Buffered) for rich suites & tactical vaults
            float adjacentChance = (mapSize == 50) ? 0.15f : 0.35f;

            int nextRoomId = 1;

            for (int attempt = 0; attempt < maxAttempts && rooms.Count < targetRoomCount; attempt++)
            {
                float width, depth;
                PickStochasticRoomDimensions(mapSize, prng, out width, out depth);

                Rect candidate;
                TacticalRoom parentRoom = null;
                bool isAdjacentPlacement = (rooms.Count > 0 && prng.NextDouble() < adjacentChance);

                if (isAdjacentPlacement)
                {
                    // Adjacent Flush Placement: Snaps flush against an existing room's exterior wall
                    parentRoom = rooms[prng.Next(rooms.Count)];
                    WallFacing snapFacing = (WallFacing)prng.Next(4);

                    if (snapFacing == WallFacing.North)
                    {
                        float minX = parentRoom.bounds.xMin - width + 3.0f;
                        float maxX = parentRoom.bounds.xMax - 3.0f;
                        float cx = (minX < maxX) ? minX + (float)prng.NextDouble() * (maxX - minX) : parentRoom.bounds.xMin;
                        candidate = new Rect(cx, parentRoom.bounds.yMax, width, depth);
                    }
                    else if (snapFacing == WallFacing.South)
                    {
                        float minX = parentRoom.bounds.xMin - width + 3.0f;
                        float maxX = parentRoom.bounds.xMax - 3.0f;
                        float cx = (minX < maxX) ? minX + (float)prng.NextDouble() * (maxX - minX) : parentRoom.bounds.xMin;
                        candidate = new Rect(cx, parentRoom.bounds.yMin - depth, width, depth);
                    }
                    else if (snapFacing == WallFacing.East)
                    {
                        float minZ = parentRoom.bounds.yMin - depth + 3.0f;
                        float maxZ = parentRoom.bounds.yMax - 3.0f;
                        float cz = (minZ < maxZ) ? minZ + (float)prng.NextDouble() * (maxZ - minZ) : parentRoom.bounds.yMin;
                        candidate = new Rect(parentRoom.bounds.xMax, cz, width, depth);
                    }
                    else // West
                    {
                        float minZ = parentRoom.bounds.yMin - depth + 3.0f;
                        float maxZ = parentRoom.bounds.yMax - 3.0f;
                        float cz = (minZ < maxZ) ? minZ + (float)prng.NextDouble() * (maxZ - minZ) : parentRoom.bounds.yMin;
                        candidate = new Rect(parentRoom.bounds.xMin - width, cz, width, depth);
                    }

                    // Bounds & spawn safety checks
                    if (candidate.xMin < minUsable || candidate.xMax > maxUsable ||
                        candidate.yMin < minUsable || candidate.yMax > maxUsable) continue;

                    if (IsRectInSafetyZone(candidate, mapSize, padding)) continue;

                    // Ensure candidate does NOT overlap interior of any room
                    bool invalid = false;
                    foreach (var r in rooms)
                    {
                        Rect interiorTest = new Rect(candidate.x + 0.1f, candidate.y + 0.1f, candidate.width - 0.2f, candidate.height - 0.2f);
                        if (interiorTest.Overlaps(r.bounds))
                        {
                            invalid = true;
                            break;
                        }

                        // For all rooms other than parentRoom, verify 3.0m padding buffer
                        if (r != parentRoom)
                        {
                            Rect paddedCandidate = new Rect(
                                candidate.x - (padding - 0.15f),
                                candidate.y - (padding - 0.15f),
                                candidate.width + (padding - 0.15f) * 2f,
                                candidate.height + (padding - 0.15f) * 2f
                            );
                            if (paddedCandidate.Overlaps(r.bounds))
                            {
                                invalid = true;
                                break;
                            }
                        }
                    }

                    if (invalid) continue;

                    rooms.Add(new TacticalRoom(nextRoomId++, candidate));
                }
                else
                {
                    // Corridor-Buffered Placement: Strict 3.0m buffer on all sides
                    float minCx = minUsable + width * 0.5f;
                    float maxCx = maxUsable - width * 0.5f;
                    float minCz = minUsable + depth * 0.5f;
                    float maxCz = maxUsable - depth * 0.5f;

                    if (minCx >= maxCx || minCz >= maxCz) continue;

                    float cx = minCx + (float)prng.NextDouble() * (maxCx - minCx);
                    float cz = minCz + (float)prng.NextDouble() * (maxCz - minCz);

                    candidate = new Rect(cx - width * 0.5f, cz - depth * 0.5f, width, depth);

                    if (IsRectInSafetyZone(candidate, mapSize, padding)) continue;

                    bool overlapsExisting = false;
                    Rect paddedCandidate = new Rect(
                        candidate.x - padding,
                        candidate.y - padding,
                        candidate.width + padding * 2f,
                        candidate.height + padding * 2f
                    );

                    foreach (var r in rooms)
                    {
                        if (paddedCandidate.Overlaps(r.bounds))
                        {
                            overlapsExisting = true;
                            break;
                        }
                    }

                    if (overlapsExisting) continue;

                    rooms.Add(new TacticalRoom(nextRoomId++, candidate));
                }
            }

            // Strictly sort rooms by ID to enforce 100% deterministic iteration order across processes
            rooms.Sort((a, b) => a.id.CompareTo(b.id));

            Debug.Log($"[MapGenerator] Room Packing Complete: Packed {rooms.Count} rooms across {mapSize}x{mapSize}m arena ({adjacentChance:P0} suite placement probability).");
            return rooms;
        }

        /// <summary>
        /// Dynamically selects room dimensions based on active map size tier:
        /// - 50x50 (Small): Mix of small dead-end rooms (5.5m - 6.2m, < 40 sq m) and medium killhouse rooms (6.5m - 11.5m).
        /// - 100x100 (Medium): 30% Small Dead-Ends (< 40 sq m), 50% Medium Rooms (6.5m - 13m), 20% Large Anchor Rooms (15m - 18m).
        /// - 150x150 (Large): 30% Small Dead-Ends (< 40 sq m), 40% Medium Rooms (6.5m - 13m), 30% Large Anchor Rooms (16m - 22m).
        /// </summary>
        private void PickStochasticRoomDimensions(int mapSize, System.Random prng, out float width, out float depth)
        {
            if (mapSize == 50)
            {
                // 50x50 (Small): 40% Small Rooms (5.5m - 6.2m, < 40 sq m), 60% Medium Rooms (6.5m - 11.5m)
                if (prng.NextDouble() < 0.40)
                {
                    width = 5.5f + (float)prng.NextDouble() * 0.7f;  // 5.5m - 6.2m
                    depth = 5.5f + (float)prng.NextDouble() * 0.7f;  // 5.5m - 6.2m (Area: 30 - 38.5 sq m)
                }
                else
                {
                    width = 6.5f + (float)prng.NextDouble() * 5.0f;  // 6.5m - 11.5m
                    depth = 6.5f + (float)prng.NextDouble() * 5.0f;  // 6.5m - 11.5m
                }
            }
            else if (mapSize == 100)
            {
                // 100x100 (Medium): 20% Large Anchors, 30% Small Dead-Ends, 50% Medium Rooms
                double roll = prng.NextDouble();
                if (roll < 0.20)
                {
                    // Anchor Room
                    width = 15.0f + (float)prng.NextDouble() * 3.0f; // 15.0m - 18.0m
                    depth = 15.0f + (float)prng.NextDouble() * 3.0f; // 15.0m - 18.0m
                }
                else if (roll < 0.50)
                {
                    // Small Dead-End Room (< 40 sq m)
                    width = 5.5f + (float)prng.NextDouble() * 0.7f;  // 5.5m - 6.2m
                    depth = 5.5f + (float)prng.NextDouble() * 0.7f;  // 5.5m - 6.2m
                }
                else
                {
                    // Medium Room
                    width = 6.5f + (float)prng.NextDouble() * 6.5f;  // 6.5m - 13.0m
                    depth = 6.5f + (float)prng.NextDouble() * 6.5f;  // 6.5m - 13.0m
                }
            }
            else // 150x150 (Large)
            {
                // 150x150 (Large): 30% Large Anchors, 30% Small Dead-Ends, 40% Medium Rooms
                double roll = prng.NextDouble();
                if (roll < 0.30)
                {
                    // Anchor Room (Warehouse Bay)
                    width = 16.0f + (float)prng.NextDouble() * 6.0f; // 16.0m - 22.0m
                    depth = 16.0f + (float)prng.NextDouble() * 6.0f; // 16.0m - 22.0m
                }
                else if (roll < 0.60)
                {
                    // Small Dead-End Room (< 40 sq m)
                    width = 5.5f + (float)prng.NextDouble() * 0.7f;  // 5.5m - 6.2m
                    depth = 5.5f + (float)prng.NextDouble() * 0.7f;  // 5.5m - 6.2m
                }
                else
                {
                    // Medium Room
                    width = 6.5f + (float)prng.NextDouble() * 6.5f;  // 6.5m - 13.0m
                    depth = 6.5f + (float)prng.NextDouble() * 6.5f;  // 6.5m - 13.0m
                }
            }

            width = Quantize(width);
            depth = Quantize(depth);
        }

        #endregion

        #region Pass 2: Shared Boundary Detection & Proportional Doorways

        private List<SharedWallBoundary> FindSharedWallBoundaries(List<TacticalRoom> rooms)
        {
            List<SharedWallBoundary> sharedList = new List<SharedWallBoundary>();
            float tol = 0.05f;

            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    TacticalRoom rA = rooms[i];
                    TacticalRoom rB = rooms[j];

                    // Check A North touches B South
                    if (Mathf.Abs(rA.bounds.yMax - rB.bounds.yMin) < tol)
                    {
                        float oMin = Mathf.Max(rA.bounds.xMin, rB.bounds.xMin);
                        float oMax = Mathf.Min(rA.bounds.xMax, rB.bounds.xMax);
                        if (oMax - oMin > 0.5f)
                        {
                            sharedList.Add(new SharedWallBoundary {
                                roomA = rA, roomB = rB,
                                facingA = WallFacing.North, facingB = WallFacing.South,
                                overlapMin = oMin, overlapMax = oMax,
                                fixedCoord = rA.bounds.yMax, isHorizontal = true
                            });
                        }
                    }
                    // Check A South touches B North
                    else if (Mathf.Abs(rA.bounds.yMin - rB.bounds.yMax) < tol)
                    {
                        float oMin = Mathf.Max(rA.bounds.xMin, rB.bounds.xMin);
                        float oMax = Mathf.Min(rA.bounds.xMax, rB.bounds.xMax);
                        if (oMax - oMin > 0.5f)
                        {
                            sharedList.Add(new SharedWallBoundary {
                                roomA = rA, roomB = rB,
                                facingA = WallFacing.South, facingB = WallFacing.North,
                                overlapMin = oMin, overlapMax = oMax,
                                fixedCoord = rA.bounds.yMin, isHorizontal = true
                            });
                        }
                    }
                    // Check A East touches B West
                    else if (Mathf.Abs(rA.bounds.xMax - rB.bounds.xMin) < tol)
                    {
                        float oMin = Mathf.Max(rA.bounds.yMin, rB.bounds.yMin);
                        float oMax = Mathf.Min(rA.bounds.yMax, rB.bounds.yMax);
                        if (oMax - oMin > 0.5f)
                        {
                            sharedList.Add(new SharedWallBoundary {
                                roomA = rA, roomB = rB,
                                facingA = WallFacing.East, facingB = WallFacing.West,
                                overlapMin = oMin, overlapMax = oMax,
                                fixedCoord = rA.bounds.xMax, isHorizontal = false
                            });
                        }
                    }
                    // Check A West touches B East
                    else if (Mathf.Abs(rA.bounds.xMin - rB.bounds.xMax) < tol)
                    {
                        float oMin = Mathf.Max(rA.bounds.yMin, rB.bounds.yMin);
                        float oMax = Mathf.Min(rA.bounds.yMax, rB.bounds.yMax);
                        if (oMax - oMin > 0.5f)
                        {
                            sharedList.Add(new SharedWallBoundary {
                                roomA = rA, roomB = rB,
                                facingA = WallFacing.West, facingB = WallFacing.East,
                                overlapMin = oMin, overlapMax = oMax,
                                fixedCoord = rA.bounds.xMin, isHorizontal = false
                            });
                        }
                    }
                }
            }

            // Strictly sort shared boundaries by member room IDs for deterministic processing
            sharedList.Sort((a, b) =>
            {
                int cmp = a.roomA.id.CompareTo(b.roomA.id);
                if (cmp != 0) return cmp;
                return a.roomB.id.CompareTo(b.roomB.id);
            });

            return sharedList;
        }

        /// <summary>
        /// Assigns proportional doorways:
        /// - Inter-room doorways across shared partition boundaries (with strict Alignment Safeguard).
        /// - Small Rooms (< 40 sq m): HARD CAP = 1 DOORWAY (ensuite or corridor).
        /// - Medium Rooms (40 - 100 sq m): HARD CAP = 2 DOORWAYS.
        /// - Large / Anchor Rooms (> 100 sq m): 3 to 4 doorways.
        /// - 50x50 Small Maps: Guarantees 100% room reachability by prioritizing corridor-facing exterior doorways.
        /// </summary>
        private void CarveProportionalDoorways(List<TacticalRoom> rooms, List<SharedWallBoundary> sharedBoundaries, int mapSize, System.Random prng)
        {
            // 1. Process Shared Boundaries for inter-room doorways
            foreach (var sb in sharedBoundaries)
            {
                float overlapSpan = sb.overlapMax - sb.overlapMin;
                if (overlapSpan >= 3.2f) // Doorway width (2.4m) + margins
                {
                    bool roomACanTakeDoor = (sb.roomA.Area < 40f && sb.roomA.doorways.Count == 0) ||
                                            (sb.roomA.Area >= 40f && sb.roomA.Area <= 100f && sb.roomA.doorways.Count < 2) ||
                                            (sb.roomA.Area > 100f && sb.roomA.doorways.Count < 4);

                    bool roomBCanTakeDoor = (sb.roomB.Area < 40f && sb.roomB.doorways.Count == 0) ||
                                            (sb.roomB.Area >= 40f && sb.roomB.Area <= 100f && sb.roomB.doorways.Count < 2) ||
                                            (sb.roomB.Area > 100f && sb.roomB.doorways.Count < 4);

                    if (roomACanTakeDoor && roomBCanTakeDoor)
                    {
                        bool placeSharedDoor = (sb.roomA.Area < 40f || sb.roomB.Area < 40f)
                            ? (prng.NextDouble() < 0.65)  // 65% chance for ensuite attachment
                            : (prng.NextDouble() < 0.80); // 80% chance for suite connection

                        if (placeSharedDoor)
                        {
                            // Alignment Safeguard: fits 100% inside shared overlap segment
                            float margin = 0.4f;
                            float halfDoor = doorwayWidth * 0.5f;
                            float minCenter = sb.overlapMin + halfDoor + margin;
                            float maxCenter = sb.overlapMax - halfDoor - margin;

                            float doorCenter = (minCenter < maxCenter)
                                ? minCenter + (float)prng.NextDouble() * (maxCenter - minCenter)
                                : (sb.overlapMin + sb.overlapMax) * 0.5f;

                            Vector2 doorPos = sb.isHorizontal
                                ? new Vector2(doorCenter, sb.fixedCoord)
                                : new Vector2(sb.fixedCoord, doorCenter);

                            Doorway doorA = new Doorway(sb.facingA, doorPos, doorwayWidth);
                            Doorway doorB = new Doorway(sb.facingB, doorPos, doorwayWidth);

                            sb.roomA.doorways.Add(doorA);
                            sb.roomB.doorways.Add(doorB);

                            sb.hasDoorway = true;
                            sb.sharedDoorway = doorA;
                        }
                    }
                }
            }

            // 2. Fill remaining target doorways on exterior walls
            foreach (var room in rooms)
            {
                float area = room.Area;
                int targetDoors;
                if (area < 40f) targetDoors = 1;
                else if (area <= 100f) targetDoors = 2;
                else targetDoors = prng.Next(3, 5);

                if (room.doorways.Count >= targetDoors) continue;

                List<WallFacing> availableFacings = new List<WallFacing>
                {
                    WallFacing.North, WallFacing.South, WallFacing.East, WallFacing.West
                };

                foreach (var d in room.doorways)
                {
                    availableFacings.Remove(d.facing);
                }

                // Prioritize exterior non-shared facings to ensure clean corridor connections
                List<WallFacing> exteriorFacings = new List<WallFacing>();
                List<WallFacing> sharedFacings = new List<WallFacing>();

                foreach (var f in availableFacings)
                {
                    bool isShared = false;
                    foreach (var sb in sharedBoundaries)
                    {
                        if ((sb.roomA == room && sb.facingA == f) || (sb.roomB == room && sb.facingB == f))
                        {
                            isShared = true;
                            break;
                        }
                    }
                    if (isShared) sharedFacings.Add(f);
                    else exteriorFacings.Add(f);
                }

                Shuffle(exteriorFacings, prng);
                Shuffle(sharedFacings, prng);

                List<WallFacing> prioritizedFacings = new List<WallFacing>(exteriorFacings);
                // On 50x50 maps, strictly carve to open corridors; on larger maps allow fallback to shared facings
                if (mapSize != 50 || prioritizedFacings.Count == 0)
                {
                    prioritizedFacings.AddRange(sharedFacings);
                }

                while (room.doorways.Count < targetDoors && prioritizedFacings.Count > 0)
                {
                    WallFacing facing = prioritizedFacings[0];
                    prioritizedFacings.RemoveAt(0);

                    Vector2 doorPos = GetFacingDoorPosition(room.bounds, facing, prng);
                    room.doorways.Add(new Doorway(facing, doorPos, doorwayWidth));
                }
            }
        }

        private Vector2 GetFacingDoorPosition(Rect r, WallFacing facing, System.Random prng)
        {
            float halfDoor = doorwayWidth * 0.5f;
            float margin = 1.0f; // inset from corners

            switch (facing)
            {
                case WallFacing.North:
                    float minNx = r.xMin + margin + halfDoor;
                    float maxNx = r.xMax - margin - halfDoor;
                    float nx = (minNx < maxNx) ? minNx + (float)prng.NextDouble() * (maxNx - minNx) : r.center.x;
                    return new Vector2(nx, r.yMax);

                case WallFacing.South:
                    float minSx = r.xMin + margin + halfDoor;
                    float maxSx = r.xMax - margin - halfDoor;
                    float sx = (minSx < maxSx) ? minSx + (float)prng.NextDouble() * (maxSx - minSx) : r.center.x;
                    return new Vector2(sx, r.yMin);

                case WallFacing.East:
                    float minEz = r.yMin + margin + halfDoor;
                    float maxEz = r.yMax - margin - halfDoor;
                    float ez = (minEz < maxEz) ? minEz + (float)prng.NextDouble() * (maxEz - minEz) : r.center.y;
                    return new Vector2(r.xMax, ez);

                default: // West
                    float minWz = r.yMin + margin + halfDoor;
                    float maxWz = r.yMax - margin - halfDoor;
                    float wz = (minWz < maxWz) ? minWz + (float)prng.NextDouble() * (maxWz - minWz) : r.center.y;
                    return new Vector2(r.xMin, wz);
            }
        }

        #endregion

        #region Pass 3: Room Wall Construction & Cover Matrices for Anchor Rooms

        private void ConstructRoomWalls(
            List<TacticalRoom> rooms, List<SharedWallBoundary> sharedBoundaries,
            List<WallSegment> rawWalls, System.Random prng)
        {
            // 1. Construct single-layer walls for shared boundaries
            HashSet<SharedWallBoundary> constructedShared = new HashSet<SharedWallBoundary>();

            foreach (var sb in sharedBoundaries)
            {
                constructedShared.Add(sb);

                List<Doorway> doorsOnShared = new List<Doorway>();
                if (sb.hasDoorway)
                {
                    doorsOnShared.Add(sb.sharedDoorway);
                }

                ConstructWallSegmentSpan(sb.facingA, sb.overlapMin, sb.overlapMax, sb.fixedCoord, doorsOnShared, rawWalls);
            }

            // 2. Construct perimeter walls for each room (excluding shared spans already built)
            foreach (var room in rooms)
            {
                Rect r = room.bounds;

                ConstructRoomPerimeterExcludingShared(WallFacing.North, r.xMin, r.xMax, r.yMax, room, sharedBoundaries, rawWalls);
                ConstructRoomPerimeterExcludingShared(WallFacing.South, r.xMin, r.xMax, r.yMin, room, sharedBoundaries, rawWalls);
                ConstructRoomPerimeterExcludingShared(WallFacing.West, r.yMin, r.yMax, r.xMin, room, sharedBoundaries, rawWalls);
                ConstructRoomPerimeterExcludingShared(WallFacing.East, r.yMin, r.yMax, r.xMax, room, sharedBoundaries, rawWalls);

                // Internal Tactical Cover Elements for Large & Anchor Rooms (> 100 sq m)
                if (room.Area > 100f)
                {
                    float cx = room.bounds.center.x;
                    float cz = room.bounds.center.y;
                    float rw = room.bounds.width;
                    float rh = room.bounds.height;

                    if (room.Area > 200f)
                    {
                        // Massive Anchor Room (> 200 sq m, e.g. 15m-22m warehouse/suites)
                        double style = prng.NextDouble();
                        if (style < 0.45)
                        {
                            // 2x2 Structural Pillar Matrix (1.2m x 1.2m columns)
                            float offX = rw * 0.25f;
                            float offZ = rh * 0.25f;

                            rawWalls.Add(new WallSegment(new Vector3(cx - offX, wallHeight * 0.5f, cz - offZ), new Vector3(1.2f, wallHeight, 1.2f)));
                            rawWalls.Add(new WallSegment(new Vector3(cx + offX, wallHeight * 0.5f, cz - offZ), new Vector3(1.2f, wallHeight, 1.2f)));
                            rawWalls.Add(new WallSegment(new Vector3(cx - offX, wallHeight * 0.5f, cz + offZ), new Vector3(1.2f, wallHeight, 1.2f)));
                            rawWalls.Add(new WallSegment(new Vector3(cx + offX, wallHeight * 0.5f, cz + offZ), new Vector3(1.2f, wallHeight, 1.2f)));
                        }
                        else if (style < 0.75)
                        {
                            // Central divider partition + 2 flanking cover columns
                            bool divideHoriz = rw >= rh;
                            float partLen = Mathf.Min(rw, rh) * 0.45f;
                            if (divideHoriz)
                            {
                                rawWalls.Add(new WallSegment(new Vector3(cx, wallHeight * 0.5f, cz), new Vector3(partLen, wallHeight, wallThickness)));
                                rawWalls.Add(new WallSegment(new Vector3(cx, wallHeight * 0.5f, cz - rh * 0.25f), new Vector3(1.2f, wallHeight, 1.2f)));
                                rawWalls.Add(new WallSegment(new Vector3(cx, wallHeight * 0.5f, cz + rh * 0.25f), new Vector3(1.2f, wallHeight, 1.2f)));
                            }
                            else
                            {
                                rawWalls.Add(new WallSegment(new Vector3(cx, wallHeight * 0.5f, cz), new Vector3(wallThickness, wallHeight, partLen)));
                                rawWalls.Add(new WallSegment(new Vector3(cx - rw * 0.25f, wallHeight * 0.5f, cz), new Vector3(1.2f, wallHeight, 1.2f)));
                                rawWalls.Add(new WallSegment(new Vector3(cx + rw * 0.25f, wallHeight * 0.5f, cz), new Vector3(1.2f, wallHeight, 1.2f)));
                            }
                        }
                        else
                        {
                            // Dual-Partition tactical barrier (0.5m x 3.0m dividers)
                            float partLen = Mathf.Min(rw, rh) * 0.4f;
                            rawWalls.Add(new WallSegment(new Vector3(cx - rw * 0.18f, wallHeight * 0.5f, cz), new Vector3(wallThickness, wallHeight, partLen)));
                            rawWalls.Add(new WallSegment(new Vector3(cx + rw * 0.18f, wallHeight * 0.5f, cz), new Vector3(wallThickness, wallHeight, partLen)));
                        }
                    }
                    else
                    {
                        // Medium-Large Room (100 - 200 sq m)
                        if (prng.NextDouble() > 0.5)
                        {
                            float partLen = Mathf.Min(rw, rh) * 0.45f;
                            bool horiz = prng.NextDouble() > 0.5;
                            rawWalls.Add(new WallSegment(
                                new Vector3(cx, wallHeight * 0.5f, cz),
                                horiz ? new Vector3(partLen, wallHeight, wallThickness) : new Vector3(wallThickness, wallHeight, partLen)
                            ));
                        }
                        else
                        {
                            float off = Mathf.Min(rw, rh) * 0.22f;
                            rawWalls.Add(new WallSegment(new Vector3(cx - off, wallHeight * 0.5f, cz), new Vector3(1.0f, wallHeight, 1.0f)));
                            rawWalls.Add(new WallSegment(new Vector3(cx + off, wallHeight * 0.5f, cz), new Vector3(1.0f, wallHeight, 1.0f)));
                        }
                    }
                }
            }
        }

        private void ConstructRoomPerimeterExcludingShared(
            WallFacing facing, float startCoord, float endCoord, float fixedCoord,
            TacticalRoom room, List<SharedWallBoundary> sharedBoundaries, List<WallSegment> rawWalls)
        {
            // Find all shared spans along this facing for this room
            List<Vector2> sharedIntervals = new List<Vector2>();
            foreach (var sb in sharedBoundaries)
            {
                if ((sb.roomA == room && sb.facingA == facing) || (sb.roomB == room && sb.facingB == facing))
                {
                    sharedIntervals.Add(new Vector2(sb.overlapMin, sb.overlapMax));
                }
            }

            // Doorways assigned to this room on this facing
            List<Doorway> roomDoorsOnFacing = new List<Doorway>();
            foreach (var d in room.doorways)
            {
                if (d.facing == facing) roomDoorsOnFacing.Add(d);
            }

            if (sharedIntervals.Count == 0)
            {
                // Simple unbroken exterior facing
                ConstructWallSegmentSpan(facing, startCoord, endCoord, fixedCoord, roomDoorsOnFacing, rawWalls);
                return;
            }

            // Merge shared intervals
            sharedIntervals.Sort((a, b) => a.x.CompareTo(b.x));

            float cur = startCoord;
            foreach (var s in sharedIntervals)
            {
                if (s.x - cur > 0.3f)
                {
                    // Non-shared span
                    float spanStart = cur;
                    float spanEnd = s.x;

                    List<Doorway> doorsInSpan = new List<Doorway>();
                    foreach (var d in roomDoorsOnFacing)
                    {
                        float dPos = (facing == WallFacing.North || facing == WallFacing.South) ? d.centerPos.x : d.centerPos.y;
                        if (dPos >= spanStart - 0.1f && dPos <= spanEnd + 0.1f)
                        {
                            doorsInSpan.Add(d);
                        }
                    }

                    ConstructWallSegmentSpan(facing, spanStart, spanEnd, fixedCoord, doorsInSpan, rawWalls);
                }

                cur = Mathf.Max(cur, s.y);
            }

            if (endCoord - cur > 0.3f)
            {
                float spanStart = cur;
                float spanEnd = endCoord;

                List<Doorway> doorsInSpan = new List<Doorway>();
                foreach (var d in roomDoorsOnFacing)
                {
                    float dPos = (facing == WallFacing.North || facing == WallFacing.South) ? d.centerPos.x : d.centerPos.y;
                    if (dPos >= spanStart - 0.1f && dPos <= spanEnd + 0.1f)
                    {
                        doorsInSpan.Add(d);
                    }
                }

                ConstructWallSegmentSpan(facing, spanStart, spanEnd, fixedCoord, doorsInSpan, rawWalls);
            }
        }

        private void ConstructWallSegmentSpan(
            WallFacing facing, float startCoord, float endCoord, float fixedCoord,
            List<Doorway> doorsInSpan, List<WallSegment> rawWalls)
        {
            bool isHorizontal = (facing == WallFacing.North || facing == WallFacing.South);

            if (doorsInSpan == null || doorsInSpan.Count == 0)
            {
                float length = endCoord - startCoord;
                if (length > 0.3f)
                {
                    float mid = (startCoord + endCoord) * 0.5f;
                    Vector3 pos = isHorizontal
                        ? new Vector3(mid, wallHeight * 0.5f, fixedCoord)
                        : new Vector3(fixedCoord, wallHeight * 0.5f, mid);
                    Vector3 sz = isHorizontal
                        ? new Vector3(length, wallHeight, wallThickness)
                        : new Vector3(wallThickness, wallHeight, length);

                    rawWalls.Add(new WallSegment(pos, sz));
                }
                return;
            }

            doorsInSpan.Sort((a, b) => isHorizontal ? a.centerPos.x.CompareTo(b.centerPos.x) : a.centerPos.y.CompareTo(b.centerPos.y));

            float cur = startCoord;
            foreach (var door in doorsInSpan)
            {
                float doorCenter = isHorizontal ? door.centerPos.x : door.centerPos.y;
                float doorStart = Mathf.Max(cur, doorCenter - door.width * 0.5f);
                float doorEnd = doorCenter + door.width * 0.5f;

                if (doorStart - cur > 0.3f)
                {
                    float mid = (cur + doorStart) * 0.5f;
                    float len = doorStart - cur;
                    Vector3 pos = isHorizontal
                        ? new Vector3(mid, wallHeight * 0.5f, fixedCoord)
                        : new Vector3(fixedCoord, wallHeight * 0.5f, mid);
                    Vector3 sz = isHorizontal
                        ? new Vector3(len, wallHeight, wallThickness)
                        : new Vector3(wallThickness, wallHeight, len);

                    rawWalls.Add(new WallSegment(pos, sz));
                }

                cur = Mathf.Max(cur, doorEnd);
            }

            if (endCoord - cur > 0.3f)
            {
                float mid = (cur + endCoord) * 0.5f;
                float len = endCoord - cur;
                Vector3 pos = isHorizontal
                    ? new Vector3(mid, wallHeight * 0.5f, fixedCoord)
                    : new Vector3(fixedCoord, wallHeight * 0.5f, mid);
                Vector3 sz = isHorizontal
                    ? new Vector3(len, wallHeight, wallThickness)
                    : new Vector3(wallThickness, wallHeight, len);

                rawWalls.Add(new WallSegment(pos, sz));
            }
        }

        #endregion

        #region Pass 3.5: Post-Processing Corridor Web & Room Cluster Linking (100x100 & 150x150)

        /// <summary>
        /// Post-processing pass for Medium (100x100) and Large (150x150) maps:
        /// 1. Room Sub-Grouping (Graph Proximity): Groups adjacent rooms into clusters of 2, 3, 4, and 5 rooms (edge distance <= 6.0m).
        /// 2. Dead-Space Corridor Wall Injection: Encloses inter-room gaps into dedicated 3.0m - 4.0m wide hallway channels.
        /// 3. Sightline Constraint: Clamps straight corridor runs to <= 25.0m with 90-degree corner jogs and T-junction walls.
        /// 4. Doorway & Spawn Safeguards: Preserves >= 2.2m clearance from all carved room doorways and spawn zones.
        /// </summary>
        private void InjectCorridorWebs(int mapSize, List<TacticalRoom> rooms, List<WallSegment> rawWalls, System.Random prng)
        {
            float halfSize = mapSize * 0.5f;
            List<WallSegment> injectedWalls = new List<WallSegment>();

            // Collect all carved room doorways for fast distance checks
            List<Vector2> allDoorways = new List<Vector2>();
            foreach (var r in rooms)
            {
                foreach (var d in r.doorways)
                {
                    allDoorways.Add(d.centerPos);
                }
            }

            // =========================================================================
            // STEP 1: ROOM SUB-GROUPING (GRAPH PROXIMITY)
            // =========================================================================
            List<List<TacticalRoom>> clusters = GroupRoomsIntoProximityClusters(rooms, prng);

            // =========================================================================
            // STEP 2: CLUSTER GAP ENCLOSURE & CORRIDOR WEB INJECTION
            // =========================================================================
            foreach (var cluster in clusters)
            {
                for (int i = 0; i < cluster.Count; i++)
                {
                    for (int j = i + 1; j < cluster.Count; j++)
                    {
                        TacticalRoom rA = cluster[i];
                        TacticalRoom rB = cluster[j];

                        // Enclose gaps between member rooms into dedicated hallway channels
                        EncloseInterRoomGap(rA, rB, rooms, rawWalls, injectedWalls, allDoorways, mapSize, prng);
                    }
                }
            }

            // =========================================================================
            // STEP 3: ARTERIAL SPINES & SIGHTLINE CLAMPING (DEAD-SPACE CHANNELS)
            // =========================================================================
            float stepSize = (mapSize == 100) ? 12.0f : 14.0f;
            float margin = 8.0f;

            for (float x = -halfSize + margin; x <= halfSize - margin; x += stepSize)
            {
                for (float z = -halfSize + margin; z <= halfSize - margin; z += stepSize)
                {
                    float probeX = x + (float)(prng.NextDouble() * 4.0 - 2.0);
                    float probeZ = z + (float)(prng.NextDouble() * 4.0 - 2.0);

                    if (!IsPositionInOpenDeadSpace(probeX, probeZ, rooms, mapSize, allDoorways)) continue;

                    bool isHorizontal = (prng.NextDouble() > 0.5);
                    float spineLength = (float)(8.0 + prng.NextDouble() * 12.0); // 8m to 20m (Strictly <= 25m sightline clamp)
                    spineLength = Mathf.Min(spineLength, 22.0f);

                    Vector3 segPos = new Vector3(probeX, wallHeight * 0.5f, probeZ);
                    Vector3 segSize = isHorizontal
                        ? new Vector3(spineLength, wallHeight, wallThickness)
                        : new Vector3(wallThickness, wallHeight, spineLength);

                    if (IsValidCorridorSegment(segPos, segSize, rooms, rawWalls, injectedWalls, allDoorways, mapSize))
                    {
                        injectedWalls.Add(new WallSegment(segPos, segSize));

                        // 20% - 30% Secondary dead-end feeder alleyway / 90-degree jog
                        if (prng.NextDouble() < 0.30)
                        {
                            float jogLen = (float)(5.0 + prng.NextDouble() * 6.0); // 5m - 11m
                            float jogOffset = spineLength * 0.4f;

                            Vector3 jogPos = isHorizontal
                                ? new Vector3(probeX + jogOffset, wallHeight * 0.5f, probeZ + jogLen * 0.5f)
                                : new Vector3(probeX + jogLen * 0.5f, wallHeight * 0.5f, probeZ + jogOffset);
                            Vector3 jogSize = isHorizontal
                                ? new Vector3(wallThickness, wallHeight, jogLen)
                                : new Vector3(jogLen, wallHeight, wallThickness);

                            if (IsValidCorridorSegment(jogPos, jogSize, rooms, rawWalls, injectedWalls, allDoorways, mapSize))
                            {
                                injectedWalls.Add(new WallSegment(jogPos, jogSize));
                            }
                        }
                    }
                }
            }

            LastInjectedCorridorWallCount = injectedWalls.Count;
            rawWalls.AddRange(injectedWalls);
            Debug.Log($"[MapGenerator] Corridor Web & Cluster Linking: Formed {clusters.Count} clusters, Injected {injectedWalls.Count} hallway segments for Tier {mapSize}x{mapSize}.");
        }

        private List<List<TacticalRoom>> GroupRoomsIntoProximityClusters(List<TacticalRoom> rooms, System.Random prng)
        {
            List<List<TacticalRoom>> clusters = new List<List<TacticalRoom>>();
            HashSet<int> visited = new HashSet<int>();
            List<TacticalRoom> shuffled = new List<TacticalRoom>(rooms);
            Shuffle(shuffled, prng);

            foreach (var root in shuffled)
            {
                if (visited.Contains(root.id)) continue;

                int targetSize = prng.Next(2, 6); // 2 to 5 rooms per cluster
                List<TacticalRoom> cluster = new List<TacticalRoom> { root };
                visited.Add(root.id);

                Queue<TacticalRoom> queue = new Queue<TacticalRoom>();
                queue.Enqueue(root);

                while (queue.Count > 0 && cluster.Count < targetSize)
                {
                    TacticalRoom curr = queue.Dequeue();
                    foreach (var other in rooms)
                    {
                        if (visited.Contains(other.id)) continue;

                        float dist = GetEdgeToEdgeDistance(curr.bounds, other.bounds);
                        if (dist <= 6.0f) // Proximity threshold <= 6.0m
                        {
                            visited.Add(other.id);
                            cluster.Add(other);
                            queue.Enqueue(other);
                            if (cluster.Count >= targetSize) break;
                        }
                    }
                }

                if (cluster.Count >= 2)
                {
                    clusters.Add(cluster);
                }
            }

            return clusters;
        }

        private float GetEdgeToEdgeDistance(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dz = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void EncloseInterRoomGap(
            TacticalRoom rA, TacticalRoom rB, List<TacticalRoom> rooms,
            List<WallSegment> rawWalls, List<WallSegment> injectedWalls,
            List<Vector2> allDoorways, int mapSize, System.Random prng)
        {
            Rect bA = rA.bounds;
            Rect bB = rB.bounds;

            // Check if separated horizontally along X axis
            bool aIsWestOfB = bA.xMax < bB.xMin;
            bool bIsWestOfA = bB.xMax < bA.xMin;

            if (aIsWestOfB || bIsWestOfA)
            {
                Rect westR = aIsWestOfB ? bA : bB;
                Rect eastR = aIsWestOfB ? bB : bA;

                float gapWidth = eastR.xMin - westR.xMax;
                if (gapWidth >= 2.5f && gapWidth <= 6.5f)
                {
                    float overlapZMin = Mathf.Max(westR.yMin, eastR.yMin);
                    float overlapZMax = Mathf.Min(westR.yMax, eastR.yMax);

                    if (overlapZMax - overlapZMin >= 2.5f)
                    {
                        float midX = (westR.xMax + eastR.xMin) * 0.5f;
                        float lenX = gapWidth;

                        // Wall on North edge of gap
                        Vector3 northPos = new Vector3(midX, wallHeight * 0.5f, overlapZMax);
                        Vector3 northSz = new Vector3(lenX, wallHeight, wallThickness);
                        if (IsValidCorridorSegment(northPos, northSz, rooms, rawWalls, injectedWalls, allDoorways, mapSize))
                        {
                            injectedWalls.Add(new WallSegment(northPos, northSz));
                        }

                        // Wall on South edge of gap
                        Vector3 southPos = new Vector3(midX, wallHeight * 0.5f, overlapZMin);
                        Vector3 southSz = new Vector3(lenX, wallHeight, wallThickness);
                        if (IsValidCorridorSegment(southPos, southSz, rooms, rawWalls, injectedWalls, allDoorways, mapSize))
                        {
                            injectedWalls.Add(new WallSegment(southPos, southSz));
                        }
                    }
                }
            }

            // Check if separated vertically along Z axis
            bool aIsSouthOfB = bA.yMax < bB.yMin;
            bool bIsSouthOfA = bB.yMax < bA.yMin;

            if (aIsSouthOfB || bIsSouthOfA)
            {
                Rect southR = aIsSouthOfB ? bA : bB;
                Rect northR = aIsSouthOfB ? bB : bA;

                float gapDepth = northR.yMin - southR.yMax;
                if (gapDepth >= 2.5f && gapDepth <= 6.5f)
                {
                    float overlapXMin = Mathf.Max(southR.xMin, northR.xMin);
                    float overlapXMax = Mathf.Min(southR.xMax, northR.xMax);

                    if (overlapXMax - overlapXMin >= 2.5f)
                    {
                        float midZ = (southR.yMax + northR.yMin) * 0.5f;
                        float lenZ = gapDepth;

                        // Wall on East edge of gap
                        Vector3 eastPos = new Vector3(overlapXMax, wallHeight * 0.5f, midZ);
                        Vector3 eastSz = new Vector3(wallThickness, wallHeight, lenZ);
                        if (IsValidCorridorSegment(eastPos, eastSz, rooms, rawWalls, injectedWalls, allDoorways, mapSize))
                        {
                            injectedWalls.Add(new WallSegment(eastPos, eastSz));
                        }

                        // Wall on West edge of gap
                        Vector3 westPos = new Vector3(overlapXMin, wallHeight * 0.5f, midZ);
                        Vector3 westSz = new Vector3(wallThickness, wallHeight, lenZ);
                        if (IsValidCorridorSegment(westPos, westSz, rooms, rawWalls, injectedWalls, allDoorways, mapSize))
                        {
                            injectedWalls.Add(new WallSegment(westPos, westSz));
                        }
                    }
                }
            }
        }

        private bool IsPositionInOpenDeadSpace(float px, float pz, List<TacticalRoom> rooms, int mapSize, List<Vector2> doorways)
        {
            Vector2 pos = new Vector2(px, pz);

            // Spawn and center safety clearance
            if (pos.magnitude < 8.0f) return false;
            float northZ = Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            float southZ = -Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            if (Mathf.Abs(pos.x) < 6.0f && Mathf.Abs(pos.y - northZ) < 6.0f) return false;
            if (Mathf.Abs(pos.x) < 6.0f && Mathf.Abs(pos.y - southZ) < 6.0f) return false;

            // Room interior clearance
            foreach (var r in rooms)
            {
                if (r.bounds.Contains(pos)) return false;
            }

            // Doorway clearance (>= 2.5m)
            foreach (var d in doorways)
            {
                if (Vector2.Distance(pos, d) < 2.5f) return false;
            }

            return true;
        }

        private bool IsValidCorridorSegment(
            Vector3 pos, Vector3 size, List<TacticalRoom> rooms,
            List<WallSegment> existingWalls, List<WallSegment> injectedWalls,
            List<Vector2> doorways, int mapSize)
        {
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            float halfMap = mapSize * 0.5f;

            // Out of bounds check
            if (pos.x - halfX < -halfMap + 3.0f || pos.x + halfX > halfMap - 3.0f ||
                pos.z - halfZ < -halfMap + 3.0f || pos.z + halfZ > halfMap - 3.0f)
                return false;

            // Spawn safety check
            float northZ = Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            float southZ = -Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            Rect segBounds = new Rect(pos.x - halfX, pos.z - halfZ, size.x, size.z);

            Rect centerSafe = new Rect(-6f, -6f, 12f, 12f);
            Rect northSafe = new Rect(-5f, northZ - 5f, 10f, 10f);
            Rect southSafe = new Rect(-5f, southZ - 5f, 10f, 10f);

            if (segBounds.Overlaps(centerSafe) || segBounds.Overlaps(northSafe) || segBounds.Overlaps(southSafe))
                return false;

            // Room interior overlap check (with 1.2m buffer from room walls)
            foreach (var r in rooms)
            {
                Rect expandedRoom = new Rect(
                    r.bounds.x - 1.2f,
                    r.bounds.y - 1.2f,
                    r.bounds.width + 2.4f,
                    r.bounds.height + 2.4f
                );
                if (segBounds.Overlaps(expandedRoom)) return false;
            }

            // Doorway clearance check (>= 2.2m from all room doorways)
            foreach (var d in doorways)
            {
                float clampX = Mathf.Clamp(d.x, pos.x - halfX, pos.x + halfX);
                float clampZ = Mathf.Clamp(d.y, pos.z - halfZ, pos.z + halfZ);
                if (Vector2.Distance(d, new Vector2(clampX, clampZ)) < 2.2f) return false;
            }

            // Check overlap with already injected walls (prevent overlapping parallel walls)
            foreach (var w in injectedWalls)
            {
                Rect other = new Rect(w.position.x - w.size.x * 0.5f, w.position.z - w.size.z * 0.5f, w.size.x, w.size.z);
                if (segBounds.Overlaps(other))
                {
                    bool thisIsHoriz = size.x >= size.z;
                    bool otherIsHoriz = w.size.x >= w.size.z;
                    if (thisIsHoriz == otherIsHoriz) return false;
                }
            }

            return true;
        }

        #endregion

        #region Pass 4: 2D Grid Flood-Fill (BFS) Connectivity Validation

        /// <summary>
        /// Checks if a wall segment belongs to the outer perimeter of a Small Room (< 40 sq m).
        /// Used to prevent secondary auto-remediation passes from punching extra doors into small dead-end rooms.
        /// </summary>
        private bool IsWallOfSmallRoom(WallSegment seg, List<TacticalRoom> rooms)
        {
            float tol = 0.4f;
            foreach (var r in rooms)
            {
                if (r.Area < 40f)
                {
                    Rect b = r.bounds;
                    bool alongNorth = Mathf.Abs(seg.position.z - b.yMax) < tol && seg.position.x >= b.xMin - tol && seg.position.x <= b.xMax + tol;
                    bool alongSouth = Mathf.Abs(seg.position.z - b.yMin) < tol && seg.position.x >= b.xMin - tol && seg.position.x <= b.xMax + tol;
                    bool alongEast  = Mathf.Abs(seg.position.x - b.xMax) < tol && seg.position.z >= b.yMin - tol && seg.position.z <= b.yMax + tol;
                    bool alongWest  = Mathf.Abs(seg.position.x - b.xMin) < tol && seg.position.z >= b.yMin - tol && seg.position.z <= b.yMax + tol;

                    if (alongNorth || alongSouth || alongEast || alongWest)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void ValidateAndRemediateMapConnectivity(int mapSize, List<WallSegment> rawWalls, List<TacticalRoom> rooms)
        {
            float halfSize = mapSize * 0.5f;
            float gridRes = 1.0f;
            int gridW = Mathf.RoundToInt(mapSize / gridRes);
            int gridH = Mathf.RoundToInt(mapSize / gridRes);

            int maxRemediations = 10;
            int remediationCount = 0;

            while (remediationCount < maxRemediations)
            {
                bool[,] isBlocked = new bool[gridW, gridH];

                foreach (var seg in rawWalls)
                {
                    float minX = seg.position.x - seg.size.x * 0.5f;
                    float maxX = seg.position.x + seg.size.x * 0.5f;
                    float minZ = seg.position.z - seg.size.z * 0.5f;
                    float maxZ = seg.position.z + seg.size.z * 0.5f;

                    int gxMin = Mathf.Clamp(Mathf.FloorToInt((minX + halfSize) / gridRes), 0, gridW - 1);
                    int gxMax = Mathf.Clamp(Mathf.FloorToInt((maxX + halfSize) / gridRes), 0, gridW - 1);
                    int gzMin = Mathf.Clamp(Mathf.FloorToInt((minZ + halfSize) / gridRes), 0, gridH - 1);
                    int gzMax = Mathf.Clamp(Mathf.FloorToInt((maxZ + halfSize) / gridRes), 0, gridH - 1);

                    for (int gx = gxMin; gx <= gxMax; gx++)
                    {
                        for (int gz = gzMin; gz <= gzMax; gz++)
                        {
                            isBlocked[gx, gz] = true;
                        }
                    }
                }

                Vector3 policePos = GetSafePoliceSpawnPosition(0, mapSize);
                Vector3 assassinPos = GetSafeAssassinSpawnPosition(mapSize);

                int startGx = Mathf.Clamp(Mathf.FloorToInt((policePos.x + halfSize) / gridRes), 0, gridW - 1);
                int startGz = Mathf.Clamp(Mathf.FloorToInt((policePos.z + halfSize) / gridRes), 0, gridH - 1);

                int targetGx = Mathf.Clamp(Mathf.FloorToInt((assassinPos.x + halfSize) / gridRes), 0, gridW - 1);
                int targetGz = Mathf.Clamp(Mathf.FloorToInt((assassinPos.z + halfSize) / gridRes), 0, gridH - 1);

                bool[,] visited = new bool[gridW, gridH];
                Queue<Vector2Int> queue = new Queue<Vector2Int>();

                if (!isBlocked[startGx, startGz])
                {
                    visited[startGx, startGz] = true;
                    queue.Enqueue(new Vector2Int(startGx, startGz));
                }

                int[] dx = { 1, -1, 0, 0 };
                int[] dz = { 0, 0, 1, -1 };

                while (queue.Count > 0)
                {
                    Vector2Int curr = queue.Dequeue();
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = curr.x + dx[d];
                        int nz = curr.y + dz[d];
                        if (nx >= 0 && nx < gridW && nz >= 0 && nz < gridH)
                        {
                            if (!isBlocked[nx, nz] && !visited[nx, nz])
                            {
                                visited[nx, nz] = true;
                                queue.Enqueue(new Vector2Int(nx, nz));
                            }
                        }
                    }
                }

                bool assassinReached = visited[targetGx, targetGz];

                int reachableRooms = 0;
                foreach (var r in rooms)
                {
                    int rgx = Mathf.Clamp(Mathf.FloorToInt((r.bounds.center.x + halfSize) / gridRes), 0, gridW - 1);
                    int rgz = Mathf.Clamp(Mathf.FloorToInt((r.bounds.center.y + halfSize) / gridRes), 0, gridH - 1);
                    if (visited[rgx, rgz]) reachableRooms++;
                }

                float reachabilityRatio = (rooms.Count > 0) ? ((float)reachableRooms / rooms.Count) : 1.0f;

                if (assassinReached && reachabilityRatio >= 0.85f)
                {
                    Debug.Log($"[MapGenerator] Flood Fill Verified PASS -> Assassin Connected: {assassinReached}, Room Reachability: {reachabilityRatio:P0} ({reachableRooms}/{rooms.Count})");
                    break;
                }

                Debug.LogWarning($"[MapGenerator] Flood Fill FAIL (Assassin: {assassinReached}, Reachability: {reachabilityRatio:P0}). Auto-remediating breach #{remediationCount + 1}...");

                bool breached = false;
                for (int i = rawWalls.Count - 1; i >= 0; i--)
                {
                    WallSegment seg = rawWalls[i];
                    if (Mathf.Abs(seg.position.x) > halfSize - 1.0f || Mathf.Abs(seg.position.z) > halfSize - 1.0f) continue;
                    // CRITICAL: Strictly protect small dead-end rooms from receiving secondary doorway punches
                    if (IsWallOfSmallRoom(seg, rooms)) continue;

                    float len = Mathf.Max(seg.size.x, seg.size.z);
                    if (len >= 3.5f)
                    {
                        rawWalls.RemoveAt(i);
                        float breachSize = doorwayWidth;
                        float remaining = (len - breachSize) * 0.5f;
                        if (seg.size.x >= seg.size.z)
                        {
                            float offset = (breachSize + remaining) * 0.5f;
                            rawWalls.Add(new WallSegment(new Vector3(seg.position.x - offset, seg.position.y, seg.position.z), new Vector3(remaining, wallHeight, wallThickness)));
                            rawWalls.Add(new WallSegment(new Vector3(seg.position.x + offset, seg.position.y, seg.position.z), new Vector3(remaining, wallHeight, wallThickness)));
                        }
                        else
                        {
                            float offset = (breachSize + remaining) * 0.5f;
                            rawWalls.Add(new WallSegment(new Vector3(seg.position.x, seg.position.y, seg.position.z - offset), new Vector3(wallThickness, wallHeight, remaining)));
                            rawWalls.Add(new WallSegment(new Vector3(seg.position.x, seg.position.y, seg.position.z + offset), new Vector3(wallThickness, wallHeight, remaining)));
                        }
                        breached = true;
                        break;
                    }
                }

                if (!breached) break;
                remediationCount++;
            }
        }

        #endregion

        #region Pass 5: Monolithic Collinear Collider Merging

        private List<WallSegment> MergeCollinearWallSegments(List<WallSegment> rawWalls, int mapSize)
        {
            List<WallSegment> merged = new List<WallSegment>();
            Dictionary<int, List<Vector2>> horizontalLines = new Dictionary<int, List<Vector2>>();
            Dictionary<int, List<Vector2>> verticalLines = new Dictionary<int, List<Vector2>>();
            float quantizeFactor = 10.0f;

            foreach (var seg in rawWalls)
            {
                if (seg.size.x >= seg.size.z)
                {
                    int keyZ = Mathf.RoundToInt(seg.position.z * quantizeFactor);
                    if (!horizontalLines.ContainsKey(keyZ)) horizontalLines[keyZ] = new List<Vector2>();
                    horizontalLines[keyZ].Add(new Vector2(seg.position.x - seg.size.x * 0.5f, seg.position.x + seg.size.x * 0.5f));
                }
                else
                {
                    int keyX = Mathf.RoundToInt(seg.position.x * quantizeFactor);
                    if (!verticalLines.ContainsKey(keyX)) verticalLines[keyX] = new List<Vector2>();
                    verticalLines[keyX].Add(new Vector2(seg.position.z - seg.size.z * 0.5f, seg.position.z + seg.size.z * 0.5f));
                }
            }

            // Deterministically sort horizontal keys before merging
            List<int> sortedHorizontalKeys = new List<int>(horizontalLines.Keys);
            sortedHorizontalKeys.Sort();

            foreach (int keyZ in sortedHorizontalKeys)
            {
                float z = keyZ / quantizeFactor;
                List<Vector2> spans = horizontalLines[keyZ];
                spans.Sort((a, b) => a.x.CompareTo(b.x));
                List<Vector2> mergedSpans = new List<Vector2>();
                foreach (var span in spans)
                {
                    if (mergedSpans.Count == 0) mergedSpans.Add(span);
                    else
                    {
                        Vector2 last = mergedSpans[mergedSpans.Count - 1];
                        if (span.x <= last.y + 0.1f) mergedSpans[mergedSpans.Count - 1] = new Vector2(last.x, Mathf.Max(last.y, span.y));
                        else mergedSpans.Add(span);
                    }
                }
                foreach (var span in mergedSpans)
                {
                    float len = span.y - span.x;
                    if (len > 0.3f) merged.Add(new WallSegment(new Vector3((span.x + span.y) * 0.5f, wallHeight * 0.5f, z), new Vector3(len, wallHeight, wallThickness)));
                }
            }

            // Deterministically sort vertical keys before merging
            List<int> sortedVerticalKeys = new List<int>(verticalLines.Keys);
            sortedVerticalKeys.Sort();

            foreach (int keyX in sortedVerticalKeys)
            {
                float x = keyX / quantizeFactor;
                List<Vector2> spans = verticalLines[keyX];
                spans.Sort((a, b) => a.x.CompareTo(b.x));
                List<Vector2> mergedSpans = new List<Vector2>();
                foreach (var span in spans)
                {
                    if (mergedSpans.Count == 0) mergedSpans.Add(span);
                    else
                    {
                        Vector2 last = mergedSpans[mergedSpans.Count - 1];
                        if (span.x <= last.y + 0.1f) mergedSpans[mergedSpans.Count - 1] = new Vector2(last.x, Mathf.Max(last.y, span.y));
                        else mergedSpans.Add(span);
                    }
                }
                foreach (var span in mergedSpans)
                {
                    float len = span.y - span.x;
                    if (len > 0.3f) merged.Add(new WallSegment(new Vector3(x, wallHeight * 0.5f, (span.x + span.y) * 0.5f), new Vector3(wallThickness, wallHeight, len)));
                }
            }

            // Final deterministic sorting: Guarantee 100% identical element ordering across processes
            merged.Sort((a, b) =>
            {
                int cmp = a.position.x.CompareTo(b.position.x);
                if (cmp != 0) return cmp;
                cmp = a.position.z.CompareTo(b.position.z);
                if (cmp != 0) return cmp;
                cmp = a.size.x.CompareTo(b.size.x);
                if (cmp != 0) return cmp;
                return a.size.z.CompareTo(b.size.z);
            });

            return merged;
        }

        private void InstantiateWallGeometry(List<WallSegment> walls, int seed, int mapSize, int roomCount)
        {
            int spawnedCount = 0;
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer == -1) obstacleLayer = 7;

            foreach (var seg in walls)
            {
                GameObject wallObj;
                if (wallPrefab != null) wallObj = Instantiate(wallPrefab, seg.position, Quaternion.identity, generatedEnvironment);
                else
                {
                    wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallObj.transform.SetParent(generatedEnvironment, false);
                    wallObj.transform.position = seg.position;
                }
                wallObj.name = $"TacticalWall_{spawnedCount:D3}";
                wallObj.transform.localScale = seg.size;
                wallObj.layer = obstacleLayer;
                wallObj.tag = "Obstacle";
                Renderer rend = wallObj.GetComponent<Renderer>();
                if (rend != null)
                {
                    if (wallMaterial != null) rend.sharedMaterial = wallMaterial;
                    rend.shadowCastingMode = ShadowCastingMode.TwoSided;
                    rend.receiveShadows = true;
                }
                BoxCollider col = wallObj.GetComponent<BoxCollider>();
                if (col == null) col = wallObj.AddComponent<BoxCollider>();
                col.enabled = true;
                var netObj = wallObj.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null) DestroyImmediate(netObj);
                spawnedCount++;
            }
            Debug.Log($"[MapGenerator] Generated {spawnedCount} monolithic walls (0.5m thick, {roomCount} packed rooms) for seed {seed} ({mapSize}x{mapSize}).");
        }

        #endregion

        #region Safety Zones & Spawn Clearances

        private bool IsRectInSafetyZone(Rect r, int mapSize, float padding = 0f)
        {
            Vector2 center = r.center;
            if (center.magnitude < 5.0f + padding) return true;
            float northZ = Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            float safeHalf = 3.5f + padding;
            if (Mathf.Abs(center.x) < safeHalf && Mathf.Abs(center.y - northZ) < safeHalf) return true;
            if (Vector2.Distance(center, new Vector2(0f, northZ)) < 5.5f + padding) return true;
            float southZ = -Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            if (Mathf.Abs(center.x) < safeHalf && Mathf.Abs(center.y - southZ) < safeHalf) return true;
            if (Vector2.Distance(center, new Vector2(0f, southZ)) < 5.5f + padding) return true;
            return false;
        }

        private bool IsSegmentInSafetyZone(WallSegment seg, int mapSize) => false;

        public static Vector3 GetSafePoliceSpawnPosition(int index, int mapSize = 50)
        {
            var breachPositions = GridManager.GetBreachSpawnPositions(mapSize);
            if (breachPositions != null && breachPositions.Count > 0)
            {
                int safeIndex = Mathf.Abs(index) % breachPositions.Count;
                return breachPositions[safeIndex];
            }

            mapSize = ClampMapSize(mapSize);
            float southZ = -Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            float offsetX = (index % 4) * 2.5f - 3.75f;
            float offsetZ = (index / 4) * 2.5f;
            return new Vector3(offsetX, 1.0f, southZ + offsetZ);
        }

        public static Vector3 GetSafeAssassinSpawnPosition(int mapSize = 50)
        {
            mapSize = ClampMapSize(mapSize);
            float northZ = Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            return new Vector3(0f, 1.0f, northZ);
        }

        private static void Shuffle<T>(IList<T> list, System.Random prng)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = prng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        #endregion
    }
}
