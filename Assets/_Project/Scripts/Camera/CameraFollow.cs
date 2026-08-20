using UnityEngine;

namespace HunterVsHider.Cameras
{
    /// <summary>
    /// Controls the local player's camera view.
    /// Supports standard 60-degree tactical follow and full-map 90-degree Assassin sky view.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform target;
        
        [Header("Tactical Camera Offsets")]
        public Vector3 offset = new Vector3(0f, 18f, -10.4f);
        public float pitch = 60f;
        public float transitionSpeed = 8f;

        [Header("Sky Camera Mode (Read-Only)")]
        [SerializeField] private bool isSkyViewActive = false;
        [SerializeField] private Vector3 targetSkyPosition;
        [SerializeField] private Quaternion targetSkyRotation;

        public bool IsSkyViewActive => isSkyViewActive;

        private UnityEngine.Camera cam;

        private void Awake()
        {
            cam = GetComponent<UnityEngine.Camera>();
            if (cam == null) cam = UnityEngine.Camera.main;
        }

        private void LateUpdate()
        {
            if (isSkyViewActive)
            {
                // Smoothly lerp position and rotation into Assassin full-grid Sky View
                transform.position = Vector3.Lerp(transform.position, targetSkyPosition, transitionSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetSkyRotation, transitionSpeed * Time.deltaTime);
            }
            else
            {
                if (target == null) return;

                Vector3 targetPos = target.position + offset;
                transform.position = Vector3.Lerp(transform.position, targetPos, transitionSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(pitch, 0f, 0f), transitionSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Activates the top-down 90-degree Sky View framing the entire arena grid for the Assassin.
        /// Uses MatchManager.Singleton.selectedMapSize.Value dynamically.
        /// </summary>
        public void ActivateAssassinSkyView()
        {
            int mapSize = (HunterVsHider.Managers.MatchManager.Singleton != null)
                ? HunterVsHider.Managers.MatchManager.Singleton.selectedMapSize.Value
                : 50;
            ActivateAssassinSkyView(mapSize);
        }

        /// <summary>
        /// Activates the top-down 90-degree Sky View framing the entire arena grid for the Assassin.
        /// </summary>
        /// <param name="mapSize">Grid dimension (e.g. 50, 100, 250)</param>
        public void ActivateAssassinSkyView(int mapSize)
        {
            if (cam == null) cam = GetComponent<UnityEngine.Camera>();
            if (cam == null) cam = UnityEngine.Camera.main;

            float fov = (cam != null) ? cam.fieldOfView : 60f;
            float aspect = (cam != null && cam.aspect > 0.01f) ? cam.aspect : (16f / 9f);

            // Compute dynamic height to fit both width and height within camera frustum
            float halfMap = mapSize * 0.5f;
            float tanHalfFov = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            float verticalDist = halfMap / tanHalfFov;
            float horizontalDist = halfMap / (aspect * tanHalfFov);

            // Include a 10% safety margin around the grid edges
            float requiredHeight = Mathf.Max(verticalDist, horizontalDist) * 1.10f;

            if (cam != null && cam.orthographic)
            {
                cam.orthographicSize = halfMap * 1.10f;
            }

            // Snap camera directly to center of Zone_CombatArena (X=0, Z=0) looking straight down (X=90, Y=0, Z=0)
            Vector3 skyPos = new Vector3(0f, requiredHeight, 0f);
            Quaternion skyRot = Quaternion.Euler(90f, 0f, 0f);

            transform.position = skyPos;
            transform.rotation = skyRot;

            targetSkyPosition = skyPos;
            targetSkyRotation = skyRot;
            isSkyViewActive = true;

            Debug.Log($"[CameraFollow] Activated Assassin Sky View -> MapSize: {mapSize}x{mapSize}, Snapped to: {skyPos}, Rotation: (90, 0, 0)");
        }

        /// <summary>
        /// Resets the camera back to the standard 60-degree tactical follow view.
        /// </summary>
        public void ResetToTacticalView()
        {
            if (isSkyViewActive)
            {
                isSkyViewActive = false;
                Debug.Log("[CameraFollow] Reset camera to standard tactical 60-degree view.");
            }
        }
    }
}
