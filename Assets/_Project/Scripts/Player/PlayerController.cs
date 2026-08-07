using UnityEngine;

namespace HunterVsHider.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;

        private Rigidbody rb;
        private Vector3 movement;
        private UnityEngine.Camera mainCamera;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            
            // Ensure proper rigidbody constraints for a 3D character
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            
            mainCamera = UnityEngine.Camera.main;
        }

        private void Update()
        {
            // Input logic
            movement.x = Input.GetAxisRaw("Horizontal");
            movement.z = Input.GetAxisRaw("Vertical"); // 3D mapping
            movement.y = 0f;
        }

        private void FixedUpdate()
        {
            // Physics movement
            rb.MovePosition(rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime);
            
            // Mouse aiming rotation
            RotateTowardsMouse();
        }

        private void RotateTowardsMouse()
        {
            if (mainCamera == null) return;

            // Perform a raycast from the camera to the ground plane to find where the player is aiming
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            
            if (groundPlane.Raycast(ray, out float rayDistance))
            {
                Vector3 point = ray.GetPoint(rayDistance);
                Vector3 lookDirection = point - transform.position;
                lookDirection.y = 0f; // Keep rotation strictly on the Y axis

                if (lookDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    rb.MoveRotation(targetRotation);
                }
            }
        }
    }
}
