using System;
using System.Collections.Generic;
using UnityEngine;
using HunterVsHider.Player;

namespace HunterVsHider.Vision
{
    public class VisionController : MonoBehaviour
    {
        [Header("Eye / Origin Settings")]
        [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.8f, 0f);

        [Header("FOV Settings")]
        [Range(10f, 360f)]
        [SerializeField] private float viewAngle = 90f;
        [SerializeField] private float viewDistance = 25f;
        [Range(10, 360)]
        [SerializeField] private int rayCount = 120;

        [Header("Layer Masks")]
        [SerializeField] private LayerMask obstacleHighLayer;
        [SerializeField] private LayerMask obstacleLowLayer;

        [Header("Low Obstacle Cover Settings")]
        [SerializeField] private float defaultLowObstacleHeight = 1.0f;
        [SerializeField] private float groundPlaneY = 0f;

        public struct RayVisionResult
        {
            public Vector3 direction;
            public float firstHitDist;
            public bool hasLowObstacleShadow;
            public float shadowEndDist;
            public float secondHitDist;
        }

        private readonly List<RayVisionResult> rayResults = new List<RayVisionResult>();

        public float ViewAngle => viewAngle;
        public float ViewDistance => viewDistance;
        public int RayCount => rayCount;
        public Vector3 EyeOffset => eyeOffset;
        public Vector3 EyeWorldPosition => transform.position + eyeOffset;
        public IReadOnlyList<RayVisionResult> RayResults => rayResults;

        private void Awake()
        {
            ValidateLayers();
            EnsureFOVIndicator();
        }

        private void EnsureFOVIndicator()
        {
            FieldOfView fov = GetComponentInChildren<FieldOfView>();
            if (fov == null)
            {
                Transform fovTrans = transform.Find("FOV_Indicator");
                GameObject fovObj;
                if (fovTrans == null)
                {
                    fovObj = new GameObject("FOV_Indicator");
                    fovObj.transform.SetParent(transform, false);
                    fovObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                }
                else
                {
                    fovObj = fovTrans.gameObject;
                }

                fov = fovObj.AddComponent<FieldOfView>();
            }

            MeshRenderer mr = fov.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial == null)
            {
                Shader maskShader = Shader.Find("Custom/FoW_Mask");
                if (maskShader != null)
                {
                    mr.material = new Material(maskShader);
                }
            }
        }

        private void OnEnable()
        {
            if (FogOfWarManager.Instance != null)
            {
                FogOfWarManager.Instance.RegisterVisionController(this);
            }
        }

        private void OnDisable()
        {
            if (FogOfWarManager.Instance != null)
            {
                FogOfWarManager.Instance.UnregisterVisionController(this);
            }
        }

        public void ValidateLayers()
        {
            if (obstacleHighLayer.value == 0)
            {
                int highLayer = LayerMask.NameToLayer("ObstacleHigh");
                int defaultObstacle = LayerMask.NameToLayer("Obstacle");
                int mask = 0;
                if (highLayer != -1) mask |= (1 << highLayer);
                if (defaultObstacle != -1) mask |= (1 << defaultObstacle);

                obstacleHighLayer = mask != 0 ? mask : 1;
            }

            if (obstacleLowLayer.value == 0)
            {
                int lowLayer = LayerMask.NameToLayer("ObstacleLow");
                if (lowLayer != -1) obstacleLowLayer = (1 << lowLayer);
            }
        }

        public void CalculateVision()
        {
            rayResults.Clear();

            Vector3 eyePos = EyeWorldPosition;
            float startAngle = -viewAngle / 2f;
            float angleStep = viewAngle / rayCount;
            int combinedMask = obstacleHighLayer | obstacleLowLayer;

            for (int i = 0; i <= rayCount; i++)
            {
                float currentAngle = startAngle + i * angleStep;
                Vector3 dir = Quaternion.Euler(0f, currentAngle, 0f) * transform.forward;
                dir.y = 0f;
                dir.Normalize();

                RayVisionResult result = new RayVisionResult
                {
                    direction = dir,
                    firstHitDist = viewDistance,
                    hasLowObstacleShadow = false,
                    shadowEndDist = viewDistance,
                    secondHitDist = viewDistance
                };

                // Cast primary ray from eye position
                if (Physics.Raycast(eyePos, dir, out RaycastHit hit, viewDistance, combinedMask))
                {
                    int hitLayerMask = 1 << hit.collider.gameObject.layer;

                    if ((obstacleHighLayer.value & hitLayerMask) != 0)
                    {
                        // 3m wall: completely occludes vision, terminates ray
                        result.firstHitDist = hit.distance;
                        result.hasLowObstacleShadow = false;
                    }
                    else if ((obstacleLowLayer.value & hitLayerMask) != 0)
                    {
                        // 1m cover box: calculate clearance angle
                        result.firstHitDist = hit.distance;
                        result.hasLowObstacleShadow = true;

                        float obstacleHeight = defaultLowObstacleHeight;
                        if (hit.collider != null)
                        {
                            obstacleHeight = hit.collider.bounds.max.y;
                        }

                        float eyeHeight = eyePos.y;
                        float d1 = hit.distance;

                        if (eyeHeight > obstacleHeight && d1 > 0.001f)
                        {
                            // Slope of sightline from eye over obstacle top
                            float slope = (eyeHeight - obstacleHeight) / d1;
                            
                            // Distance behind box where sightline hits ground (y = groundPlaneY)
                            float dropToGround = Mathf.Max(0.01f, obstacleHeight - groundPlaneY);
                            float deltaDistance = dropToGround / slope;
                            float shadowEnd = d1 + deltaDistance;

                            result.shadowEndDist = Mathf.Min(viewDistance, shadowEnd);

                            // Check if area beyond shadow has line of sight to max view distance
                            if (result.shadowEndDist < viewDistance)
                            {
                                float remainingDist = viewDistance - result.shadowEndDist;
                                Vector3 continueOrigin = eyePos + dir * result.shadowEndDist;

                                if (Physics.Raycast(continueOrigin, dir, out RaycastHit secondHit, remainingDist, combinedMask))
                                {
                                    result.secondHitDist = result.shadowEndDist + secondHit.distance;
                                }
                                else
                                {
                                    result.secondHitDist = viewDistance;
                                }
                            }
                            else
                            {
                                result.secondHitDist = viewDistance;
                            }
                        }
                        else
                        {
                            // Eye is below or at obstacle height: full shadow
                            result.shadowEndDist = viewDistance;
                            result.secondHitDist = viewDistance;
                        }
                    }
                    else
                    {
                        result.firstHitDist = hit.distance;
                    }
                }
                else
                {
                    result.firstHitDist = viewDistance;
                }

                rayResults.Add(result);
            }
        }

        /// <summary>
        /// Checks whether a target point is in active line of sight of this police unit.
        /// </summary>
        public bool IsTargetInActiveVision(Vector3 targetPos, float targetHeight = 1.0f)
        {
            Vector3 eyePos = EyeWorldPosition;
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            float horizontalDist = toTarget.magnitude;

            if (horizontalDist > viewDistance) return false;

            float angleToTarget = Vector3.Angle(transform.forward, toTarget.normalized);
            if (angleToTarget > viewAngle / 2f) return false;

            Vector3 targetCheckPoint = targetPos + Vector3.up * targetHeight;
            Vector3 rayDir = targetCheckPoint - eyePos;
            float rayLen = rayDir.magnitude;
            if (rayLen < 0.01f) return true;

            int combinedMask = obstacleHighLayer | obstacleLowLayer;

            // Direct line of sight raycast
            if (Physics.Raycast(eyePos, rayDir.normalized, out RaycastHit hit, rayLen, combinedMask))
            {
                // If we hit high obstacle before target, blocked completely
                if ((obstacleHighLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    return false;
                }

                // If we hit low obstacle, check if target is hidden behind low cover
                if ((obstacleLowLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    float obsTop = hit.collider.bounds.max.y;
                    float hitDist = Vector3.Distance(new Vector3(eyePos.x, 0, eyePos.z), new Vector3(hit.point.x, 0, hit.point.z));
                    
                    if (eyePos.y > obsTop && hitDist > 0.01f)
                    {
                        float slope = (eyePos.y - obsTop) / hitDist;
                        float rayYAtTarget = obsTop - slope * (horizontalDist - hitDist);
                        if (targetCheckPoint.y <= rayYAtTarget)
                        {
                            // Target is occluded in the shadow behind low box
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 eyePos = EyeWorldPosition;
            Gizmos.DrawWireSphere(eyePos, 0.2f);

            Vector3 leftDir = Quaternion.Euler(0f, -viewAngle / 2f, 0f) * transform.forward;
            Vector3 rightDir = Quaternion.Euler(0f, viewAngle / 2f, 0f) * transform.forward;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(eyePos, leftDir * viewDistance);
            Gizmos.DrawRay(eyePos, rightDir * viewDistance);
        }
    }
}
