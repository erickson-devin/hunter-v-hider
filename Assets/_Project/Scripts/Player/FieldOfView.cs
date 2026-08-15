using System.Collections.Generic;
using UnityEngine;
using HunterVsHider.Vision;

namespace HunterVsHider.Player
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FieldOfView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VisionController visionController;

        [Header("Mesh Settings")]
        [SerializeField] private float meshYOffset = 0.05f;

        private Mesh mesh;
        private MeshFilter meshFilter;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Vector2> uvs = new List<Vector2>();

        public Mesh CurrentMesh => mesh;

        private void Awake()
        {
            if (visionController == null)
            {
                visionController = GetComponentInParent<VisionController>() ?? GetComponent<VisionController>();
            }

            mesh = new Mesh
            {
                name = "Dynamic_FOV_Mesh"
            };
            mesh.MarkDynamic();

            meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = mesh;
        }

        private void LateUpdate()
        {
            if (visionController == null)
            {
                visionController = GetComponentInParent<VisionController>() ?? GetComponent<VisionController>();
                if (visionController == null) return;
            }

            visionController.CalculateVision();
            GenerateFOVMesh();
        }

        public void GenerateFOVMesh()
        {
            IReadOnlyList<VisionController.RayVisionResult> rays = visionController.RayResults;
            if (rays == null || rays.Count < 2) return;

            vertices.Clear();
            triangles.Clear();
            uvs.Clear();

            // Transform world ray directions and hit points to local space
            Vector3 localOrigin = new Vector3(0f, meshYOffset, 0f);

            // 1. Build Inner Fan (from origin out to first hit / low obstacle edge)
            int originIndex = vertices.Count;
            vertices.Add(localOrigin);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int rayCount = rays.Count;
            for (int i = 0; i < rayCount; i++)
            {
                Vector3 worldHit = visionController.EyeWorldPosition + rays[i].direction * rays[i].firstHitDist;
                Vector3 localHit = transform.InverseTransformPoint(worldHit);
                localHit.y = meshYOffset;

                vertices.Add(localHit);
                uvs.Add(new Vector2(localHit.x, localHit.z));
            }

            for (int i = 0; i < rayCount - 1; i++)
            {
                int v1 = originIndex;
                int v2 = originIndex + 1 + i;
                int v3 = originIndex + 1 + i + 1;

                triangles.Add(v1);
                triangles.Add(v2);
                triangles.Add(v3);
            }

            // 2. Build Outer Visible Strips beyond low obstacle shadows
            for (int i = 0; i < rayCount - 1; i++)
            {
                var rA = rays[i];
                var rB = rays[i + 1];

                // If both adjacent rays have an outer visible segment beyond the shadow
                if (rA.hasLowObstacleShadow && rB.hasLowObstacleShadow &&
                    rA.shadowEndDist < rA.secondHitDist && rB.shadowEndDist < rB.secondHitDist)
                {
                    Vector3 worldInnerA = visionController.EyeWorldPosition + rA.direction * rA.shadowEndDist;
                    Vector3 worldOuterA = visionController.EyeWorldPosition + rA.direction * rA.secondHitDist;

                    Vector3 worldInnerB = visionController.EyeWorldPosition + rB.direction * rB.shadowEndDist;
                    Vector3 worldOuterB = visionController.EyeWorldPosition + rB.direction * rB.secondHitDist;

                    Vector3 localInnerA = transform.InverseTransformPoint(worldInnerA); localInnerA.y = meshYOffset;
                    Vector3 localOuterA = transform.InverseTransformPoint(worldOuterA); localOuterA.y = meshYOffset;
                    Vector3 localInnerB = transform.InverseTransformPoint(worldInnerB); localInnerB.y = meshYOffset;
                    Vector3 localOuterB = transform.InverseTransformPoint(worldOuterB); localOuterB.y = meshYOffset;

                    int idx = vertices.Count;
                    vertices.Add(localInnerA);
                    vertices.Add(localOuterA);
                    vertices.Add(localInnerB);
                    vertices.Add(localOuterB);

                    uvs.Add(new Vector2(localInnerA.x, localInnerA.z));
                    uvs.Add(new Vector2(localOuterA.x, localOuterA.z));
                    uvs.Add(new Vector2(localInnerB.x, localInnerB.z));
                    uvs.Add(new Vector2(localOuterB.x, localOuterB.z));

                    // Quad: (innerA, outerA, outerB) and (innerA, outerB, innerB)
                    triangles.Add(idx + 0);
                    triangles.Add(idx + 1);
                    triangles.Add(idx + 3);

                    triangles.Add(idx + 0);
                    triangles.Add(idx + 3);
                    triangles.Add(idx + 2);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
