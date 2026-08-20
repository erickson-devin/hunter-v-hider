using System;
using System.Collections.Generic;
using UnityEngine;
using HunterVsHider.Managers;

namespace HunterVsHider.Map
{
    /// <summary>
    /// Deterministic procedural map generator.
    /// Generates local 3D obstacle geometry across clients from a synchronized integer seed.
    /// Walls are strictly local geometry (no NetworkObject).
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        public static MapGenerator Instance { get; private set; }

        [Header("Prefab & Materials")]
        [Tooltip("Optional custom prefab for wall blocks. If null, standard 3D primitive Cube is instantiated.")]
        public GameObject wallPrefab;

        [Tooltip("Material applied to generated wall cubes.")]
        public Material wallMaterial;

        [Header("Grid Dimensions & Sizing")]
        [Tooltip("Size of each grid cell in meters (standard 2m x 2m).")]
        public float cellSize = 2f;

        [Tooltip("Height of the generated wall obstacles in meters.")]
        public float wallHeight = 3f;

        [Tooltip("Target obstacle density (0.35 to 0.45 recommended for tactical cover).")]
        [Range(0.2f, 0.6f)]
        public float wallDensity = 0.42f;

        [Tooltip("Smoothing passes for cellular automata generation.")]
        [Range(1, 6)]
        public int smoothingIterations = 3;

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

        /// <summary>
        /// Deterministically generates the 3D maze environment from the specified seed.
        /// </summary>
        /// <param name="seed">Synchronized random integer seed.</param>
        public void GenerateMap(int seed)
        {
            if (seed <= 0) return;

            ClearMap();
            EnsureContainer();

            currentGeneratedSeed = seed;

            int mapSize = (MatchManager.Singleton != null) ? MatchManager.Singleton.SelectedMapSize : 50;
            Debug.Log($"[MapGenerator] Generating deterministic map -> Seed: {seed}, Size: {mapSize}x{mapSize}");

            // Crucial: Use System.Random with seed for cross-client determinism
            System.Random pseudoRandom = new System.Random(seed);

            // Compute grid dimensions
            int gridWidth = Mathf.RoundToInt(mapSize / cellSize);
            int gridHeight = Mathf.RoundToInt(mapSize / cellSize);

            if (gridWidth % 2 == 0) gridWidth++;
            if (gridHeight % 2 == 0) gridHeight++;

            int[,] grid = GenerateGridData(gridWidth, gridHeight, pseudoRandom);

            float halfWidth = (gridWidth * cellSize) * 0.5f;
            float halfHeight = (gridHeight * cellSize) * 0.5f;

            int wallLayer = LayerMask.NameToLayer("Obstacle");
            if (wallLayer == -1) wallLayer = 0;

            int wallCount = 0;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    if (grid[x, z] == 1)
                    {
                        Vector3 worldPos = new Vector3(
                            x * cellSize - halfWidth + (cellSize * 0.5f),
                            wallHeight * 0.5f,
                            z * cellSize - halfHeight + (cellSize * 0.5f)
                        );

                        // Clear spawn areas so players never spawn inside obstacles
                        if (IsSpawnSafetyZone(worldPos, mapSize))
                        {
                            continue;
                        }

                        GameObject wallObj;
                        if (wallPrefab != null)
                        {
                            wallObj = Instantiate(wallPrefab, worldPos, Quaternion.identity, generatedEnvironment);
                            wallObj.name = $"Wall_{x}_{z}";
                            wallObj.transform.localScale = new Vector3(cellSize, wallHeight, cellSize);
                        }
                        else
                        {
                            wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            wallObj.name = $"Wall_{x}_{z}";
                            wallObj.transform.SetParent(generatedEnvironment, false);
                            wallObj.transform.position = worldPos;
                            wallObj.transform.localScale = new Vector3(cellSize, wallHeight, cellSize);

                            if (wallMaterial != null)
                            {
                                Renderer rend = wallObj.GetComponent<Renderer>();
                                if (rend != null) rend.sharedMaterial = wallMaterial;
                            }
                        }

                        // Strict standard: Spawned walls MUST NOT have NetworkObject
                        var netObj = wallObj.GetComponent<Unity.Netcode.NetworkObject>();
                        if (netObj != null)
                        {
                            DestroyImmediate(netObj);
                        }

                        wallObj.layer = wallLayer;
                        wallObj.tag = "Obstacle";

                        wallCount++;
                    }
                }
            }

            Debug.Log($"[MapGenerator] Generated {wallCount} obstacle walls for seed {seed} ({mapSize}x{mapSize}).");
        }

        private int[,] GenerateGridData(int width, int height, System.Random prng)
        {
            int[,] map = new int[width, height];

            // 1. Initial random fill with borders
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    if (x == 0 || x == width - 1 || z == 0 || z == height - 1)
                    {
                        map[x, z] = 1; // Perimeter border
                    }
                    else
                    {
                        map[x, z] = (prng.NextDouble() < wallDensity) ? 1 : 0;
                    }
                }
            }

            // 2. Cellular Automata Smoothing Passes (4-5 rule)
            for (int i = 0; i < smoothingIterations; i++)
            {
                int[,] nextMap = new int[width, height];

                for (int x = 0; x < width; x++)
                {
                    for (int z = 0; z < height; z++)
                    {
                        if (x == 0 || x == width - 1 || z == 0 || z == height - 1)
                        {
                            nextMap[x, z] = 1;
                        }
                        else
                        {
                            int neighborWallCount = GetSurroundingWallCount(map, x, z, width, height);

                            if (neighborWallCount > 4)
                                nextMap[x, z] = 1;
                            else if (neighborWallCount < 4)
                                nextMap[x, z] = 0;
                            else
                                nextMap[x, z] = map[x, z];
                        }
                    }
                }

                map = nextMap;
            }

            // 3. Carve central corridors to ensure navigability across quadrants
            int midX = width / 2;
            int midZ = height / 2;

            for (int x = 1; x < width - 1; x++)
            {
                map[x, midZ] = 0;
                if (midZ + 1 < height - 1) map[x, midZ + 1] = 0;
            }

            for (int z = 1; z < height - 1; z++)
            {
                map[midX, z] = 0;
                if (midX + 1 < width - 1) map[midX + 1, z] = 0;
            }

            return map;
        }

        private int GetSurroundingWallCount(int[,] map, int gridX, int gridY, int width, int height)
        {
            int wallCount = 0;
            for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
            {
                for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
                {
                    if (neighbourX >= 0 && neighbourX < width && neighbourY >= 0 && neighbourY < height)
                    {
                        if (neighbourX != gridX || neighbourY != gridY)
                        {
                            wallCount += map[neighbourX, neighbourY];
                        }
                    }
                    else
                    {
                        wallCount++;
                    }
                }
            }
            return wallCount;
        }

        /// <summary>
        /// Clears safety circles around spawn points (Center, North Spawn, South Spawn).
        /// </summary>
        private bool IsSpawnSafetyZone(Vector3 pos, int mapSize)
        {
            float distFromCenter = new Vector2(pos.x, pos.z).magnitude;
            if (distFromCenter < 5.0f) return true;

            // North Spawn (Assassin Combat Spawn: Z = 18m or scaled)
            float northZ = Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            float distFromNorth = new Vector2(pos.x, pos.z - northZ).magnitude;
            if (distFromNorth < 6.0f) return true;

            // South Spawn (Police Combat Spawn: Z = -18m or scaled)
            float southZ = -Mathf.Min(18f, (mapSize * 0.5f) - 6f);
            float distFromSouth = new Vector2(pos.x, pos.z - southZ).magnitude;
            if (distFromSouth < 6.0f) return true;

            return false;
        }
    }
}
