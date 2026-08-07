using UnityEngine;

namespace HunterVsHider.Cameras
{
    public class TacticalCamera : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform target;
        public float smoothSpeed = 10f;

        [Header("Camera Offsets")]
        public float height = 18f;
        public float pitch = 60f;

        private UnityEngine.Camera cam;

        private void Awake()
        {
            cam = GetComponent<UnityEngine.Camera>();
            if (cam != null)
            {
                cam.orthographic = false; // Ensure Perspective Mode
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Calculate target position with fixed Y offset
            Vector3 targetPosition = target.position + Vector3.up * height;

            // Smooth follow
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

            // Set fixed rotation (pitch on X axis)
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
