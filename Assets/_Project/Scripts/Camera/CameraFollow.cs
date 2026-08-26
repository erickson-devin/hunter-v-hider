using UnityEngine;
using Unity.Netcode;
using HunterVsHider.Managers;
using HunterVsHider.Map;
using HunterVsHider.Player;

namespace HunterVsHider.Cameras
{
    /// <summary>
    /// Controls the local player's camera view.
    /// Supports standard 60-degree tactical follow and role-driven dynamic map-scaled 90-degree Assassin God-View with smooth scroll zoom.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform target;
        public float smoothSpeed = 10f;
        public bool useSmooth = true;
        
        [Header("Tactical Camera Offsets")]
        public Vector3 offset = new Vector3(0f, 18f, -10.4f);
        public float pitch = 60f;
        public float transitionSpeed = 8f;

        [Header("Mouse-Lead Look-Ahead Camera Offset")]
        [Tooltip("Max distance in meters the camera shifts toward the mouse cursor when aiming away from player.")]
        public float maxLookAheadOffset = 5.0f;
        [Tooltip("Smooth time in seconds for mouse-lead look-ahead transitions.")]
        public float lookAheadSmoothTime = 0.15f;

        private Vector3 currentLookAheadOffset = Vector3.zero;
        private Vector3 lookAheadVelocity = Vector3.zero;

        [Header("God-View Camera Mode (Assassin Prep)")]
        [SerializeField] private bool isSkyViewActive = false;
        [SerializeField] private Vector3 targetSkyPosition;
        [SerializeField] private Quaternion targetSkyRotation;
        public float godViewPanSpeed = 30f;
        [SerializeField] private float currentGodViewHeight = 45f;

        public bool IsSkyViewActive => isSkyViewActive;
        public bool isGodViewActive => isSkyViewActive;
        public bool IsGodViewActive => isSkyViewActive;

        public static CameraFollow Instance { get; private set; }

        private UnityEngine.Camera cam;
        private Vector3 godViewPanOffset = Vector3.zero;
        private int currentMapSize = 50;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            cam = GetComponent<UnityEngine.Camera>();
            if (cam == null) cam = UnityEngine.Camera.main;
        }

        public bool LocalPlayerIsAssassin()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                var localObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (localObj != null)
                {
                    var playerState = localObj.GetComponent<PlayerNetworkState>();
                    if (playerState != null)
                    {
                        return playerState.Role == PlayerRole.Assassin;
                    }
                }
            }
            return false;
        }

        public void SetTarget(Transform newTarget)
        {
            var matchMgr = MatchManager.Instance ?? MatchManager.Singleton;
            bool isPrepPhase = matchMgr != null && matchMgr.CurrentState == MatchState.PrepPhase;
            if ((isPrepPhase && LocalPlayerIsAssassin()) || isSkyViewActive)
            {
                target = null;
                return; // HARD GUARD: Never allow SetTarget to attach while God-View is active!
            }
            target = newTarget;
        }

        public void SetGodView(bool active, int mapSize = 50)
        {
            if (active)
            {
                isSkyViewActive = true;
                target = null; // Explicitly detach character tracking

                int activeMapSize = (MatchManager.Singleton != null && MatchManager.Singleton.SelectedMapSize > 0)
                    ? MatchManager.Singleton.SelectedMapSize
                    : mapSize;
                activeMapSize = MapGenerator.ClampMapSize(activeMapSize);
                currentMapSize = activeMapSize;

                float baseHeight = activeMapSize * 0.9f;
                currentGodViewHeight = baseHeight;

                Vector3 skyPos = new Vector3(0f, baseHeight, 0f);
                Quaternion skyRot = Quaternion.Euler(90f, 0f, 0f);

                transform.position = skyPos;
                transform.rotation = skyRot;

                targetSkyPosition = skyPos;
                targetSkyRotation = skyRot;
                godViewPanOffset = Vector3.zero;
                currentLookAheadOffset = Vector3.zero;
                lookAheadVelocity = Vector3.zero;

                Debug.Log($"[CameraFollow] Activated Assassin God-View -> MapSize: {activeMapSize}x{activeMapSize}, Height: {baseHeight:F1}m, Character tracking detached.");
            }
            else
            {
                isSkyViewActive = false;
                godViewPanOffset = Vector3.zero;
                currentLookAheadOffset = Vector3.zero;
                lookAheadVelocity = Vector3.zero;

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
                {
                    target = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
                }
                Debug.Log($"[CameraFollow] Reset camera to standard tactical 60-degree view (Target: {(target != null ? target.name : "null")}).");
            }
        }

        private void Awake()
        {
            EnsureCameraSettings();
            FindTargetIfNull();
        }

        private void Start()
        {
            FindTargetIfNull();
        }

        public void EnsureCameraSettings()
        {
            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.orthographic = false;
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 100f;
                cam.depthTextureMode |= DepthTextureMode.Depth;
            }
        }

        public void FindTargetIfNull()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        private void LateUpdate()
        {
            var matchMgr = MatchManager.Instance ?? MatchManager.Singleton;
            bool isPrepPhase = matchMgr != null && matchMgr.CurrentState == MatchState.PrepPhase;
            bool isLocalAssassin = LocalPlayerIsAssassin();

            if ((isPrepPhase && isLocalAssassin) || isSkyViewActive)
            {
                // Force target to null
                target = null;
                isSkyViewActive = true;

                // Calculate dynamic height based on map size
                int activeMapSize = matchMgr != null && matchMgr.SelectedMapSize > 0
                    ? matchMgr.SelectedMapSize
                    : currentMapSize;
                activeMapSize = MapGenerator.ClampMapSize(activeMapSize);
                currentMapSize = activeMapSize;

                if (currentGodViewHeight < 10f)
                {
                    currentGodViewHeight = activeMapSize * 0.9f;
                }

                // Handle smooth mouse scroll wheel zoom
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float minH = activeMapSize * 0.3f;
                    float maxH = activeMapSize * 1.4f;
                    currentGodViewHeight = Mathf.Clamp(currentGodViewHeight - (scroll * 15f), minH, maxH);
                }

                // Apply overhead God-View transform looking straight down
                transform.position = new Vector3(0f, currentGodViewHeight, 0f);
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                return; // HARD GUARD: Return immediately so NO character tracking logic can execute!
            }

            if (cam == null) cam = GetComponent<UnityEngine.Camera>();
            if (cam == null) cam = UnityEngine.Camera.main;

            Vector3 targetLookAhead = Vector3.zero;

            // Check Match State:
            // - During CombatPhase: look-ahead offset is gated behind holding Right Mouse Button (ADS).
            // - During Lobby and PrepPhase: look-ahead offset is always active based on cursor position.
            bool isCombatPhase = matchMgr != null && matchMgr.CurrentState == MatchState.CombatPhase;
            bool shouldApplyLookAhead = !isCombatPhase || Input.GetMouseButton(1);

            if (shouldApplyLookAhead && cam != null)
            {
                // Convert screen mouse position to normalized viewport space [0, 1]
                Vector3 viewportMouse = cam.ScreenToViewportPoint(Input.mousePosition);

                // Calculate centered cursor offset [-1, 1]
                Vector2 centeredCursor = new Vector2(viewportMouse.x - 0.5f, viewportMouse.y - 0.5f) * 2f;

                // Clamp to [-1, 1] magnitude
                centeredCursor = Vector2.ClampMagnitude(centeredCursor, 1.0f);

                // Calculate world-space offset on the XZ plane
                targetLookAhead = new Vector3(centeredCursor.x, 0f, centeredCursor.y) * maxLookAheadOffset;
            }

            // Smoothly interpolate look-ahead offset
            currentLookAheadOffset = Vector3.SmoothDamp(currentLookAheadOffset, targetLookAhead, ref lookAheadVelocity, lookAheadSmoothTime);

            if (target == null) return;

            // Combine offset with player position and tactical offset
            Vector3 targetPos = target.position + offset + currentLookAheadOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos, transitionSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(pitch, 0f, 0f), transitionSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Activates the top-down 90-degree God-View framing the arena grid for the Assassin.
        /// Uses MatchManager.Singleton.selectedMapSize.Value dynamically.
        /// </summary>
        public void ActivateAssassinSkyView()
        {
            int mapSize = (MatchManager.Singleton != null)
                ? MatchManager.Singleton.selectedMapSize.Value
                : 50;
            SetGodView(true, mapSize);
        }

        public void ActivateAssassinGodView(int mapSize) => SetGodView(true, mapSize);
        public void ActivateAssassinSkyView(int mapSize) => SetGodView(true, mapSize);

        /// <summary>
        /// Resets the camera back to the standard 60-degree tactical follow view.
        /// </summary>
        /// <param name="newTarget">Optional physical player Transform to re-attach to.</param>
        public void ResetToTacticalView(Transform newTarget = null)
        {
            if (newTarget != null)
            {
                target = newTarget;
            }

            if (isSkyViewActive)
            {
                isSkyViewActive = false;
                godViewPanOffset = Vector3.zero;
                currentLookAheadOffset = Vector3.zero;
                lookAheadVelocity = Vector3.zero;
                Debug.Log($"[CameraFollow] Reset camera to standard tactical 60-degree view (Target: {(target != null ? target.name : "null")}).");
            }
        }
    }
}
