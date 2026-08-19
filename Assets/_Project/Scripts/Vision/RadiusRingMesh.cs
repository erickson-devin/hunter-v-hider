using UnityEngine;

namespace HunterVsHider.Vision
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RadiusRingMesh : MonoBehaviour
    {
        [Header("Radius Ring Settings")]
        [Range(1f, 30f)]
        public float radius = 7f;

        [Range(12, 64)]
        public int segments = 48;

        public Material ringMaterial;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh ringMesh;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            GenerateMesh();
        }

        private void Start()
        {
            EnsureMaterial();
        }

        private void OnValidate()
        {
            GenerateMesh();
            EnsureMaterial();
        }

        public void EnsureMaterial()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && ringMaterial != null)
            {
                meshRenderer.sharedMaterial = ringMaterial;
            }
        }

        public void GenerateMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

            if (ringMesh == null)
            {
                ringMesh = new Mesh { name = "Assassin_Radius_Mesh" };
            }
            else
            {
                ringMesh.Clear();
            }

            int vertexCount = segments + 2;
            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[segments * 3];
            Vector2[] uvs = new Vector2[vertexCount];

            // Center vertex
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            float angleStep = 360f / segments;

            for (int i = 0; i <= segments; i++)
            {
                float angle = angleStep * i;
                float rad = angle * Mathf.Deg2Rad;

                Vector3 point = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
                vertices[i + 1] = point;
                uvs[i + 1] = new Vector2(0.5f + Mathf.Sin(rad) * 0.5f, 0.5f + Mathf.Cos(rad) * 0.5f);

                if (i < segments)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }

            ringMesh.vertices = vertices;
            ringMesh.triangles = triangles;
            ringMesh.uv = uvs;
            ringMesh.RecalculateNormals();
            ringMesh.RecalculateBounds();

            meshFilter.sharedMesh = ringMesh;
        }
    }
}
