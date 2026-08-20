using UnityEngine;

namespace HunterVsHider.Vision
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    public class FoW_PostProcessFeature : MonoBehaviour
    {
        [Header("Shader & Mask Resources")]
        public Shader fowShader;
        public RenderTexture fovMaskRT;

        [Header("Arena Mapping Bounds")]
        public Vector2 mapBoundsMin = new Vector2(-125f, -125f);
        public Vector2 mapBoundsSize = new Vector2(250f, 250f);

        [Header("Appearance")]
        [Range(0.1f, 1.0f)]
        public float memoryDarkness = 0.5f;
        public Color fogColor = new Color(0.02f, 0.02f, 0.03f, 1f);

        private Camera mainCamera;
        private Material fowMaterial;

        private void Awake()
        {
            mainCamera = GetComponent<Camera>();
            EnsureResources();
        }

        private void OnEnable()
        {
            if (mainCamera == null) mainCamera = GetComponent<Camera>();
            if (mainCamera != null)
            {
                mainCamera.depthTextureMode |= DepthTextureMode.Depth;
            }
            EnsureResources();
        }

        public void EnsureResources()
        {
            if (fowShader == null)
            {
                fowShader = Shader.Find("HunterVsHider/FoW_ScreenSpace");
            }

            if (fowMaterial == null && fowShader != null)
            {
                fowMaterial = new Material(fowShader) { hideFlags = HideFlags.DontSave };
            }

            if (fovMaskRT == null)
            {
                // Fallback to FogMemoryManager singleton or lookup
                if (FogMemoryManager.Instance != null && FogMemoryManager.Instance.fovMaskRT != null)
                {
                    fovMaskRT = FogMemoryManager.Instance.fovMaskRT;
                }
            }
        }

        [ImageEffectOpaque]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // Strict bypass: If MatchManager is in WaitingForPlayers (Lobby) state, disable Fog of War overlay entirely
            if (HunterVsHider.Managers.MatchManager.Instance == null ||
                HunterVsHider.Managers.MatchManager.Instance.CurrentState == HunterVsHider.Managers.MatchState.WaitingForPlayers)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (fowMaterial == null || fowShader == null)
            {
                EnsureResources();
            }

            if (fowMaterial == null || mainCamera == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (fovMaskRT == null && FogMemoryManager.Instance != null)
            {
                fovMaskRT = FogMemoryManager.Instance.fovMaskRT;
            }

            // Calculate frustum corner direction vectors in World Space
            float fov = mainCamera.fieldOfView;
            float aspect = mainCamera.aspect;
            float halfFovRad = fov * 0.5f * Mathf.Deg2Rad;
            Vector3 toRight = mainCamera.transform.right * Mathf.Tan(halfFovRad) * aspect;
            Vector3 toTop = mainCamera.transform.up * Mathf.Tan(halfFovRad);
            Vector3 forward = mainCamera.transform.forward;

            Vector3 topLeft = forward - toRight + toTop;
            Vector3 topRight = forward + toRight + toTop;
            Vector3 bottomRight = forward + toRight - toTop;
            Vector3 bottomLeft = forward - toRight - toTop;

            Matrix4x4 frustumCorners = Matrix4x4.identity;
            frustumCorners.SetRow(0, topLeft);
            frustumCorners.SetRow(1, topRight);
            frustumCorners.SetRow(2, bottomRight);
            frustumCorners.SetRow(3, bottomLeft);

            fowMaterial.SetMatrix("_FrustumCornersWS", frustumCorners);
            fowMaterial.SetVector("_CameraWS", mainCamera.transform.position);
            fowMaterial.SetVector("_MapBounds", new Vector4(mapBoundsMin.x, mapBoundsMin.y, mapBoundsSize.x, mapBoundsSize.y));
            fowMaterial.SetFloat("_MemoryDarkness", memoryDarkness);
            fowMaterial.SetColor("_FogColor", fogColor);

            if (fovMaskRT != null)
            {
                fowMaterial.SetTexture("_FoVMaskTex", fovMaskRT);
            }

            Graphics.Blit(source, destination, fowMaterial);
        }

        private void OnDestroy()
        {
            if (fowMaterial != null)
            {
                DestroyImmediate(fowMaterial);
            }
        }
    }
}
