using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using HunterVsHider.Player;
using HunterVsHider.Vision;
using HunterVsHider.Managers;
using HunterVsHider.Map;

namespace HunterVsHider.UI
{
    /// <summary>
    /// Dual-Role Tactical Mini-Map UI component.
    /// Anchored to top-right screen-space (200px x 200px).
    /// Features:
    /// - Match State Gating: 100% hidden during Lobby & PrepPhase; reveals when CombatPhase starts.
    /// - Fog of War Masking (MiniMap_FoWOverlay.shader):
    ///     * Police: Multiplies map layout geometry by live fovMaskRT red channel. Unexplored rooms start pitch black and unveil as explored memories.
    ///     * Assassin: Full-visibility blueprint overlay rendered from round start.
    /// - Local Material Isolation: Unique runtime Material instance instantiated per client to prevent memory overwrites across joining ParrelSync clients.
    /// - Local Player Tracking: Crisp white directional arrow tracking character heading.
    /// - Strict Teammate Isolation: Does NOT render remote Police teammates on mini-map.
    /// - Enemy Line-of-Sight Visibility Filtering: Red enemy blips only render if TargetVisibility.IsVisible == true.
    /// </summary>
    public class MiniMapUI : MonoBehaviour
    {
        public static MiniMapUI Instance { get; private set; }

        [Header("UI Root Container & Gating")]
        [Tooltip("Root 200px x 200px RectTransform container of the Mini-Map.")]
        public RectTransform miniMapPanel;

        [Tooltip("CanvasGroup controlling match-state fading and visibility gating.")]
        public CanvasGroup miniMapCanvasGroup;

        [Header("Map Display Components")]
        [Tooltip("RawImage displaying the active Fog of War texture or blueprint layout.")]
        public RawImage mapTextureDisplay;

        [Tooltip("Local player marker (White arrow rotating with player heading).")]
        public RectTransform playerIconLocal;

        [Tooltip("Parent container for spotted enemy markers (Red blips).")]
        public Transform enemyIconsContainer;

        [Header("Material & Overlay Settings")]
        [Tooltip("Optional template material running UI/MiniMap_FoWOverlay shader.")]
        public Material miniMapMaskMaterial;

        [Header("Tactical Marker Prefabs / Templates")]
        [Tooltip("Optional custom marker prefab for spotted enemies. If null, a procedural red blip is generated.")]
        public GameObject enemyMarkerPrefab;

        [Header("Tactical Color Palette")]
        public Color localPlayerColor = Color.white;                         // Clean White Directional Arrow
        public Color spottedEnemyColor = new Color(0.95f, 0.25f, 0.25f, 1f);  // Warning Red (#EF4444)

        [Header("Calibration Bounds")]
        [Tooltip("Default map dimension in meters (50m for 50x50 arena).")]
        public float defaultArenaSize = 50f;

        [Tooltip("World dimensions covered by the Fog of War volume (300m x 300m).")]
        public Vector2 fowWorldDimensions = new Vector2(300f, 300f);

        [Tooltip("World center of the Fog of War volume (0, 0).")]
        public Vector3 fowWorldCenter = Vector3.zero;

        // Runtime Cached State
        private PlayerNetworkState cachedLocalPlayer;
        private PlayerRole currentRole = PlayerRole.Unassigned;
        private Texture2D bakedMapLayoutTexture;
        private Material uniqueMiniMapMaterial;
        private bool isMiniMapInitialized = false;

        // Enemy Marker Tracking
        private readonly Dictionary<ulong, MarkerInstance> activeEnemyMarkers = new Dictionary<ulong, MarkerInstance>();

        private struct MarkerInstance
        {
            public PlayerNetworkState playerState;
            public RectTransform rectTransform;
            public Image iconImage;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            EnsureUIHierarchy();
            UpdateMatchStateVisibility();
        }

        private void Start()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged += HandleMatchStateChanged;
                MatchManager.Instance.OnMapSeedReceived += HandleMapSeedReceived;
            }

            EnsureUIHierarchy();
            UpdateMatchStateVisibility();
            BakeMapLayoutTextureIfAvailable();
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
                MatchManager.Instance.OnMapSeedReceived -= HandleMapSeedReceived;
            }

            if (subscribedFoWManager != null)
            {
                subscribedFoWManager.OnFoWTextureReady -= HandleFoWTextureReady;
                subscribedFoWManager = null;
            }

            if (bakedMapLayoutTexture != null)
            {
                Destroy(bakedMapLayoutTexture);
                bakedMapLayoutTexture = null;
            }

            if (uniqueMiniMapMaterial != null)
            {
                Destroy(uniqueMiniMapMaterial);
                uniqueMiniMapMaterial = null;
            }
        }

        private void HandleMatchStateChanged(MatchState previousState, MatchState newState)
        {
            UpdateMatchStateVisibility();

            if (newState == MatchState.CombatPhase)
            {
                PlayerNetworkState local = GetLocalPlayer();
                if (local != null)
                {
                    if (local.Role == PlayerRole.Police)
                    {
                        var localFoW = GetLocalFoWManager();
                        if (localFoW != null)
                        {
                            localFoW.ResetFog();
                            localFoW.ClearExploredMemoryGrid();
                        }
                    }
                    InitializeMiniMap(local.Role);
                }
                SetMiniMapVisibility(true);
            }
            else if (newState == MatchState.WaitingForPlayers || newState == MatchState.RoleAssignment || newState == MatchState.PrepPhase)
            {
                SetMiniMapVisibility(false);
            }
        }

        private void HandleMapSeedReceived(int newSeed)
        {
            BakeMapLayoutTextureIfAvailable();
        }

        private FogOfWarManager subscribedFoWManager;

        public void HandleFoWTextureReady(RenderTexture readyTexture)
        {
            if (readyTexture == null) return;

            float activeMapSize = GetActiveArenaDimension();
            Vector4 fovSubRect = CalculateFoVTextureSubRect(activeMapSize);

            if (currentRole == PlayerRole.Police && mapTextureDisplay != null)
            {
                if (uniqueMiniMapMaterial == null)
                {
                    Shader shader = Shader.Find("UI/MiniMap_FoWOverlay") ?? Shader.Find("HunterVsHider/UI/MiniMap_FoWOverlay");
                    if (shader != null)
                    {
                        uniqueMiniMapMaterial = new Material(shader);
                    }
                    else if (miniMapMaskMaterial != null)
                    {
                        uniqueMiniMapMaterial = new Material(miniMapMaskMaterial);
                    }
                }

                if (uniqueMiniMapMaterial != null)
                {
                    if (bakedMapLayoutTexture != null)
                    {
                        uniqueMiniMapMaterial.SetTexture("_MainTex", bakedMapLayoutTexture);
                    }
                    uniqueMiniMapMaterial.SetTexture("_FoWMaskTex", readyTexture);
                    uniqueMiniMapMaterial.SetVector("_FoVUVRect", fovSubRect);
                    uniqueMiniMapMaterial.SetFloat("_IsAssassin", 0.0f);

                    mapTextureDisplay.material = uniqueMiniMapMaterial;
                    mapTextureDisplay.texture = bakedMapLayoutTexture != null ? (Texture)bakedMapLayoutTexture : readyTexture;
                    mapTextureDisplay.uvRect = new Rect(0, 0, 1, 1);
                    mapTextureDisplay.color = Color.white;
                }
                else
                {
                    mapTextureDisplay.material = null;
                    mapTextureDisplay.texture = readyTexture;
                    mapTextureDisplay.uvRect = new Rect(fovSubRect.x, fovSubRect.y, fovSubRect.z, fovSubRect.w);
                    mapTextureDisplay.color = Color.white;
                }
            }
        }

        public void BindFoWMaterial(RenderTexture fowRT) => HandleFoWTextureReady(fowRT);

        /// <summary>
        /// Retrieves the local client's Fog of War render texture instance.
        /// Resolves from the local player object, local Vision camera, or singleton instance.
        /// </summary>
        public RenderTexture GetLocalFoWMaskRT()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var localObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                var fow = localObj.GetComponentInChildren<FogOfWarManager>(true);
                if (fow != null && fow.fovMaskRT != null)
                {
                    return fow.fovMaskRT;
                }

                var memFow = localObj.GetComponentInChildren<FogMemoryManager>(true);
                if (memFow != null && memFow.fovMaskRT != null)
                {
                    return memFow.fovMaskRT;
                }
            }

            if (FogOfWarManager.Instance != null && FogOfWarManager.Instance.fovMaskRT != null)
            {
                return FogOfWarManager.Instance.fovMaskRT;
            }

            if (FogMemoryManager.Instance != null && FogMemoryManager.Instance.fovMaskRT != null)
            {
                return FogMemoryManager.Instance.fovMaskRT;
            }

            var sceneFow = Object.FindAnyObjectByType<FogOfWarManager>();
            if (sceneFow != null && sceneFow.fovMaskRT != null)
            {
                return sceneFow.fovMaskRT;
            }

            var sceneMem = Object.FindAnyObjectByType<FogMemoryManager>();
            if (sceneMem != null && sceneMem.fovMaskRT != null)
            {
                return sceneMem.fovMaskRT;
            }

            return null;
        }

        public FogOfWarManager GetLocalFoWManager()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var localObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                var fow = localObj.GetComponentInChildren<FogOfWarManager>(true);
                if (fow != null) return fow;
            }

            if (FogOfWarManager.Instance != null) return FogOfWarManager.Instance;
            return Object.FindAnyObjectByType<FogOfWarManager>();
        }

        /// <summary>
        /// Match State Gating:
        /// During Lobby and PrepPhase: Set miniMapCanvasGroup.alpha = 0f and disable interaction (100% HIDDEN).
        /// During CombatPhase: Set miniMapCanvasGroup.alpha = 1f (reveals mini-map when active combat starts).
        /// </summary>
        public void UpdateMatchStateVisibility()
        {
            var matchMgr = MatchManager.Instance ?? MatchManager.Singleton;
            bool isCombatPhase = matchMgr != null && matchMgr.CurrentState == MatchState.CombatPhase;

            SetMiniMapVisibility(isCombatPhase);
        }

        public void SetMiniMapVisibility(bool visible)
        {
            if (miniMapCanvasGroup != null)
            {
                miniMapCanvasGroup.alpha = visible ? 1f : 0f;
                miniMapCanvasGroup.blocksRaycasts = visible;
                miniMapCanvasGroup.interactable = visible;
            }

            if (miniMapPanel != null && miniMapPanel.gameObject.activeSelf != visible)
            {
                miniMapPanel.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Asymmetric Texture Setup (InitializeMiniMap(PlayerRole role)):
        /// - Police Role: Uses MiniMap_FoWOverlay.shader material multiplying map layout geometry by live fovMaskRT red channel.
        ///   Starts pitch black and permanently unveils visited hallways/rooms as dark gray memories.
        /// - Assassin Role: Displays the Map Blueprint directly without fog masking.
        /// </summary>
        public void InitializeMiniMap(PlayerRole role)
        {
            currentRole = role;
            EnsureUIHierarchy();
            BakeMapLayoutTextureIfAvailable();

            float activeMapSize = GetActiveArenaDimension();
            Vector4 fovSubRect = CalculateFoVTextureSubRect(activeMapSize);

            // 1. Resolve local player entity strictly via local client ownership
            PlayerController localPlayer = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>();
            }

            // 2. Bind to local client owner's FogOfWarManager instance
            FogOfWarManager localFoW = null;
            if (localPlayer != null)
            {
                localFoW = localPlayer.GetComponent<FogOfWarManager>();
            }
            if (localFoW == null)
            {
                localFoW = GetLocalFoWManager();
            }

            if (localFoW != null)
            {
                if (subscribedFoWManager != localFoW)
                {
                    if (subscribedFoWManager != null)
                    {
                        subscribedFoWManager.OnFoWTextureReady -= HandleFoWTextureReady;
                    }
                    subscribedFoWManager = localFoW;
                    subscribedFoWManager.OnFoWTextureReady += HandleFoWTextureReady;
                }

                if (subscribedFoWManager.fovMaskRT != null)
                {
                    HandleFoWTextureReady(subscribedFoWManager.fovMaskRT);
                }
            }

            RenderTexture fowRT = null;
            if (localFoW != null && localFoW.fovMaskRT != null)
            {
                fowRT = localFoW.fovMaskRT;
            }
            if (fowRT == null)
            {
                fowRT = GetLocalFoWMaskRT();
            }

            if (role == PlayerRole.Police)
            {
                if (localFoW != null)
                {
                    localFoW.ResetFog();
                    localFoW.ClearExploredMemoryGrid();
                }

                // 3. Enforce local runtime material instantiation per client (never assign shared project materials directly)
                if (uniqueMiniMapMaterial == null)
                {
                    Shader shader = Shader.Find("UI/MiniMap_FoWOverlay") ?? Shader.Find("HunterVsHider/UI/MiniMap_FoWOverlay");
                    if (shader != null)
                    {
                        uniqueMiniMapMaterial = new Material(shader);
                    }
                    else if (miniMapMaskMaterial != null)
                    {
                        uniqueMiniMapMaterial = new Material(miniMapMaskMaterial);
                    }
                }

                if (mapTextureDisplay != null)
                {
                    if (uniqueMiniMapMaterial != null)
                    {
                        if (bakedMapLayoutTexture != null)
                        {
                            uniqueMiniMapMaterial.SetTexture("_MainTex", bakedMapLayoutTexture);
                        }
                        if (fowRT != null)
                        {
                            uniqueMiniMapMaterial.SetTexture("_FoWMaskTex", fowRT);
                        }
                        uniqueMiniMapMaterial.SetVector("_FoVUVRect", fovSubRect);
                        uniqueMiniMapMaterial.SetFloat("_IsAssassin", 0.0f);

                        mapTextureDisplay.material = uniqueMiniMapMaterial;
                        mapTextureDisplay.texture = bakedMapLayoutTexture != null ? (Texture)bakedMapLayoutTexture : fowRT;
                        mapTextureDisplay.uvRect = new Rect(0, 0, 1, 1);
                        mapTextureDisplay.color = Color.white;
                    }
                    else if (fowRT != null)
                    {
                        mapTextureDisplay.material = null;
                        mapTextureDisplay.texture = fowRT;
                        mapTextureDisplay.uvRect = new Rect(fovSubRect.x, fovSubRect.y, fovSubRect.z, fovSubRect.w);
                        mapTextureDisplay.color = Color.white;
                    }
                }
            }
            else
            {
                // Assassin: Direct Map Blueprint overlay without fog suppression (strictly isolated from uniqueMiniMapMaterial)
                if (mapTextureDisplay != null)
                {
                    mapTextureDisplay.material = null;
                    mapTextureDisplay.texture = bakedMapLayoutTexture;
                    mapTextureDisplay.uvRect = new Rect(0, 0, 1, 1);
                    mapTextureDisplay.color = Color.white;
                }
            }

            if (playerIconLocal != null)
            {
                Image iconImg = playerIconLocal.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.color = localPlayerColor;
                    iconImg.sprite = CreateTacticalArrowSprite();
                }
            }

            isMiniMapInitialized = true;
            Debug.Log($"[MiniMapUI] Initialized mini-map layout for {role} (MapSize: {activeMapSize}x{activeMapSize}, FoW SubRect: {fovSubRect})");
        }

        private void LateUpdate()
        {
            // 1. Locate local player instance (strictly local client authority)
            PlayerNetworkState localPlayer = GetLocalPlayer();
            if (localPlayer == null) return;

            // Initialize on first valid local player detection or role change
            if (!isMiniMapInitialized || (currentRole != localPlayer.Role && localPlayer.Role != PlayerRole.Unassigned))
            {
                InitializeMiniMap(localPlayer.Role);
            }

            float activeMapSize = GetActiveArenaDimension();
            Vector3 arenaCenter = GetArenaCenter();

            // Refresh Police FoW live texture and UV bounds dynamically
            if (currentRole == PlayerRole.Police && mapTextureDisplay != null)
            {
                PlayerController localPC = null;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
                {
                    localPC = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>();
                }

                FogOfWarManager localFoW = localPC != null ? localPC.GetComponent<FogOfWarManager>() : null;
                RenderTexture fowRT = (localFoW != null && localFoW.fovMaskRT != null) ? localFoW.fovMaskRT : GetLocalFoWMaskRT();

                if (fowRT != null)
                {
                    Vector4 fovSubRect = CalculateFoVTextureSubRect(activeMapSize);
                    if (uniqueMiniMapMaterial != null)
                    {
                        uniqueMiniMapMaterial.SetTexture("_FoWMaskTex", fowRT);
                        if (bakedMapLayoutTexture != null && uniqueMiniMapMaterial.GetTexture("_MainTex") != bakedMapLayoutTexture)
                        {
                            uniqueMiniMapMaterial.SetTexture("_MainTex", bakedMapLayoutTexture);
                        }
                        uniqueMiniMapMaterial.SetVector("_FoVUVRect", fovSubRect);
                        uniqueMiniMapMaterial.SetFloat("_IsAssassin", 0.0f);

                        if (mapTextureDisplay.material != uniqueMiniMapMaterial)
                        {
                            mapTextureDisplay.material = uniqueMiniMapMaterial;
                        }
                    }
                    else
                    {
                        if (mapTextureDisplay.texture != fowRT)
                        {
                            mapTextureDisplay.texture = fowRT;
                        }
                        mapTextureDisplay.uvRect = new Rect(fovSubRect.x, fovSubRect.y, fovSubRect.z, fovSubRect.w);
                    }
                }
            }

            // 2. Local Player Tracking & Orientation (White Directional Arrow)
            if (playerIconLocal != null)
            {
                Vector2 localNormPos = WorldToNormalizedMapCoordinates(localPlayer.transform.position, arenaCenter, activeMapSize);
                Vector2 localAnchoredPos = NormalizedToAnchoredPosition(localNormPos);
                playerIconLocal.anchoredPosition = localAnchoredPos;

                // Rotate arrow precisely with character facing direction (strictly UP at EulerY 0)
                playerIconLocal.localRotation = Quaternion.Euler(0f, 0f, -localPlayer.transform.eulerAngles.y);
            }

            // 3. Remote Enemy Tracking with Strict Line-of-Sight Filtering (Teammates strictly isolated)
            UpdateEnemyMarkers(localPlayer, arenaCenter, activeMapSize);
        }

        /// <summary>
        /// Transforms world position coordinates (x, z) of local and visible players into normalized UI coordinates ([0, 1]) relative to the arena bounds.
        /// </summary>
        public Vector2 WorldToNormalizedMapCoordinates(Vector3 worldPos, Vector3 center, float mapSize)
        {
            float halfSize = mapSize * 0.5f;
            float minX = center.x - halfSize;
            float minZ = center.z - halfSize;

            float u = Mathf.Clamp01((worldPos.x - minX) / mapSize);
            float v = Mathf.Clamp01((worldPos.z - minZ) / mapSize);

            return new Vector2(u, v);
        }

        /// <summary>
        /// Converts normalized [0, 1] mini-map coordinates to pixel anchoredPosition in the 200x200 panel.
        /// </summary>
        public Vector2 NormalizedToAnchoredPosition(Vector2 normCoords)
        {
            if (miniMapPanel == null) return Vector2.zero;

            Vector2 panelSize = miniMapPanel.rect.size;
            if (panelSize.x <= 0f || panelSize.y <= 0f)
            {
                panelSize = new Vector2(200f, 200f);
            }

            // Center-anchored coordinates (pivot 0.5, 0.5)
            float posX = (normCoords.x - 0.5f) * panelSize.x;
            float posY = (normCoords.y - 0.5f) * panelSize.y;

            return new Vector2(posX, posY);
        }

        /// <summary>
        /// Updates enemy markers in enemyIconsContainer.
        /// Strict Isolation: Remote Police teammates are NEVER rendered on the mini-map.
        /// Enemy Visibility Filter: Only render enemy blips if TargetVisibility.IsVisible == true.
        /// </summary>
        private void UpdateEnemyMarkers(PlayerNetworkState localPlayer, Vector3 center, float mapSize)
        {
            if (enemyIconsContainer == null) return;

            PlayerNetworkState[] allPlayers = Object.FindObjectsByType<PlayerNetworkState>(FindObjectsInactive.Include);
            HashSet<ulong> activeEnemyClientIds = new HashSet<ulong>();

            for (int i = 0; i < allPlayers.Length; i++)
            {
                PlayerNetworkState other = allPlayers[i];
                if (other == null || other == localPlayer || !other.IsSpawned) continue;

                // Strict Isolation: Skip friendly teammates entirely (no blips for teammates)
                if (other.Role == localPlayer.Role || other.Role == PlayerRole.Unassigned || localPlayer.Role == PlayerRole.Unassigned)
                {
                    continue;
                }

                ulong clientId = other.OwnerClientId;
                activeEnemyClientIds.Add(clientId);

                // Retrieve or create enemy marker instance
                if (!activeEnemyMarkers.TryGetValue(clientId, out MarkerInstance marker))
                {
                    marker = CreateEnemyMarkerInstance(other);
                    activeEnemyMarkers[clientId] = marker;
                }

                // Update Position and Rotation
                Vector2 normPos = WorldToNormalizedMapCoordinates(other.transform.position, center, mapSize);
                marker.rectTransform.anchoredPosition = NormalizedToAnchoredPosition(normPos);
                marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -other.transform.eulerAngles.y);

                // Line-of-Sight Visibility Filter
                TargetVisibility targetVis = other.GetComponent<TargetVisibility>();
                bool isVisible = targetVis != null && targetVis.IsVisible;

                if (marker.rectTransform.gameObject.activeSelf != isVisible)
                {
                    marker.rectTransform.gameObject.SetActive(isVisible);
                }
            }

            // Cleanup despawned enemy markers
            List<ulong> toRemove = null;
            foreach (var kvp in activeEnemyMarkers)
            {
                if (!activeEnemyClientIds.Contains(kvp.Key))
                {
                    if (toRemove == null) toRemove = new List<ulong>();
                    toRemove.Add(kvp.Key);
                    if (kvp.Value.rectTransform != null)
                    {
                        Destroy(kvp.Value.rectTransform.gameObject);
                    }
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    activeEnemyMarkers.Remove(toRemove[i]);
                }
            }
        }

        private MarkerInstance CreateEnemyMarkerInstance(PlayerNetworkState targetPlayer)
        {
            GameObject markerObj = null;

            if (enemyMarkerPrefab != null)
            {
                markerObj = Instantiate(enemyMarkerPrefab, enemyIconsContainer);
            }
            else
            {
                markerObj = new GameObject($"EnemyMarker_{targetPlayer.OwnerClientId}", typeof(RectTransform), typeof(Image));
                markerObj.transform.SetParent(enemyIconsContainer, false);
                RectTransform rt = markerObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(14f, 14f);
                Image img = markerObj.GetComponent<Image>();
                img.sprite = CreateTacticalArrowSprite();
                img.color = spottedEnemyColor;
            }

            return new MarkerInstance
            {
                playerState = targetPlayer,
                rectTransform = markerObj.GetComponent<RectTransform>(),
                iconImage = markerObj.GetComponent<Image>()
            };
        }

        /// <summary>
        /// Validates and constructs all required UI container elements dynamically if unassigned.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            if (miniMapPanel == null)
            {
                miniMapPanel = GetComponent<RectTransform>();
            }

            if (miniMapCanvasGroup == null)
            {
                if (miniMapPanel != null)
                {
                    miniMapCanvasGroup = miniMapPanel.GetComponent<CanvasGroup>();
                    if (miniMapCanvasGroup == null)
                    {
                        miniMapCanvasGroup = miniMapPanel.gameObject.AddComponent<CanvasGroup>();
                    }
                }
                else
                {
                    miniMapCanvasGroup = GetComponent<CanvasGroup>();
                    if (miniMapCanvasGroup == null)
                    {
                        miniMapCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                    }
                }
            }

            if (mapTextureDisplay == null && miniMapPanel != null)
            {
                Transform displayTr = miniMapPanel.Find("MapMask/MapTextureDisplay");
                if (displayTr == null) displayTr = miniMapPanel.Find("MapTextureDisplay");

                if (displayTr != null)
                {
                    mapTextureDisplay = displayTr.GetComponent<RawImage>();
                }
                else
                {
                    GameObject rawObj = new GameObject("MapTextureDisplay", typeof(RectTransform), typeof(RawImage));
                    rawObj.transform.SetParent(miniMapPanel, false);
                    RectTransform rt = rawObj.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    mapTextureDisplay = rawObj.GetComponent<RawImage>();
                }
            }

            if (enemyIconsContainer == null && miniMapPanel != null)
            {
                Transform enemyTr = miniMapPanel.Find("MapMask/EnemyIconsContainer");
                if (enemyTr == null) enemyTr = miniMapPanel.Find("EnemyIconsContainer");

                if (enemyTr != null)
                {
                    enemyIconsContainer = enemyTr;
                }
                else
                {
                    GameObject enemyObj = new GameObject("EnemyIconsContainer", typeof(RectTransform));
                    enemyObj.transform.SetParent(miniMapPanel, false);
                    RectTransform rt = enemyObj.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    enemyIconsContainer = enemyObj.transform;
                }
            }

            if (playerIconLocal == null && miniMapPanel != null)
            {
                Transform iconTr = miniMapPanel.Find("MapMask/PlayerIconLocal");
                if (iconTr == null) iconTr = miniMapPanel.Find("PlayerIconLocal");

                if (iconTr != null)
                {
                    playerIconLocal = iconTr.GetComponent<RectTransform>();
                }
                else
                {
                    GameObject iconObj = new GameObject("PlayerIconLocal", typeof(RectTransform), typeof(Image));
                    iconObj.transform.SetParent(miniMapPanel, false);
                    playerIconLocal = iconObj.GetComponent<RectTransform>();
                    playerIconLocal.sizeDelta = new Vector2(16f, 16f);
                }
            }

            if (playerIconLocal != null)
            {
                Image img = playerIconLocal.GetComponent<Image>();
                if (img == null) img = playerIconLocal.gameObject.AddComponent<Image>();
                img.color = Color.white;
                img.sprite = CreateTacticalArrowSprite();
            }

            if (miniMapMaskMaterial == null)
            {
                Shader shader = Shader.Find("HunterVsHider/UI/MiniMap_FoWOverlay") ?? Shader.Find("UI/MiniMap_FoWOverlay");
                if (shader != null)
                {
                    miniMapMaskMaterial = new Material(shader);
                }
            }
        }

        /// <summary>
        /// Bakes the active procedural room, corridor, and wall geometry from MapGenerator into a crisp 512x512 tactical texture.
        /// </summary>
        public void BakeMapLayoutTextureIfAvailable()
        {
            float activeMapSize = GetActiveArenaDimension();
            int texRes = 512;

            if (bakedMapLayoutTexture == null || bakedMapLayoutTexture.width != texRes)
            {
                if (bakedMapLayoutTexture != null) Destroy(bakedMapLayoutTexture);
                bakedMapLayoutTexture = new Texture2D(texRes, texRes, TextureFormat.RGBA32, false)
                {
                    name = "Baked_MapLayout_Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            Color corridorColor = new Color(0.11f, 0.15f, 0.20f, 1f); // #1C2633 Corridor Base
            Color wallColor = new Color(0.35f, 0.45f, 0.58f, 1f);     // #597394 Structural Wall
            Color perimeterColor = new Color(0.48f, 0.62f, 0.78f, 1f);// #7A9EC7 Perimeter Wall

            Color[] pixels = new Color[texRes * texRes];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = corridorColor;
            }

            float halfSize = activeMapSize * 0.5f;

            // Draw outer perimeter border
            int borderPx = Mathf.Max(3, Mathf.RoundToInt(0.5f / activeMapSize * texRes));
            for (int y = 0; y < texRes; y++)
            {
                for (int x = 0; x < texRes; x++)
                {
                    if (x < borderPx || x >= texRes - borderPx || y < borderPx || y >= texRes - borderPx)
                    {
                        pixels[y * texRes + x] = perimeterColor;
                    }
                }
            }

            // Draw procedural rooms and wall segments if MapGenerator exists
            if (MapGenerator.Instance != null && MapGenerator.Instance.generatedEnvironment != null)
            {
                Transform env = MapGenerator.Instance.generatedEnvironment;
                int childCount = env.childCount;

                for (int i = 0; i < childCount; i++)
                {
                    Transform wallChild = env.GetChild(i);
                    Vector3 pos = wallChild.position;
                    Vector3 scale = wallChild.localScale;

                    // Convert world bounds to pixel rect
                    float minX = pos.x - (scale.x * 0.5f);
                    float maxX = pos.x + (scale.x * 0.5f);
                    float minZ = pos.z - (scale.z * 0.5f);
                    float maxZ = pos.z + (scale.z * 0.5f);

                    int pxMinX = Mathf.Clamp(Mathf.RoundToInt((minX + halfSize) / activeMapSize * texRes), 0, texRes - 1);
                    int pxMaxX = Mathf.Clamp(Mathf.RoundToInt((maxX + halfSize) / activeMapSize * texRes), 0, texRes - 1);
                    int pxMinZ = Mathf.Clamp(Mathf.RoundToInt((minZ + halfSize) / activeMapSize * texRes), 0, texRes - 1);
                    int pxMaxZ = Mathf.Clamp(Mathf.RoundToInt((maxZ + halfSize) / activeMapSize * texRes), 0, texRes - 1);

                    for (int py = pxMinZ; py <= pxMaxZ; py++)
                    {
                        for (int px = pxMinX; px <= pxMaxX; px++)
                        {
                            pixels[py * texRes + px] = wallColor;
                        }
                    }
                }
            }

            bakedMapLayoutTexture.SetPixels(pixels);
            bakedMapLayoutTexture.Apply();

            if (uniqueMiniMapMaterial != null)
            {
                uniqueMiniMapMaterial.SetTexture("_MainTex", bakedMapLayoutTexture);
            }
            else if (currentRole == PlayerRole.Assassin && mapTextureDisplay != null)
            {
                mapTextureDisplay.texture = bakedMapLayoutTexture;
            }
        }

        private Vector4 CalculateFoVTextureSubRect(float mapSize)
        {
            Vector3 center = GetArenaCenter();
            float fowWidth = (DynamicFog.Instance != null) ? DynamicFog.Instance.worldSize.x : fowWorldDimensions.x;
            float fowHeight = (DynamicFog.Instance != null) ? DynamicFog.Instance.worldSize.y : fowWorldDimensions.y;
            Vector3 fowCenter = (DynamicFog.Instance != null) ? DynamicFog.Instance.worldCenter : fowWorldCenter;

            float minFowX = fowCenter.x - (fowWidth * 0.5f);
            float minFowZ = fowCenter.z - (fowHeight * 0.5f);

            float arenaMinX = center.x - (mapSize * 0.5f);
            float arenaMinZ = center.z - (mapSize * 0.5f);

            float uvMinX = (arenaMinX - minFowX) / fowWidth;
            float uvMinY = (arenaMinZ - minFowZ) / fowHeight;
            float uvSizeX = mapSize / fowWidth;
            float uvSizeY = mapSize / fowHeight;

            return new Vector4(uvMinX, uvMinY, uvSizeX, uvSizeY);
        }

        private float GetActiveArenaDimension()
        {
            if (MatchManager.Instance != null && MatchManager.Instance.SelectedMapSize > 0)
            {
                return MatchManager.Instance.SelectedMapSize;
            }
            return defaultArenaSize;
        }

        private Vector3 GetArenaCenter()
        {
            if (MatchManager.Instance != null && MatchManager.Instance.zoneCombatArena != null)
            {
                return MatchManager.Instance.zoneCombatArena.position;
            }
            return Vector3.zero;
        }

        private PlayerNetworkState GetLocalPlayer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var localNetState = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerNetworkState>();
                if (localNetState != null)
                {
                    cachedLocalPlayer = localNetState;
                    return cachedLocalPlayer;
                }
            }

            if (cachedLocalPlayer != null && cachedLocalPlayer.IsSpawned && cachedLocalPlayer.IsLocalPlayer)
            {
                return cachedLocalPlayer;
            }

            PlayerNetworkState[] players = Object.FindObjectsByType<PlayerNetworkState>(FindObjectsInactive.Include);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsLocalPlayer)
                {
                    cachedLocalPlayer = players[i];
                    return cachedLocalPlayer;
                }
            }

            return null;
        }

        /// <summary>
        /// Generates a sharp, clean white tactical directional chevron arrow pointing strictly UP (+Y towards 0 degrees).
        /// </summary>
        private Sprite CreateTacticalArrowSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear
            };

            Color transparent = new Color(0, 0, 0, 0);
            Color fill = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, transparent);
                }
            }

            // Sharp tactical arrow pointing UP (+Y towards 0 degrees)
            Vector2 tip = new Vector2(15.5f, 28f);
            Vector2 leftBase = new Vector2(5.5f, 4f);
            Vector2 rightBase = new Vector2(25.5f, 4f);
            Vector2 innerNotch = new Vector2(15.5f, 10f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    if (PointInTriangle(p, tip, leftBase, innerNotch) || PointInTriangle(p, tip, innerNotch, rightBase))
                    {
                        tex.SetPixel(x, y, fill);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static bool PointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
        {
            float d1 = Sign(pt, v1, v2);
            float d2 = Sign(pt, v2, v3);
            float d3 = Sign(pt, v3, v1);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }
}
