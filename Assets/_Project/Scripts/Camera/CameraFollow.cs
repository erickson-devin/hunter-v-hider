using UnityEngine;

namespace HunterVsHider.Cameras
{
    /// <summary>
    /// Controls the local player's camera view.
    /// Supports standard 60-degree tactical follow and full-map 90-degree Assassin God-View with WASD panning.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform target;
        
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

        public bool IsSkyViewActive => isSkyViewActive;
        public bool isGodViewActive => isSkyViewActive;
        public bool IsGodViewActive => isSkyViewActive;

        public static CameraFollow Instance { get; private set; }

        private UnityEngine.Camera cam;
        private Vector3 godViewPanOffset = Vector3.zero;
        private int currentMapSize = 50;
        private float currentRequiredHeight = 35f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            cam = GetComponent<UnityEngine.Camera>();
            if (cam == null) cam = UnityEngine.Camera.main;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetGodView(bool active, int mapSize = 50)
        {
            if (active)
            {
                SetTarget(null);
                ActivateAssassinGodView(mapSize);
            }
            else
            {
                ResetToTacticalView();
            }
        }

        private void Update()
        {
            if (isSkyViewActive)
            {
                // Handle WASD / Arrow Keys camera panning across active map bounds
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");

                Vector3 panDir = new Vector3(h, 0f, v);
                if (panDir.sqrMagnitude > 0.01f)
                {
                    godViewPanOffset += panDir.normalized * godViewPanSpeed * Time.deltaTime;
                }

                // Clamp panning within map boundaries (with a small edge buffer)
                float maxPan = Mathf.Max(10f, currentMapSize * 0.5f);
                godViewPanOffset.x = Mathf.Clamp(godViewPanOffset.x, -maxPan, maxPan);
                godViewPanOffset.z = Mathf.Clamp(godViewPanOffset.z, -maxPan, maxPan);

                targetSkyPosition = new Vector3(godViewPanOffset.x, currentRequiredHeight, godViewPanOffset.z);
            }
        }

        private void LateUpdate()
        {
            if (cam == null) cam = GetComponent<UnityEngine.Camera>();
            if (cam == null) cam = UnityEngine.Camera.main;

            Vector3 targetLookAhead = Vector3.zero;

            // Check Match State:
            // - During CombatPhase: look-ahead offset is gated behind holding Right Mouse Button (ADS).
            // - During Lobby and PrepPhase: look-ahead offset is always active based on cursor position.
            var matchMgr = HunterVsHider.Managers.MatchManager.Instance ?? HunterVsHider.Managers.MatchManager.Singleton;
            bool isCombatPhase = matchMgr != null && matchMgr.CurrentState == HunterVsHider.Managers.MatchState.CombatPhase;
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

            if (isSkyViewActive)
            {
                // Smoothly lerp position and rotation into Assassin God-View with unified mouse-lead support
                Vector3 skyTargetPos = targetSkyPosition + currentLookAheadOffset;
                transform.position = Vector3.Lerp(transform.position, skyTargetPos, transitionSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetSkyRotation, transitionSpeed * Time.deltaTime);
            }
            else
            {
                if (target == null) return;

                // Combine offset with player position and tactical offset
                Vector3 targetPos = target.position + offset + currentLookAheadOffset;
                transform.position = Vector3.Lerp(transform.position, targetPos, transitionSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(pitch, 0f, 0f), transitionSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Activates the top-down 90-degree God-View framing the arena grid for the Assassin.
        /// Uses MatchManager.Singleton.selectedMapSize.Value dynamically.
        /// </summary>
        public void ActivateAssassinSkyView()
        {
            int mapSize = (HunterVsHider.Managers.MatchManager.Singleton != null)
                ? HunterVsHider.Managers.MatchManager.Singleton.selectedMapSize.Value
                : 50;
            ActivateAssassinGodView(mapSize);
        }

        /// <summary>
        /// Activates the top-down 90-degree God-View framing the arena grid with WASD panning support.
        /// </summary>
        /// <param name="mapSize">Grid dimension (50, 100, 150)</param>
        public void ActivateAssassinGodView(int mapSize)
        {
            currentMapSize = mapSize;
            if (cam == null) cam = GetComponent<UnityEngine.Camera>();
            if (cam == null) cam = UnityEngine.Camera.main;

            float fov = (cam != null) ? cam.fieldOfView : 60f;
            float aspect = (cam != null && cam.aspect > 0.01f) ? cam.aspect : (16f / 9f);

            // Compute dynamic height to fit arena or provide optimal overhead perspective
            float halfMap = mapSize * 0.5f;
            float tanHalfFov = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            float verticalDist = halfMap / tanHalfFov;
            float horizontalDist = halfMap / (aspect * tanHalfFov);

            // Set camera height scaled to map size (minimum 25m)
            currentRequiredHeight = Mathf.Max(25f, Mathf.Max(verticalDist, horizontalDist) * 1.05f);

            if (cam != null && cam.orthographic)
            {
                cam.orthographicSize = halfMap * 1.05f;
            }

            target = null;
            godViewPanOffset = Vector3.zero;

            Vector3 skyPos = new Vector3(0f, currentRequiredHeight, 0f);
            Quaternion skyRot = Quaternion.Euler(90f, 0f, 0f);

            transform.position = skyPos;
            transform.rotation = skyRot;

            targetSkyPosition = skyPos;
            targetSkyRotation = skyRot;
            isSkyViewActive = true;

            Debug.Log($"[CameraFollow] Activated Assassin God-View -> MapSize: {mapSize}x{mapSize}, Height: {currentRequiredHeight:F1}m, WASD Pan Active.");
        }

        public void ActivateAssassinSkyView(int mapSize) => ActivateAssassinGodView(mapSize);

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
