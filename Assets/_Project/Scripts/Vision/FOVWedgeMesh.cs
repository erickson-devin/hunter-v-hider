using UnityEngine;

namespace HunterVsHider.Vision
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FOVWedgeMesh : MonoBehaviour
    {
        [Header("FOV Wedge Settings")]
        [Range(1f, 50f)]
        public float viewRadius = 18f;

        [Range(10f, 180f)]
        public float viewAngle = 80f;

        [Range(6, 64)]
        public int segments = 32;

        public Material wedgeMaterial;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh wedgeMesh;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            GenerateMesh();
        }

        private void Start()
        {
            EnsureLayerAndMaterial();
        }

        private void OnValidate()
        {
            GenerateMesh();
            EnsureLayerAndMaterial();
        }

        public void EnsureLayerAndMaterial()
        {
            // Ensure this object and its renderer are on the VisionMask layer (11)
            int visionLayer = LayerMask.NameToLayer("VisionMask");
            if (visionLayer != -1)
            {
                gameObject.layer = visionLayer;
            }

            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && wedgeMaterial != null)
            {
                meshRenderer.sharedMaterial = wedgeMaterial;
            }
        }

        public void GenerateMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

            if (wedgeMesh == null)
            {
                wedgeMesh = new Mesh { name = "FOV_Wedge_Mesh" };
            }
            else
            {
                wedgeMesh.Clear();
            }

            int vertexCount = segments + 2;
            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[segments * 3];
            Vector2[] uvs = new Vector2[vertexCount];

            // Center origin at player
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            float halfAngle = viewAngle * 0.5f;
            float angleStep = viewAngle / segments;

            for (int i = 0; i <= segments; i++)
            {
                float currentAngle = -halfAngle + (angleStep * i);
                float rad = currentAngle * Mathf.Deg2Rad;

                // Forward is +Z, right is +X
                Vector3 point = new Vector3(Mathf.Sin(rad) * viewRadius, 0f, Mathf.Cos(rad) * viewRadius);
                vertices[i + 1] = point;
                uvs[i + 1] = new Vector2(0.5f + Mathf.Sin(rad) * 0.5f, 0.5f + Mathf.Cos(rad) * 0.5f);

                if (i < segments)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }

            wedgeMesh.vertices = vertices;
            wedgeMesh.triangles = triangles;
            wedgeMesh.uv = uvs;
            wedgeMesh.RecalculateNormals();
            wedgeMesh.RecalculateBounds();

            meshFilter.sharedMesh = wedgeMesh;
        }
    }
}
