using UnityEngine;

namespace HunterVsHider.Cameras
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform target;
        public float smoothSpeed = 10f;
        public bool useSmooth = true;
        
        [Header("Camera Offsets")]
        public Vector3 offset = new Vector3(0f, 18f, -10.4f);
        public float pitch = 60f;

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
            if (target == null)
            {
                FindTargetIfNull();
                if (target == null) return;
            }

            Vector3 targetPosition = target.position + offset;

            if (useSmooth && Application.isPlaying)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
            }
            else
            {
                transform.position = targetPosition;
            }
            
            // Maintain the 60 degree pitch strictly
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
