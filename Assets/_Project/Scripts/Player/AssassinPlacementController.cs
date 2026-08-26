using UnityEngine;
using Unity.Netcode;
using HunterVsHider.Managers;
using HunterVsHider.Map;

namespace HunterVsHider.Player
{
    /// <summary>
    /// Interactive Drag-and-Drop Placement Controller for the Assassin during Prep Phase God-View.
    /// Restricts placement strictly to the top 30% sector of the arena bounds (Z >= +10m for 50x50)
    /// and validates against wall obstacle colliders with real-time green/red visual hue feedback.
    /// </summary>
    public class AssassinPlacementController : MonoBehaviour
    {
        public static AssassinPlacementController Instance { get; private set; }

        [Header("Placement Configuration")]
        [Tooltip("LayerMask containing wall geometry and obstacles.")]
        public LayerMask obstacleMask;

        [Tooltip("Radius around placement point checked for obstacle collisions.")]
        public float collisionCheckRadius = 0.6f;

        [Header("Visual Feedback Colors")]
        public Color validPlacementColor = new Color(0f, 1f, 0f, 0.45f);
        public Color invalidPlacementColor = new Color(1f, 0f, 0f, 0.45f);

        [Header("Placement State")]
        [SerializeField] private Vector3 confirmedSpawnPosition = Vector3.zero;
        [SerializeField] private Vector3 currentCandidatePosition = Vector3.zero;
        [SerializeField] private bool isCurrentlyDragging = false;
        [SerializeField] private bool isCandidateValid = false;

        public Vector3 ConfirmedSpawnPosition => confirmedSpawnPosition;
        public bool HasConfirmedSpawn => confirmedSpawnPosition != Vector3.zero;

        // Visual Indicator Hierarchy
        private GameObject indicatorRoot;
        private MeshRenderer indicatorRenderer;
        private Material indicatorMaterial;
        private Camera activeCamera;

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

            int obsLayer = LayerMask.NameToLayer("Obstacle");
            if (obsLayer != -1)
            {
                obstacleMask = 1 << obsLayer;
            }
            else
            {
                obstacleMask = LayerMask.GetMask("Obstacle", "Default");
            }
        }

        private void Start()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged += HandleMatchStateChanged;
            }
            InitializePlacementIndicator();
            RefreshPlacementState();
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
            }

            if (indicatorRoot != null)
            {
                Destroy(indicatorRoot);
            }
            if (indicatorMaterial != null)
            {
                Destroy(indicatorMaterial);
            }
        }

        private void Update()
        {
            if (!IsAssassinInPrepPhase())
            {
                if (indicatorRoot != null && indicatorRoot.activeSelf)
                {
                    indicatorRoot.SetActive(false);
                }
                return;
            }

            if (indicatorRoot != null && !indicatorRoot.activeSelf)
            {
                indicatorRoot.SetActive(true);
            }

            HandleMouseInteraction();
        }

        private void HandleMatchStateChanged(MatchState previousState, MatchState newState)
        {
            if (newState == MatchState.PrepPhase)
            {
                RefreshPlacementState();
            }
            else
            {
                if (indicatorRoot != null)
                {
                    indicatorRoot.SetActive(false);
                }
                isCurrentlyDragging = false;
            }
        }

        public void RefreshPlacementState()
        {
            int mapSize = (MatchManager.Instance != null) ? MatchManager.Instance.SelectedMapSize : 50;
            Vector3 defaultSpawn = MapGenerator.GetSafeAssassinSpawnPosition(mapSize);

            if (confirmedSpawnPosition == Vector3.zero)
            {
                confirmedSpawnPosition = defaultSpawn;
            }

            currentCandidatePosition = confirmedSpawnPosition;
            isCandidateValid = ValidateCandidatePosition(currentCandidatePosition, mapSize);

            UpdateIndicatorTransformAndColor(currentCandidatePosition, isCandidateValid);
        }

        private bool IsAssassinInPrepPhase()
        {
            if (MatchManager.Instance == null || MatchManager.Instance.CurrentState != MatchState.PrepPhase)
            {
                return false;
            }

            PlayerNetworkState localPlayer = GetLocalPlayerState();
            return (localPlayer != null && localPlayer.Role == PlayerRole.Assassin);
        }

        private void HandleMouseInteraction()
        {
            if (activeCamera == null || !activeCamera.gameObject.activeInHierarchy)
            {
                activeCamera = Camera.main;
                if (activeCamera == null) activeCamera = Object.FindAnyObjectByType<Camera>();
            }

            if (activeCamera == null) return;

            int mapSize = (MatchManager.Instance != null) ? MatchManager.Instance.SelectedMapSize : 50;

            // Start drag on Left Mouse Button Down
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
                Plane floorPlane = new Plane(Vector3.up, Vector3.zero);

                if (floorPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    // If clicked anywhere in top sector or near indicator, initiate drag
                    isCurrentlyDragging = true;
                    currentCandidatePosition = new Vector3(hitPoint.x, 0.05f, hitPoint.z);
                    isCandidateValid = ValidateCandidatePosition(currentCandidatePosition, mapSize);
                    UpdateIndicatorTransformAndColor(currentCandidatePosition, isCandidateValid);
                }
            }
            // Continue drag while Left Mouse Button is held
            else if (Input.GetMouseButton(0) && isCurrentlyDragging)
            {
                Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
                Plane floorPlane = new Plane(Vector3.up, Vector3.zero);

                if (floorPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    currentCandidatePosition = new Vector3(hitPoint.x, 0.05f, hitPoint.z);
                    isCandidateValid = ValidateCandidatePosition(currentCandidatePosition, mapSize);
                    UpdateIndicatorTransformAndColor(currentCandidatePosition, isCandidateValid);
                }
            }
            // Release drag on Left Mouse Button Up
            else if (Input.GetMouseButtonUp(0) && isCurrentlyDragging)
            {
                isCurrentlyDragging = false;

                if (isCandidateValid)
                {
                    confirmedSpawnPosition = currentCandidatePosition;
                    Debug.Log($"[AssassinPlacementController] Assassin confirmed spawn coordinates: {confirmedSpawnPosition}");

                    if (MatchManager.Instance != null)
                    {
                        MatchManager.Instance.ConfirmAssassinSpawnServerRpc(confirmedSpawnPosition);
                    }
                }
                else
                {
                    // Revert to last confirmed valid position
                    currentCandidatePosition = confirmedSpawnPosition;
                    isCandidateValid = true;
                    Debug.LogWarning("[AssassinPlacementController] Invalid placement position! Reverted to last confirmed spot.");
                }

                UpdateIndicatorTransformAndColor(confirmedSpawnPosition, true);
            }
        }

        /// <summary>
        /// Validates candidate position against top 30% arena sector constraint and wall obstacle colliders.
        /// </summary>
        public bool ValidateCandidatePosition(Vector3 pos, int mapSize)
        {
            float halfSize = mapSize * 0.5f;

            // 1. Placement Region Constraint: Top 30% of the arena bounds (Z >= -halfSize + 0.70 * mapSize)
            // For 50x50 map: -25 + 35 = +10m
            float minAllowedZ = -halfSize + (mapSize * 0.70f);
            float maxAllowedZ = halfSize - 1.5f;
            float maxAllowedX = halfSize - 1.5f;

            if (pos.z < minAllowedZ || pos.z > maxAllowedZ)
            {
                return false;
            }

            if (Mathf.Abs(pos.x) > maxAllowedX)
            {
                return false;
            }

            // 2. Obstacle Collision Check: Clear of any Obstacle layer wall colliders
            Vector3 sphereCenter = new Vector3(pos.x, 1.0f, pos.z);
            bool isBlocked = Physics.CheckSphere(sphereCenter, collisionCheckRadius, obstacleMask);
            if (isBlocked)
            {
                return false;
            }

            return true;
        }

        private void InitializePlacementIndicator()
        {
            if (indicatorRoot == null)
            {
                indicatorRoot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                indicatorRoot.name = "Assassin_Placement_Indicator";
                indicatorRoot.transform.localScale = new Vector3(2.0f, 0.02f, 2.0f);

                // Disable collider on indicator so it doesn't self-obstruct raycasts or checks
                var col = indicatorRoot.GetComponent<Collider>();
                if (col != null) Destroy(col);

                indicatorRenderer = indicatorRoot.GetComponent<MeshRenderer>();
                if (indicatorRenderer != null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                    indicatorMaterial = new Material(shader);
                    indicatorMaterial.color = validPlacementColor;

                    // Set standard transparency flags if using standard shader
                    if (indicatorMaterial.HasProperty("_Mode"))
                    {
                        indicatorMaterial.SetFloat("_Mode", 3f);
                        indicatorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        indicatorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        indicatorMaterial.SetInt("_ZWrite", 0);
                        indicatorMaterial.DisableKeyword("_ALPHATEST_ON");
                        indicatorMaterial.EnableKeyword("_ALPHABLEND_ON");
                        indicatorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        indicatorMaterial.renderQueue = 3000;
                    }

                    indicatorRenderer.material = indicatorMaterial;
                    indicatorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    indicatorRenderer.receiveShadows = false;
                }

                // Child ring or center core
                GameObject centerCore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                centerCore.name = "CenterCore";
                centerCore.transform.SetParent(indicatorRoot.transform, false);
                centerCore.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                centerCore.transform.localScale = new Vector3(0.4f, 0.05f, 0.4f);
                var coreCol = centerCore.GetComponent<Collider>();
                if (coreCol != null) Destroy(coreCol);

                var coreRend = centerCore.GetComponent<MeshRenderer>();
                if (coreRend != null && indicatorMaterial != null)
                {
                    coreRend.material = indicatorMaterial;
                    coreRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                indicatorRoot.SetActive(false);
            }
        }

        private void UpdateIndicatorTransformAndColor(Vector3 pos, bool valid)
        {
            if (indicatorRoot != null)
            {
                indicatorRoot.transform.position = new Vector3(pos.x, 0.05f, pos.z);
            }

            Color targetColor = valid ? validPlacementColor : invalidPlacementColor;
            if (indicatorMaterial != null)
            {
                indicatorMaterial.color = targetColor;
            }
        }

        private PlayerNetworkState GetLocalPlayerState()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return null;
            var localObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localObj == null) return null;
            return localObj.GetComponent<PlayerNetworkState>();
        }

        private void OnGUI()
        {
            if (!IsAssassinInPrepPhase()) return;

            int mapSize = (MatchManager.Instance != null) ? MatchManager.Instance.SelectedMapSize : 50;
            float minZ = - (mapSize * 0.5f) + (mapSize * 0.70f);

            int panelW = 380;
            int panelH = 110;
            int x = 20;
            int y = Screen.height - panelH - 30;

            GUI.backgroundColor = new Color(0.12f, 0.06f, 0.08f, 0.95f);
            GUILayout.BeginArea(new Rect(x, y, panelW, panelH), GUI.skin.box);

            GUILayout.Label("<size=14><b>ASSASSIN SPAWN INSERTION</b></size>");
            GUILayout.Label("<color=#FF8888><size=11>Click & drag the circular floor marker to select your starting spawn spot.</size></color>");
            GUILayout.Space(4);

            string statusText = isCandidateValid
                ? $"<color=#00FF66><b>[VALID SPOT]</b> ({currentCandidatePosition.x:F1}m, {currentCandidatePosition.z:F1}m)</color>"
                : $"<color=#FF3333><b>[INVALID SPOT]</b> (Must be in Top 30% [Z ≥ +{minZ:F0}m] and clear of walls)</color>";

            GUILayout.Label($"<size=11>Status: {statusText}</size>");

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
