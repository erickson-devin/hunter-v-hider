using UnityEngine;
using Unity.Netcode;

namespace HunterVsHider.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement Speeds")]
        [Tooltip("Standard walking speed in m/s.")]
        public float walkSpeed = 5.0f;

        [Tooltip("High-speed sprinting speed in m/s.")]
        public float sprintSpeed = 8.5f;

        [Tooltip("Legacy fallback move speed property.")]
        public float moveSpeed = 5.0f;

        [Header("Movement State")]
        [SerializeField] private bool isSprinting = false;

        public bool IsSprinting => isSprinting;
        public float CurrentSpeed => isSprinting ? sprintSpeed : walkSpeed;

        private Rigidbody rb;
        private Vector3 movementInput;
        private Quaternion targetRotation;
        private PlayerNetworkState playerNetworkState;

        public Transform WeaponHolder { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            targetRotation = transform.rotation;
            WeaponHolder = transform.Find("WeaponHolder");
            playerNetworkState = GetComponent<PlayerNetworkState>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // For remote clones, set Rigidbody to kinematic so NetworkTransform handles interpolation cleanly
            if (!IsOwner)
            {
                rb.isKinematic = true;
            }
        }

        [Header("Movement Control")]
        [SerializeField] private bool canMove = true;

        public bool CanMove
        {
            get => canMove;
            set => canMove = value;
        }

        public void SetMovementEnabled(bool enabled)
        {
            canMove = enabled;
            if (!enabled)
            {
                movementInput = Vector3.zero;
                isSprinting = false;
                if (playerNetworkState != null && IsOwner)
                {
                    playerNetworkState.SetSprinting(false);
                }
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Strict lifecycle separation: Read input in Update
            if (canMove)
            {
                movementInput.x = Input.GetAxisRaw("Horizontal");
                movementInput.z = Input.GetAxisRaw("Vertical");
                movementInput.y = 0f;

                // Dual-input sprint detection: Left Shift, Right Shift, OR physical Caps Lock hold
                bool isSprintKeyPressed = Input.GetKey(KeyCode.LeftShift) || 
                                          Input.GetKey(KeyCode.RightShift) || 
                                          Input.GetKey(KeyCode.CapsLock);

                isSprinting = isSprintKeyPressed && (movementInput.sqrMagnitude > 0.01f);
            }
            else
            {
                movementInput = Vector3.zero;
                isSprinting = false;
            }

            // Sync sprinting state to network
            if (playerNetworkState != null)
            {
                playerNetworkState.SetSprinting(isSprinting);
            }

            HandleAimingInput();
        }

        private void HandleAimingInput()
        {
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, 1f, 0)); // Horizontal Plane at Y = 1.0m

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 aimDirection = hitPoint - rb.position;
                aimDirection.y = 0f; // Zero out Y to prevent tilting

                if (aimDirection.sqrMagnitude > 0.001f)
                {
                    targetRotation = Quaternion.LookRotation(aimDirection);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;

            // Strict lifecycle separation: Apply physics in FixedUpdate
            if (movementInput.sqrMagnitude > 0.01f)
            {
                float activeSpeed = isSprinting ? sprintSpeed : walkSpeed;

                // Normalize to prevent faster diagonal movement
                Vector3 moveVelocity = movementInput.normalized * activeSpeed;
                
                // Using MovePosition for smooth kinematic-like movement on a dynamic rigidbody
                rb.MovePosition(rb.position + moveVelocity * Time.fixedDeltaTime);
            }

            // Apply rotation in FixedUpdate to prevent jitter
            if (targetRotation != Quaternion.identity)
            {
                rb.MoveRotation(targetRotation);
            }
        }
    }
}
