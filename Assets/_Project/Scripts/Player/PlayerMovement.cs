using UnityEngine;

namespace HunterVsHider.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;

        private Rigidbody rb;
        private Vector3 movementInput;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            // Strict lifecycle separation: Read input in Update
            movementInput.x = Input.GetAxisRaw("Horizontal");
            movementInput.z = Input.GetAxisRaw("Vertical");
            movementInput.y = 0f;
        }

        private void FixedUpdate()
        {
            // Strict lifecycle separation: Apply physics in FixedUpdate
            if (movementInput.sqrMagnitude > 0.01f)
            {
                // Normalize to prevent faster diagonal movement
                Vector3 moveVelocity = movementInput.normalized * moveSpeed;
                
                // Using MovePosition for smooth kinematic-like movement on a dynamic rigidbody
                rb.MovePosition(rb.position + moveVelocity * Time.fixedDeltaTime);
                
                // Rotate to face movement direction (optional, but good for testing)
                Quaternion targetRotation = Quaternion.LookRotation(movementInput);
                rb.MoveRotation(targetRotation);
            }
        }
    }
}
