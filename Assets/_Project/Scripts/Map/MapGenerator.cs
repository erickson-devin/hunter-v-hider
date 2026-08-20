using System;
using System.Collections.Generic;
using UnityEngine;
using HunterVsHider.Managers;

namespace HunterVsHider.Map
{
    /// <summary>
    /// Deterministic procedural tactical map generator.
    /// Generates player-scaled rooms (6m–14m) and a dense web of variable-width corridors
    /// (tight chokes 2–2.5m, standard 3–3.5m, open thoroughfares 4.5–6m) with thin 0.5-unit walls.
    /// Walls are strictly local geometry (no NetworkObject).
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        public static MapGenerator Instance { get; private set; }

        [Header("Prefab & Materials")]
        [Tooltip("Optional custom prefab for wall segments (Prefab_TacticalWall). If null, standard 3D primitive Cube is instantiated.")]
        public GameObject wallPrefab;

        [Tooltip("Material applied to generated wall segments (Mat_TacticalWall).")]
        public Material wallMaterial;

        [Header("Player-Scale Room Constraints")]
        [Tooltip("Minimum room width in meters (player-scale).")]
        public float minRoomWidth = 6.0f;

        [Tooltip("Maximum room width in meters (player-scale).")]
        public float maxRoomWidth = 14.0f;

        [Tooltip("Minimum room height/depth in meters (player-scale).")]
        public float minRoomHeight = 6.0f;

        [Tooltip("Maximum room height/depth in meters (player-scale).")]
        public float maxRoomHeight = 14.0f;

        [Header("Tactical Architectural Dimensions")]
        [Tooltip("Standard thickness of all structural walls in meters (0.5m).")]
        public float wallThickness = 0.5f;

        [Tooltip("Height of generated walls in meters (3.0m).")]
        public float wallHeight = 3.0f;

        [Tooltip("Percentage of interior walls breached/removed for tactical looping paths (0.18 = 18%).")]
        [Range(0.1f, 0.35f)]
        public float punchThroughRatio = 0.18f;

        [Header("Hierarchy Organization")]
        [Tooltip("Container Transform under which all spawned walls are parented.")]
        public Transform generatedEnvironment;

        private int currentGeneratedSeed = -1;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            EnsureContainer();
        }

        private void Start()
        {
            EnsureContainer();

            if (MatchManager.Instance != null)
            {
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

        /// <summary>
        /// Clears all existing spawned wall geometry from the container.
        /// </summary>
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

        private struct WallSegment
        {
            public Vector3 position;
            public Vector3 size; // (sizeX, sizeY, sizeZ)

            public WallSegment(Vector3 pos, Vector3 sz)
            {
                position = pos;
                size = sz;
            }
        }

        private class BSPNode
        {
            public Rect bounds;
            public BSPNode left;
            public BSPNode right;
            public Rect room;
            public float hallwayWidth;
            public float hallwayHeight;

            public bool IsLeaf => left == null && right == null;

            public BSPNode(Rect b)
            {
                bounds = b;
                left = null;
                right = null;
                room = Rect.zero;
                hallwayWidth = 3.0f;
                hallwayHeight = 3.0f;
            }

            public bool Split(System.Random prng, float minSize)
            {
                if (!IsLeaf) return false;

                bool splitHorizontal;
                if (bounds.width / bounds.height >= 1.25f)
                    splitHorizontal = false; // Split vertically across X
                else if (bounds.height / bounds.width >= 1.25f)
                    splitHorizontal = true;  // Split horizontally across Z
                else
                    splitHorizontal = prng.NextDouble() > 0.5;

                float maxSplit = (splitHorizontal ? bounds.height : bounds.width) - minSize;
                if (maxSplit <= minSize) return false;

                float split = (float)(minSize + prng.NextDouble() * (maxSplit - minSize));

                if (splitHorizontal)
                {
                    left = new BSPNode(new Rect(bounds.x, bounds.y, bounds.width, split));
                    right = new BSPNode(new Rect(bounds.x, bounds.y + split, bounds.width, bounds.height - split));
                }
                else
                {
                    left = new BSPNode(new Rect(bounds.x, bounds.y, split, bounds.height));
                    right = new BSPNode(new Rect(bounds.x + split, bounds.y, bounds.width - split, bounds.height));
                }

                return true;
            }
        }

        private float GetRandomHallwayWidth(System.Random prng)
        {
            double roll = prng.NextDouble();
            if (roll < 0.25)
            {
                // 25% Tight Choke Points (2.0 to 2.5 units)
                return 2.0f + (float)prng.NextDouble() * 0.5f;
            }
            else if (roll < 0.75)
            {
                // 50% Standard Corridors (3.0 to 3.5 units)
                return 3.0f + (float)prng.NextDouble() * 0.5f;
            }
            else
            {
                // 25% Main Thoroughfares / Open Hallways (4.5 to 6.0 units)
                return 4.5f + (float)prng.NextDouble() * 1.5f;
            }
        }

        /// <summary>
        /// Deterministically generates structured player-scaled rooms (6m–14m) and variable-width corridors
        /// using deep BSP partitioning, unbroken outer boundary walls, and a tactical punch-through pass.
        /// </summary>
        /// <param name="seed">Synchronized random integer seed.</param>
        public void GenerateMap(int seed)
        {
            if (seed <= 0) return;

            PurgeLegacyDummies();
            ClearMap();
            EnsureContainer();

            currentGeneratedSeed = seed;

            int mapSize = (MatchManager.Singleton != null) ? MatchManager.Singleton.SelectedMapSize : 50;
            if (mapSize <= 0) mapSize = 50;

            Debug.Log($"[MapGenerator] Generating player-scale BSP map -> Seed: {seed}, Size: {mapSize}x{mapSize}");

            System.Random prng = new System.Random(seed);
            List<WallSegment> finalWalls = new List<WallSegment>();

            float halfSize = mapSize * 0.5f;
            float halfThick = wallThickness * 0.5f;

            // =========================================================================
            // 1. UNBROKEN OUTER BOUNDARY PERIMETER (GUARANTEED FOR ALL SIZES 50, 100, 250)
            // =========================================================================
            // North Boundary
            finalWalls.Add(new WallSegment(
                new Vector3(0f, wallHeight * 0.5f, halfSize - halfThick),
                new Vector3(mapSize, wallHeight, wallThickness)
            ));
            // South Boundary
            finalWalls.Add(new WallSegment(
                new Vector3(0f, wallHeight * 0.5f, -halfSize + halfThick),
                new Vector3(mapSize, wallHeight, wallThickness)
            ));
            // West Boundary
            finalWalls.Add(new WallSegment(
                new Vector3(-halfSize + halfThick, wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, mapSize - (wallThickness * 2f))
            ));
            // East Boundary
            finalWalls.Add(new WallSegment(
                new Vector3(halfSize - halfThick, wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, mapSize - (wallThickness * 2f))
            ));

            // =========================================================================
            // 2. PLAYER-SCALE BSP PARTITIONING (DEEP SUBDIVISION UNTIL LEAVES <= 18M)
            // =========================================================================
            float activeMargin = 2.5f; // Margin inside boundary wall
            Rect rootArea = new Rect(-halfSize + activeMargin, -halfSize + activeMargin, mapSize - (activeMargin * 2f), mapSize - (activeMargin * 2f));
            BSPNode rootNode = new BSPNode(rootArea);

            float minLeafSplit = minRoomWidth + 2.0f; // 8.0f minimum child size
            float maxLeafAllowed = maxRoomWidth + 4.0f; // 18.0f max leaf before splitting

            int maxDepth = (mapSize >= 200) ? 9 : ((mapSize >= 90) ? 7 : 5);
            int currentDepth = 0;
            bool didSplit = true;

            while (didSplit && currentDepth < maxDepth)
            {
                didSplit = false;
                List<BSPNode> currentLeaves = new List<BSPNode>();
                GetLeaves(rootNode, currentLeaves);

                foreach (var node in currentLeaves)
                {
                    if (node.IsLeaf)
                    {
                        if (node.bounds.width > maxLeafAllowed || node.bounds.height > maxLeafAllowed)
                        {
                            if (node.Split(prng, minLeafSplit))
                            {
                                didSplit = true;
                            }
                        }
                    }
                }
                currentDepth++;
            }

            // =========================================================================
            // 3. HUMAN-SCALE ROOMS & VARIABLE-WIDTH CORRIDORS
            // =========================================================================
            List<BSPNode> leaves = new List<BSPNode>();
            GetLeaves(rootNode, leaves);

            List<WallSegment> rawInteriorWalls = new List<WallSegment>();

            foreach (var leaf in leaves)
            {
                // Dynamic variable hallway widths
                float hallwayW = GetRandomHallwayWidth(prng);
                float hallwayH = GetRandomHallwayWidth(prng);
                leaf.hallwayWidth = hallwayW;
                leaf.hallwayHeight = hallwayH;

                // Player-scale room clamping (6.0m to 14.0m)
                float roomWidth = Mathf.Clamp((float)(leaf.bounds.width - hallwayW), minRoomWidth, maxRoomWidth);
                float roomHeight = Mathf.Clamp((float)(leaf.bounds.height - hallwayH), minRoomHeight, maxRoomHeight);

                // Center room inside leaf bounds, preserving hallway clearances
                float roomX = leaf.bounds.x + (leaf.bounds.width - roomWidth) * 0.5f;
                float roomZ = leaf.bounds.y + (leaf.bounds.height - roomHeight) * 0.5f;

                leaf.room = new Rect(roomX, roomZ, roomWidth, roomHeight);
                Rect r = leaf.room;
                if (r.width < minRoomWidth * 0.8f || r.height < minRoomHeight * 0.8f) continue;

                // Doorway width scaled to corridor
                float doorway = Mathf.Clamp(hallwayW * 0.9f, 2.5f, 3.5f);

                // Add perimeter walls with doorways
                AddHorizontalWallWithDoor(rawInteriorWalls, r.xMin, r.xMax, r.yMax, doorway, prng);
                AddHorizontalWallWithDoor(rawInteriorWalls, r.xMin, r.xMax, r.yMin, doorway, prng);
                AddVerticalWallWithDoor(rawInteriorWalls, r.xMin, r.yMin, r.yMax, doorway, prng);
                AddVerticalWallWithDoor(rawInteriorWalls, r.xMax, r.yMin, r.yMax, doorway, prng);

                // Small tactical column or half-wall in larger rooms (11m+)
                if (r.width >= 11.0f && r.height >= 11.0f && prng.NextDouble() < 0.4)
                {
                    float cx = r.center.x;
                    float cz = r.center.y;
                    float colLen = 2.5f;
                    if (prng.NextDouble() > 0.5)
                    {
                        rawInteriorWalls.Add(new WallSegment(new Vector3(cx, wallHeight * 0.5f, cz), new Vector3(colLen, wallHeight, wallThickness)));
                    }
                    else
                    {
                        rawInteriorWalls.Add(new WallSegment(new Vector3(cx, wallHeight * 0.5f, cz), new Vector3(wallThickness, wallHeight, colLen)));
                    }
                }
            }

            // =========================================================================
            // 4. SECONDARY PUNCH-THROUGH PASS (18% FOR NON-LINEAR TACTICAL LOOPS)
            // =========================================================================
            foreach (var wall in rawInteriorWalls)
            {
                // Safety zone filtering: Do not spawn walls inside spawn zones
                if (IsSegmentInSafetyZone(wall, mapSize))
                {
                    continue;
                }

                // Random punch-through check
                if (prng.NextDouble() < punchThroughRatio)
                {
                    // If wall is long (> 5m), punch a 3.0m breach hole in the middle (split into 2 pieces)
                    float length = Mathf.Max(wall.size.x, wall.size.z);
                    if (length > 5.0f)
                    {
                        float holeSize = 3.0f;
                        float remaining = (length - holeSize) * 0.5f;
                        if (remaining > 0.5f)
                        {
                            if (wall.size.x > wall.size.z)
                            {
                                // Horizontal wall
                                float xOffset = (holeSize + remaining) * 0.5f;
                                finalWalls.Add(new WallSegment(new Vector3(wall.position.x - xOffset, wall.position.y, wall.position.z), new Vector3(remaining, wallHeight, wallThickness)));
                                finalWalls.Add(new WallSegment(new Vector3(wall.position.x + xOffset, wall.position.y, wall.position.z), new Vector3(remaining, wallHeight, wallThickness)));
                            }
                            else
                            {
                                // Vertical wall
                                float zOffset = (holeSize + remaining) * 0.5f;
                                finalWalls.Add(new WallSegment(new Vector3(wall.position.x, wall.position.y, wall.position.z - zOffset), new Vector3(wallThickness, wallHeight, remaining)));
                                finalWalls.Add(new WallSegment(new Vector3(wall.position.x, wall.position.y, wall.position.z + zOffset), new Vector3(wallThickness, wallHeight, remaining)));
                            }
                        }
                    }
                    // If wall is short (<= 5.0m), omit it completely to open up a corridor flank route!
                    continue;
                }

                finalWalls.Add(wall);
            }

            // =========================================================================
            // 5. INSTANTIATE WALL GEOMETRY (0.5M THICKNESS)
            // =========================================================================
            int spawnedCount = 0;
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer == -1) obstacleLayer = 7;

            foreach (var seg in finalWalls)
            {
                GameObject wallObj;
                if (wallPrefab != null)
                {
                    wallObj = Instantiate(wallPrefab, seg.position, Quaternion.identity, generatedEnvironment);
                }
                else
                {
                    wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallObj.transform.SetParent(generatedEnvironment, false);
                    wallObj.transform.position = seg.position;
                    wallObj.transform.rotation = Quaternion.identity;
                }

                wallObj.name = $"TacticalWall_{spawnedCount:D3}";
                wallObj.transform.localScale = seg.size;
                wallObj.layer = obstacleLayer;
                wallObj.tag = "Obstacle";

                Renderer rend = wallObj.GetComponent<Renderer>();
                if (rend != null)
                {
                    if (wallMaterial != null) rend.sharedMaterial = wallMaterial;
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
                    rend.receiveShadows = true;
                }

                BoxCollider col = wallObj.GetComponent<BoxCollider>();
                if (col == null) col = wallObj.AddComponent<BoxCollider>();
                col.enabled = true;

                // Ensure NO NetworkObject
                var netObj = wallObj.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null) DestroyImmediate(netObj);

                spawnedCount++;
            }

            Debug.Log($"[MapGenerator] Successfully generated {spawnedCount} structural walls (0.5m thick, player-scale 6m–14m rooms) for seed {seed} ({mapSize}x{mapSize}).");
        }

        private void GetLeaves(BSPNode node, List<BSPNode> leaves)
        {
            if (node == null) return;
            if (node.IsLeaf)
            {
                leaves.Add(node);
            }
            else
            {
                GetLeaves(node.left, leaves);
                GetLeaves(node.right, leaves);
            }
        }

        private void AddHorizontalWallWithDoor(List<WallSegment> segments, float xMin, float xMax, float z, float doorWidth, System.Random prng)
        {
            float length = xMax - xMin;
            if (length <= doorWidth + 1.2f)
            {
                segments.Add(new WallSegment(new Vector3((xMin + xMax) * 0.5f, wallHeight * 0.5f, z), new Vector3(length, wallHeight, wallThickness)));
                return;
            }

            float doorStart = xMin + (length - doorWidth) * 0.5f;
            float doorEnd = doorStart + doorWidth;

            float leftLen = doorStart - xMin;
            if (leftLen > 0.4f)
            {
                segments.Add(new WallSegment(new Vector3((xMin + doorStart) * 0.5f, wallHeight * 0.5f, z), new Vector3(leftLen, wallHeight, wallThickness)));
            }

            float rightLen = xMax - doorEnd;
            if (rightLen > 0.4f)
            {
                segments.Add(new WallSegment(new Vector3((doorEnd + xMax) * 0.5f, wallHeight * 0.5f, z), new Vector3(rightLen, wallHeight, wallThickness)));
            }
        }

        private void AddVerticalWallWithDoor(List<WallSegment> segments, float x, float zMin, float zMax, float doorWidth, System.Random prng)
        {
            float length = zMax - zMin;
            if (length <= doorWidth + 1.2f)
            {
                segments.Add(new WallSegment(new Vector3(x, wallHeight * 0.5f, (zMin + zMax) * 0.5f), new Vector3(wallThickness, wallHeight, length)));
                return;
            }

            float doorStart = zMin + (length - doorWidth) * 0.5f;
            float doorEnd = doorStart + doorWidth;

            float botLen = doorStart - zMin;
            if (botLen > 0.4f)
            {
                segments.Add(new WallSegment(new Vector3(x, wallHeight * 0.5f, (zMin + doorStart) * 0.5f), new Vector3(wallThickness, wallHeight, botLen)));
            }

            float topLen = zMax - doorEnd;
            if (topLen > 0.4f)
            {
                segments.Add(new WallSegment(new Vector3(x, wallHeight * 0.5f, (doorEnd + zMax) * 0.5f), new Vector3(wallThickness, wallHeight, topLen)));
            }
        }

        private bool IsSegmentInSafetyZone(WallSegment seg, int mapSize)
        {
            Vector2 segPos = new Vector2(seg.position.x, seg.position.z);

            // 1. Center Safety Zone (Radius 5m)
            if (segPos.magnitude < 5.0f) return true;

            // 2. North Spawn (Assassin Combat Spawn)
            float northZ = Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            if (Vector2.Distance(segPos, new Vector2(0f, northZ)) < 5.5f) return true;

            // 3. South Spawn (Police Combat Spawn)
            float southZ = -Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            if (Vector2.Distance(segPos, new Vector2(0f, southZ)) < 5.5f) return true;

            return false;
        }

        /// <summary>
        /// Calculates a safe spawn coordinate for Police inside the procedurally generated maze.
        /// Guaranteed to be in a cleared corridor / safety zone slightly above the floor (Y = 1.0f).
        /// </summary>
        public static Vector3 GetSafePoliceSpawnPosition(int index, int mapSize = 50)
        {
            float southZ = -Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            float offsetX = (index % 4) * 2.5f - 3.75f;
            float offsetZ = (index / 4) * 2.5f;
            return new Vector3(offsetX, 1.0f, southZ + offsetZ);
        }

        /// <summary>
        /// Calculates a safe spawn coordinate for the Assassin inside the procedurally generated maze.
        /// </summary>
        public static Vector3 GetSafeAssassinSpawnPosition(int mapSize = 50)
        {
            float northZ = Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            return new Vector3(0f, 1.0f, northZ);
        }
    }
}
