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
        private Quaternion targetRotation;

        public Transform WeaponHolder { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            targetRotation = transform.rotation;
            SetupWeaponHolder();
            SetupVisualIndicator();
        }

        private void SetupVisualIndicator()
        {
            Transform existingIndicator = transform.Find("VisualIndicator_Forward");
            if (existingIndicator == null)
            {
                GameObject indicatorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                indicatorGo.name = "VisualIndicator_Forward";
                indicatorGo.transform.SetParent(transform);
                indicatorGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                indicatorGo.transform.localScale = new Vector3(0.2f, 0.2f, 0.5f);
                indicatorGo.transform.localRotation = Quaternion.identity;
                
                Collider col = indicatorGo.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col); // Remove BoxCollider to prevent physics interference
                }
            }
        }

        private void SetupWeaponHolder()
        {
            Transform existingHolder = transform.Find("WeaponHolder");
            if (existingHolder != null)
            {
                WeaponHolder = existingHolder;
            }
            else
            {
                GameObject holderGo = new GameObject("WeaponHolder");
                holderGo.transform.SetParent(transform);
                holderGo.transform.localPosition = Vector3.zero;
                holderGo.transform.localRotation = Quaternion.identity;
                WeaponHolder = holderGo.transform;
            }
        }

        private void Update()
        {
            // Strict lifecycle separation: Read input in Update
            movementInput.x = Input.GetAxisRaw("Horizontal");
            movementInput.z = Input.GetAxisRaw("Vertical");
            movementInput.y = 0f;

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
            // Strict lifecycle separation: Apply physics in FixedUpdate
            if (movementInput.sqrMagnitude > 0.01f)
            {
                // Normalize to prevent faster diagonal movement
                Vector3 moveVelocity = movementInput.normalized * moveSpeed;
                
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
