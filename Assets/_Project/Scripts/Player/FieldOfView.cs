using System.Collections.Generic;
using UnityEngine;
using HunterVsHider.Vision;

namespace HunterVsHider.Player
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FieldOfView : MonoBehaviour
    {
        public float viewRadius = 5f;
        [Range(0, 360)]
        public float viewAngle = 90f;
        public bool isVisionMaskSuppressed { get; set; } = false;

        [Header("Mesh Settings")]
        [SerializeField] private float meshYOffset = 0.05f;

        private Mesh mesh;
        private MeshFilter meshFilter;
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<int> triangles = new List<int>(3072);
        private readonly List<Vector2> uvs = new List<Vector2>(1024);

        public Mesh CurrentMesh => mesh;

        private void Awake()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            if (visionController == null)
            {
                visionController = GetComponentInParent<VisionController>() ?? GetComponent<VisionController>();
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = "Dynamic_FOV_Mesh"
                };
                mesh.MarkDynamic();
                if (meshFilter != null)
                {
                    meshFilter.mesh = mesh;
                }
            }
        }

        private void OnDisable()
        {
            CleanupMesh();
        }

        private void OnDestroy()
        {
            CleanupMesh();
        }

        private void CleanupMesh()
        {
            if (mesh != null)
            {
                if (meshFilter != null && meshFilter.sharedMesh == mesh)
                {
                    meshFilter.sharedMesh = null;
                }
                SafeDestroy(mesh);
                mesh = null;
            }
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private void LateUpdate()
        {
            EnsureComponents();
            if (visionController == null) return;

            visionController.CalculateVision();
            GenerateFOVMesh();
        }

        public void GenerateFOVMesh()
        {
            EnsureComponents();
            if (visionController == null) return;

            IReadOnlyList<VisionController.RayVisionResult> rays = visionController.RayResults;
            IReadOnlyList<VisionController.ProximityRayResult> proxRays = visionController.ProximityResults;

            if (rays == null || rays.Count < 2) return;

            vertices.Clear();
            triangles.Clear();
            uvs.Clear();

            // 1. Build Proximity Circle Fan (Centered around player base position)
            if (proxRays != null && proxRays.Count >= 3)
            {
                Vector3 proxWorldOrigin = visionController.transform.position;
                Vector3 proxLocalOrigin = transform.InverseTransformPoint(proxWorldOrigin);
                proxLocalOrigin.y = meshYOffset;

                int proxOriginIdx = vertices.Count;
                vertices.Add(proxLocalOrigin);
                uvs.Add(new Vector2(0.5f, 0.5f));

                int count = proxRays.Count;
                for (int i = 0; i < count; i++)
                {
                    Vector3 worldPt = proxWorldOrigin + proxRays[i].direction * proxRays[i].hitDist;
                    Vector3 localPt = transform.InverseTransformPoint(worldPt);
                    localPt.y = meshYOffset;
                    vertices.Add(localPt);
                    uvs.Add(new Vector2(localPt.x, localPt.z));
                }

                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    triangles.Add(proxOriginIdx);
                    triangles.Add(proxOriginIdx + 1 + i);
                    triangles.Add(proxOriginIdx + 1 + next);
                }
            }

            // 2. Build Directional FOV Wedge Fan (Apex locked strictly to active weapon MuzzlePoint / EyeWorldPosition)
            Vector3 eyeWorldPos = visionController.EyeWorldPosition;
            Vector3 wedgeLocalOrigin = transform.InverseTransformPoint(eyeWorldPos);
            wedgeLocalOrigin.y = meshYOffset;

            int wedgeOriginIdx = vertices.Count;
            vertices.Add(wedgeLocalOrigin);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int rayCount = rays.Count;
            for (int i = 0; i < rayCount; i++)
            {
                Vector3 worldHit = eyeWorldPos + rays[i].direction * rays[i].firstHitDist;
                Vector3 localHit = transform.InverseTransformPoint(worldHit);
                localHit.y = meshYOffset;

                vertices.Add(localHit);
                uvs.Add(new Vector2(localHit.x, localHit.z));
            }

            for (int i = 0; i < rayCount - 1; i++)
            {
                int v1 = wedgeOriginIdx;
                int v2 = wedgeOriginIdx + 1 + i;
                int v3 = wedgeOriginIdx + 1 + i + 1;

                triangles.Add(v1);
                triangles.Add(v2);
                triangles.Add(v3);
            }

            // 3. Build Outer Visible Strips beyond low obstacle shadows
            for (int i = 0; i < rayCount - 1; i++)
            {
                var rA = rays[i];
                var rB = rays[i + 1];

                if (rA.hasLowObstacleShadow && rB.hasLowObstacleShadow &&
                    rA.shadowEndDist < rA.secondHitDist && rB.shadowEndDist < rB.secondHitDist)
                {
                    Vector3 worldInnerA = eyeWorldPos + rA.direction * rA.shadowEndDist;
                    Vector3 worldOuterA = eyeWorldPos + rA.direction * rA.secondHitDist;

                    Vector3 worldInnerB = eyeWorldPos + rB.direction * rB.shadowEndDist;
                    Vector3 worldOuterB = eyeWorldPos + rB.direction * rB.secondHitDist;

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

                    triangles.Add(idx + 0);
                    triangles.Add(idx + 1);
                    triangles.Add(idx + 3);

                    triangles.Add(idx + 0);
                    triangles.Add(idx + 3);
                    triangles.Add(idx + 2);
                }
            }

            if (mesh != null)
            {
                mesh.Clear(false);
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0, false);
                mesh.RecalculateBounds();
            }
        }

        public void ClearExploredMemoryGrid()
        {
            // Facade placeholder
        }
    }
}
