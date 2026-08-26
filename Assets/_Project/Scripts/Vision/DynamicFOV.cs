using Unity.Netcode;
using UnityEngine;

namespace HunterVsHider.Vision
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class DynamicFOV : NetworkBehaviour
    {
        [Header("FOV Configuration")]
        [Tooltip("Max range of the active directional view cone.")]
        [Range(1f, 50f)]
        public float viewRadius = 15f;

        [Tooltip("Angular width of the active directional view cone in degrees.")]
        [Range(10f, 360f)]
        public float viewAngle = 90f;

        [Tooltip("Immediate 360-degree close-quarters awareness radius (stops cleanly at walls).")]
        [Range(0.5f, 20f)]
        public float proximityRadius = 2.5f;

        [Tooltip("Distance the raycast penetrates past obstacle hit points to illuminate flat wall tops (0.25m for 0.5m thick walls).")]
        [SerializeField]
        [Range(0f, 5f)]
        public float wallTopOvershoot = 0.25f;

        [Tooltip("Total ray count cast around the 360-degree perimeter.")]
        [Range(60, 360)]
        public int rayCount = 240;

        [Header("Obstacle Layer")]
        public LayerMask obstacleMask = 1 << 7; // Layer 7: Obstacle

        public Material visionMaterial;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh fovMesh;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            EnsureLayerAndMaterial();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            CheckOwnerAuthority();
        }

        private void Start()
        {
            EnsureLayerAndMaterial();
            CheckOwnerAuthority();
        }

        public void CheckOwnerAuthority()
        {
            // In a networked multiplayer session, remote player clones MUST NOT render their white FOV wedge
            // into the local player's VisionMask camera, nor waste CPU performing dynamic raycasts.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (!IsOwner)
                {
                    if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
                    if (meshRenderer != null) meshRenderer.enabled = false;
                    enabled = false; // Disable DynamicFOV update loop
                    return;
                }
            }
        }

        public void EnsureLayerAndMaterial()
        {
            int visionLayer = LayerMask.NameToLayer("VisionMask");
            if (visionLayer != -1)
            {
                gameObject.layer = visionLayer;
            }

            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && visionMaterial != null)
            {
                meshRenderer.sharedMaterial = visionMaterial;
            }
        }

        [Header("Vision Mask Suppression")]
        [Tooltip("When true (Lobby/PrepPhase), FOV restrictions and dark fog are suppressed to provide 360-degree full-screen visibility.")]
        [SerializeField] private bool _isVisionMaskSuppressed = false;

        public bool isVisionMaskSuppressed
        {
            get => _isVisionMaskSuppressed;
            set => SetVisionMaskSuppression(value);
        }

        private float targetViewAngle = 90f;
        private float targetViewRadius = 15f;
        private float targetProximityRadius = 2.5f;
        private float visionTransitionSpeed = 5f;
        private bool isTransitioningVision = false;

        public void SetVisionMaskSuppression(bool suppressed, float transitionDuration = 0.3f)
        {
            if (_isVisionMaskSuppressed == suppressed && !isTransitioningVision) return;
            _isVisionMaskSuppressed = suppressed;

            // Toggle Flashlight outline / Visuals
            Transform parent = transform.parent != null ? transform.parent : transform;
            Transform fovVisuals = parent.Find("FOV_Visuals");
            if (fovVisuals != null)
            {
                fovVisuals.gameObject.SetActive(!suppressed);
            }

            if (suppressed)
            {
                // In Lobby & PrepPhase: full 360 degree unrestricted vision
                viewAngle = 360f;
                viewRadius = 60f;
                proximityRadius = 60f;
                isTransitioningVision = false;
                Debug.Log($"[DynamicFOV] Vision Mask SUPPRESSED for {gameObject.name}: 360 deg unrestricted full-screen visibility enabled.");
            }
            else
            {
                // When entering CombatPhase: smoothly contract from 360 deg full-screen down to weapon profile over 0.3s
                viewAngle = 360f;
                viewRadius = Mathf.Max(viewRadius, 30f);
                proximityRadius = 2.5f;
                UpdateWeaponVisionProfile(targetViewAngle, targetViewRadius, transitionDuration);
                Debug.Log($"[DynamicFOV] Vision Mask RESTORED for {gameObject.name}: Transitioning to {targetViewAngle} deg / {targetViewRadius}m over {transitionDuration}s.");
            }
        }

        public void UpdateWeaponVisionProfile(float targetAngle, float targetDistance, float transitionDuration = 0.3f)
        {
            targetViewAngle = targetAngle;
            targetViewRadius = targetDistance;
            targetProximityRadius = 2.5f;
            visionTransitionSpeed = (transitionDuration > 0.001f) ? (1f / transitionDuration) : 100f;
            isTransitioningVision = true;
        }

        public void SetRoleFOV(Player.PlayerRole role)
        {
            if (role == Player.PlayerRole.Police)
            {
                targetViewAngle = 90f;
                targetViewRadius = 15f;
                targetProximityRadius = 2.5f;
                rayCount = 240;
            }
            else if (role == Player.PlayerRole.Assassin)
            {
                targetViewAngle = 360f;
                targetViewRadius = 12f;
                targetProximityRadius = 12f;
                rayCount = 240;
            }

            if (_isVisionMaskSuppressed)
            {
                viewAngle = 360f;
                viewRadius = 60f;
                proximityRadius = 60f;
            }
            else
            {
                viewAngle = targetViewAngle;
                viewRadius = targetViewRadius;
                proximityRadius = targetProximityRadius;
            }
        }

        /// <summary>
        /// Resets all persistent Fog of War memory buffers to pure black (Unexplored).
        /// Re-renders the Fog of War mesh so the arena outside the starting flashlight cone returns to pitch black.
        /// </summary>
        public void ClearExploredMemoryGrid()
        {
            if (FogMemoryManager.Instance != null)
            {
                FogMemoryManager.Instance.ResetFog();
            }

            GenerateDynamicFOVMesh();
            Debug.Log($"[DynamicFOV] Cleared explored memory buffer for {gameObject.name}: Map reset to pitch black unexplored fog.");
        }

        private Vector3 lastFramePosition;
        private bool hasInitializedPosition = false;

        private void LateUpdate()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOwner)
            {
                return;
            }

            if (!hasInitializedPosition)
            {
                lastFramePosition = transform.position;
                hasInitializedPosition = true;
            }

            // Displacement Guard: Detect large frame-to-frame teleportation displacement (> 10m)
            float frameDisplacement = Vector3.Distance(transform.position, lastFramePosition);
            if (frameDisplacement > 10.0f)
            {
                // Instant RPC teleport detected -> skip memory updating and clear memory buffers to prevent dirty streak artifacts
                lastFramePosition = transform.position;
                ClearExploredMemoryGrid();
                return;
            }
            lastFramePosition = transform.position;

            if (isTransitioningVision && !_isVisionMaskSuppressed)
            {
                viewAngle = Mathf.MoveTowards(viewAngle, targetViewAngle, (Mathf.Abs(targetViewAngle - viewAngle) * visionTransitionSpeed + 20f) * Time.deltaTime);
                viewRadius = Mathf.MoveTowards(viewRadius, targetViewRadius, (Mathf.Abs(targetViewRadius - viewRadius) * visionTransitionSpeed + 5f) * Time.deltaTime);

                if (Mathf.Abs(viewAngle - targetViewAngle) < 0.2f && Mathf.Abs(viewRadius - targetViewRadius) < 0.2f)
                {
                    viewAngle = targetViewAngle;
                    viewRadius = targetViewRadius;
                    isTransitioningVision = false;
                }

                // Update attached or child Light if present
                var lightComp = GetComponentInChildren<Light>();
                if (lightComp != null && lightComp.type == LightType.Spot)
                {
                    lightComp.spotAngle = viewAngle;
                    lightComp.range = viewRadius;
                }
            }

            DynamicFog.Instance?.UpdatePlayerTracking(transform);

            GenerateDynamicFOVMesh();
        }

        public void GenerateDynamicFOVMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

            if (fovMesh == null)
            {
                fovMesh = new Mesh { name = "Dynamic_FOV_Mesh" };
                meshFilter.sharedMesh = fovMesh;
            }
            else
            {
                fovMesh.Clear();
            }

            // Flatten target position to physical floor plane (Y = 0) to eliminate 60-degree camera parallax offset
            Vector3 flattenedPos = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 rayOrigin = flattenedPos + Vector3.up * 0.5f; // Eye/torso height at 0.5m above ground
            int numRays = rayCount;
            int numVertices = numRays + 2;

            Vector3[] vertices = new Vector3[numVertices];
            int[] triangles = new int[numRays * 3];
            Vector2[] uvs = new Vector2[numVertices];

            // Center vertex
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            float halfAngle = viewAngle * 0.5f;
            float angleStep = 360f / numRays;

            for (int i = 0; i <= numRays; i++)
            {
                // Relative angle to local forward in [-180, 180]
                float currentRelAngle = (i == numRays) ? 180f : -180f + (angleStep * i);
                float maxDist = (Mathf.Abs(currentRelAngle) <= halfAngle) ? viewRadius : proximityRadius;

                Vector3 dir = DirectionFromAngle(currentRelAngle, false);

                Vector3 worldPoint;
                if (!_isVisionMaskSuppressed && Physics.Raycast(rayOrigin, dir, out RaycastHit hit, maxDist, obstacleMask))
                {
                    // Raycast overshoot: bleed into wall volume so flat wall tops are illuminated
                    worldPoint = hit.point + (dir * wallTopOvershoot);
                }
                else
                {
                    worldPoint = rayOrigin + (dir * maxDist);
                }

                // Project hit point down to character local plane
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                localPoint.y = 0f;

                vertices[i + 1] = localPoint;
                uvs[i + 1] = new Vector2(0.5f + (localPoint.x / (viewRadius * 2f)), 0.5f + (localPoint.z / (viewRadius * 2f)));

                if (i < numRays)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }

            fovMesh.vertices = vertices;
            fovMesh.triangles = triangles;
            fovMesh.uv = uvs;
            fovMesh.RecalculateNormals();
            fovMesh.RecalculateBounds();
        }

        private Vector3 DirectionFromAngle(float angleInDegrees, bool isGlobal)
        {
            if (!isGlobal)
            {
                angleInDegrees += transform.eulerAngles.y;
            }
            float rad = angleInDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }
    }
}
