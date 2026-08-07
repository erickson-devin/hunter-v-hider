using UnityEngine;

namespace HunterVsHider.Cameras
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform target;
        
        [Header("Camera Offsets")]
        public float height = 18f;
        public float pitch = 60f;

        private void LateUpdate()
        {
            if (target == null) return;

            // Strict follow without smoothing ensures 100% jitter-free tracking with Interpolated Rigidbody
            Vector3 targetPosition = target.position;
            targetPosition.y += height;

            transform.position = targetPosition;
            
            // Maintain the 60 degree pitch
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
