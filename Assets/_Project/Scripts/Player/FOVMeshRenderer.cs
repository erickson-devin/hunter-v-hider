using UnityEngine;

namespace HunterVsHider.Player
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FOVMeshRenderer : MonoBehaviour
    {
        [Header("FOV Settings")]
        [SerializeField] private float fovAngle = 90f;        // Total cone angle (45 degrees left/right)
        [SerializeField] private float viewDistance = 15f;    // Distance of the pizza slice
        [SerializeField] private int rayCount = 50;           // Mesh resolution density
        [SerializeField] private LayerMask obstacleLayer;     // Obstacle wall collision mask

        private Mesh mesh;
        private MeshFilter meshFilter;

        private void Awake()
        {
            mesh = new Mesh();
            mesh.name = "FOV_Mesh";
            meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = mesh;
        }

        private void LateUpdate()
        {
            DrawFOV();
        }

        private void DrawFOV()
        {
            float currentAngle = -fovAngle / 2f;
            float angleIncrease = fovAngle / rayCount;

            Vector3[] vertices = new Vector3[rayCount + 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[rayCount * 3];

            // Origin vertex at player center
            vertices[0] = Vector3.zero;

            int vertexIndex = 1;
            int triangleIndex = 0;

            for (int i = 0; i <= rayCount; i++)
            {
                Vector3 vertex;
                Vector3 dir = GetVectorFromAngle(currentAngle);

                // Raycast in local space converted to world direction
                Vector3 worldDir = transform.TransformDirection(dir);

                if (Physics.Raycast(transform.position, worldDir, out RaycastHit hit, viewDistance, obstacleLayer))
                {
                    // Hit wall obstacle
                    vertex = transform.InverseTransformPoint(hit.point);
                }
                else
                {
                    // No obstacle hit
                    vertex = dir * viewDistance;
                }

                vertices[vertexIndex] = vertex;

                if (i > 0)
                {
                    triangles[triangleIndex + 0] = 0;
                    triangles[triangleIndex + 1] = vertexIndex - 1;
                    triangles[triangleIndex + 2] = vertexIndex;

                    triangleIndex += 3;
                }

                vertexIndex++;
                currentAngle += angleIncrease;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private Vector3 GetVectorFromAngle(float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }
    }
}
