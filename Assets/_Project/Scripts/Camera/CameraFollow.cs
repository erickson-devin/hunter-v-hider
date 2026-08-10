using UnityEngine;

namespace HunterVsHider.Cameras
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform target;
        
        [Header("Camera Offsets")]
        public Vector3 offset = new Vector3(0f, 18f, -10.4f);
        public float pitch = 60f;

        private void LateUpdate()
        {
            if (target == null) return;

            // Follow target with the new offset
            transform.position = target.position + offset;
            
            // Maintain the 60 degree pitch strictly
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
