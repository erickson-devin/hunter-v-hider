using Unity.Netcode;
using UnityEngine;

namespace HunterVsHider.Vision
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class DynamicFOV : NetworkBehaviour
    {
        [Header("FOV Configuration")]
        [Tooltip("Max range of the active directional view cone.")]
        [Range(1f, 50f)]
        public float viewRadius = 15f;

        [Tooltip("Angular width of the active directional view cone in degrees.")]
        [Range(10f, 360f)]
        public float viewAngle = 90f;

        [Tooltip("Immediate 360-degree close-quarters awareness radius (stops cleanly at walls).")]
        [Range(0.5f, 20f)]
        public float proximityRadius = 2.5f;

        [Tooltip("Distance the raycast penetrates past obstacle hit points to illuminate flat wall tops (0.25m for 0.5m thick walls).")]
        [SerializeField]
        [Range(0f, 5f)]
        public float wallTopOvershoot = 0.25f;

        [Tooltip("Total ray count cast around the 360-degree perimeter.")]
        [Range(60, 360)]
        public int rayCount = 180;

        [Header("Obstacle Layer")]
        public LayerMask obstacleMask = 1 << 7; // Layer 7: Obstacle

        public Material visionMaterial;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh fovMesh;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            EnsureLayerAndMaterial();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            CheckOwnerAuthority();
        }

        private void Start()
        {
            EnsureLayerAndMaterial();
            CheckOwnerAuthority();
        }

        public void CheckOwnerAuthority()
        {
            // In a networked multiplayer session, remote player clones MUST NOT render their white FOV wedge
            // into the local player's VisionMask camera, nor waste CPU performing dynamic raycasts.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (!IsOwner)
                {
                    if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
                    if (meshRenderer != null) meshRenderer.enabled = false;
                    enabled = false; // Disable DynamicFOV update loop
                    return;
                }
            }
        }

        public void EnsureLayerAndMaterial()
        {
            int visionLayer = LayerMask.NameToLayer("VisionMask");
            if (visionLayer != -1)
            {
                gameObject.layer = visionLayer;
            }

            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && visionMaterial != null)
            {
                meshRenderer.sharedMaterial = visionMaterial;
            }
        }

        public void SetRoleFOV(Player.PlayerRole role)
        {
            if (role == Player.PlayerRole.Police)
            {
                viewAngle = 90f;
                viewRadius = 15f;
                proximityRadius = 2.5f;
                rayCount = 180;
            }
            else if (role == Player.PlayerRole.Assassin)
            {
                viewAngle = 360f;
                viewRadius = 12f;
                proximityRadius = 12f;
                rayCount = 180;
            }
        }

        private void LateUpdate()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOwner)
            {
                return;
            }

            GenerateDynamicFOVMesh();
        }

        public void GenerateDynamicFOVMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

            if (fovMesh == null)
            {
                fovMesh = new Mesh { name = "Dynamic_FOV_Mesh" };
                meshFilter.sharedMesh = fovMesh;
            }
            else
            {
                fovMesh.Clear();
            }

            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f; // Eye/torso height
            int numRays = rayCount;
            int numVertices = numRays + 2;

            Vector3[] vertices = new Vector3[numVertices];
            int[] triangles = new int[numRays * 3];
            Vector2[] uvs = new Vector2[numVertices];

            // Center vertex
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            float halfAngle = viewAngle * 0.5f;
            float angleStep = 360f / numRays;

            for (int i = 0; i <= numRays; i++)
            {
                // Relative angle to local forward in [-180, 180]
                float currentRelAngle = (i == numRays) ? 180f : -180f + (angleStep * i);
                float maxDist = (Mathf.Abs(currentRelAngle) <= halfAngle) ? viewRadius : proximityRadius;

                Vector3 dir = DirectionFromAngle(currentRelAngle, false);

                Vector3 worldPoint;
                if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, maxDist, obstacleMask))
                {
                    // Raycast overshoot: bleed into wall volume so flat wall tops are illuminated
                    worldPoint = hit.point + (dir * wallTopOvershoot);
                }
                else
                {
                    worldPoint = rayOrigin + (dir * maxDist);
                }

                // Project hit point down to character local plane
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                localPoint.y = 0f;

                vertices[i + 1] = localPoint;
                uvs[i + 1] = new Vector2(0.5f + (localPoint.x / (viewRadius * 2f)), 0.5f + (localPoint.z / (viewRadius * 2f)));

                if (i < numRays)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }

            fovMesh.vertices = vertices;
            fovMesh.triangles = triangles;
            fovMesh.uv = uvs;
            fovMesh.RecalculateNormals();
            fovMesh.RecalculateBounds();
        }

        private Vector3 DirectionFromAngle(float angleInDegrees, bool isGlobal)
        {
            if (!isGlobal)
            {
                angleInDegrees += transform.eulerAngles.y;
            }
            float rad = angleInDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }
    }
}
