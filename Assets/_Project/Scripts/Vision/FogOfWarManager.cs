using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HunterVsHider.Player;

namespace HunterVsHider.Vision
{
    public class FogOfWarManager : MonoBehaviour
    {
        private static FogOfWarManager _instance;
        public static FogOfWarManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<FogOfWarManager>();
                    if (_instance == null && Application.isPlaying)
                    {
                        GameObject go = new GameObject("_FogOfWarManager");
                        _instance = go.AddComponent<FogOfWarManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Arena Bounds Settings (50m x 50m)")]
        [SerializeField] private Vector2 arenaMin = new Vector2(-25f, -25f);
        [SerializeField] private Vector2 arenaMax = new Vector2(25f, 25f);

        [Header("Texture Settings")]
        [SerializeField] private int textureResolution = 1024;

        [Header("Shader References")]
        [SerializeField] private Shader stampShader;
        [SerializeField] private Shader accumulateShader;

        private RenderTexture activeVisionRT;
        private RenderTexture combinedFoWRT;
        private RenderTexture prevCombinedFoWRT;

        private Material stampMaterial;
        private Material accumulateMaterial;

        private readonly List<VisionController> activeVisionControllers = new List<VisionController>();
        private CommandBuffer renderCmdBuffer;

        public RenderTexture CombinedFoWTexture => combinedFoWRT;
        public Vector2 ArenaMin => arenaMin;
        public Vector2 ArenaMax => arenaMax;
        public Vector2 ArenaSize => arenaMax - arenaMin;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            InitializeResources();
            EnsureCameraSetup();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            ReleaseResources();
        }

        private void EnsureCameraSetup()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = GameObject.FindGameObjectWithTag("MainCamera");
                if (camGo != null) mainCam = camGo.GetComponent<Camera>();
            }

            if (mainCam != null)
            {
                mainCam.depthTextureMode |= DepthTextureMode.Depth;

                // Ensure screen-space darkness overlay quad exists
                Transform existingQuad = mainCam.transform.Find("FoW_ScreenDarkness");
                GameObject screenDarkness;
                if (existingQuad == null)
                {
                    screenDarkness = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    screenDarkness.name = "FoW_ScreenDarkness";
                    screenDarkness.transform.SetParent(mainCam.transform, false);
                }
                else
                {
                    screenDarkness = existingQuad.gameObject;
                }

                screenDarkness.transform.localPosition = new Vector3(0f, 0f, 1.5f);
                screenDarkness.transform.localRotation = Quaternion.identity;
                screenDarkness.transform.localScale = new Vector3(50f, 50f, 1f);

                Collider col = screenDarkness.GetComponent<Collider>();
                if (col != null) Destroy(col);

                MeshRenderer mr = screenDarkness.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Shader blitShader = Shader.Find("Custom/FogOfWarBlit");
                    if (blitShader != null && (mr.sharedMaterial == null || mr.sharedMaterial.shader != blitShader))
                    {
                        Material blitMat = new Material(blitShader);
                        blitMat.SetColor("_UnexploredColor", new Color(0.039f, 0.039f, 0.047f, 1.0f));
                        blitMat.SetColor("_ExploredColor", new Color(0.106f, 0.133f, 0.173f, 0.72f));
                        blitMat.SetFloat("_UnexploredAlpha", 1.0f);
                        blitMat.SetFloat("_ExploredAlpha", 0.72f);
                        mr.material = blitMat;
                    }
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
            }
        }

        private void InitializeResources()
        {
            if (stampShader == null) stampShader = Shader.Find("Custom/FoW_Stamp");
            if (accumulateShader == null) accumulateShader = Shader.Find("Custom/FoW_Accumulate");

            if (stampShader != null) stampMaterial = new Material(stampShader);
            if (accumulateShader != null) accumulateMaterial = new Material(accumulateShader);

            activeVisionRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32)
            {
                name = "FoW_ActiveVision_RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            activeVisionRT.Create();

            combinedFoWRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32)
            {
                name = "FoW_Combined_RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            combinedFoWRT.Create();

            prevCombinedFoWRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32)
            {
                name = "FoW_PrevCombined_RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            prevCombinedFoWRT.Create();

            // Clear textures to 0 (Unexplored)
            ClearTexture(activeVisionRT, Color.clear);
            ClearTexture(combinedFoWRT, Color.clear);
            ClearTexture(prevCombinedFoWRT, Color.clear);

            renderCmdBuffer = new CommandBuffer
            {
                name = "FoW_RenderActiveVision"
            };
        }

        private void ReleaseResources()
        {
            if (activeVisionRT != null) { activeVisionRT.Release(); Destroy(activeVisionRT); }
            if (combinedFoWRT != null) { combinedFoWRT.Release(); Destroy(combinedFoWRT); }
            if (prevCombinedFoWRT != null) { prevCombinedFoWRT.Release(); Destroy(prevCombinedFoWRT); }

            if (stampMaterial != null) Destroy(stampMaterial);
            if (accumulateMaterial != null) Destroy(accumulateMaterial);
            if (renderCmdBuffer != null) { renderCmdBuffer.Release(); renderCmdBuffer = null; }
        }

        private void ClearTexture(RenderTexture rt, Color color)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, color);
            RenderTexture.active = prev;
        }

        public void RegisterVisionController(VisionController controller)
        {
            if (controller != null && !activeVisionControllers.Contains(controller))
            {
                activeVisionControllers.Add(controller);
            }
        }

        public void UnregisterVisionController(VisionController controller)
        {
            if (controller != null)
            {
                activeVisionControllers.Remove(controller);
            }
        }

        private void LateUpdate()
        {
            UpdateFogOfWar();
        }

        public void UpdateFogOfWar()
        {
            if (stampMaterial == null || accumulateMaterial == null || activeVisionRT == null)
            {
                return;
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.depthTextureMode |= DepthTextureMode.Depth;
                Matrix4x4 vp = mainCam.projectionMatrix * mainCam.worldToCameraMatrix;
                Shader.SetGlobalMatrix("_FoW_InvVP", vp.inverse);
            }

            // 1. Setup Top-Down Orthographic Projection for the 50x50 arena
            Vector2 size = arenaMax - arenaMin;
            Vector2 center = (arenaMin + arenaMax) * 0.5f;

            Vector3 eye = new Vector3(center.x, 50f, center.y);
            Vector3 target = new Vector3(center.x, 0f, center.y);
            Vector3 up = Vector3.forward; // Maps +Z in world to +Y in UV

            Matrix4x4 viewMat = Matrix4x4.LookAt(eye, target, up);
            Matrix4x4 projMat = Matrix4x4.Ortho(-size.x * 0.5f, size.x * 0.5f, -size.y * 0.5f, size.y * 0.5f, 0.1f, 100f);
            projMat = GL.GetGPUProjectionMatrix(projMat, true);

            // 2. Render all active Police FOV meshes into ActiveVisionRT (Green channel = 1.0)
            renderCmdBuffer.Clear();
            renderCmdBuffer.SetRenderTarget(activeVisionRT);
            renderCmdBuffer.ClearRenderTarget(true, true, Color.clear);
            renderCmdBuffer.SetViewProjectionMatrices(viewMat, projMat);

            for (int i = 0; i < activeVisionControllers.Count; i++)
            {
                var controller = activeVisionControllers[i];
                if (controller == null || !controller.isActiveAndEnabled) continue;

                var fov = controller.GetComponentInChildren<FieldOfView>() ?? controller.GetComponent<FieldOfView>();
                if (fov != null && fov.CurrentMesh != null && fov.CurrentMesh.vertexCount > 2)
                {
                    renderCmdBuffer.DrawMesh(fov.CurrentMesh, fov.transform.localToWorldMatrix, stampMaterial);
                }
            }

            Graphics.ExecuteCommandBuffer(renderCmdBuffer);

            // 3. Accumulate active vision into persistent discovery buffer (Red = Discovery, Green = Active Vision)
            accumulateMaterial.SetTexture("_PrevCombinedTex", prevCombinedFoWRT);
            Graphics.Blit(activeVisionRT, combinedFoWRT, accumulateMaterial);

            // Ping-pong discovery buffer
            Graphics.Blit(combinedFoWRT, prevCombinedFoWRT);

            // 4. Update Global Shader Properties
            Shader.SetGlobalTexture("_FogOfWarTex", combinedFoWRT);
            Shader.SetGlobalVector("_FoWArenaMin", new Vector4(arenaMin.x, arenaMin.y, 0f, 0f));
            Shader.SetGlobalVector("_FoWArenaSize", new Vector4(size.x, size.y, 0f, 0f));
        }

        /// <summary>
        /// Real-time query to check if a world position is currently inside active vision of any Police unit.
        /// </summary>
        public bool IsPositionVisible(Vector3 worldPos, float targetHeight = 1.0f)
        {
            for (int i = 0; i < activeVisionControllers.Count; i++)
            {
                var controller = activeVisionControllers[i];
                if (controller != null && controller.isActiveAndEnabled)
                {
                    if (controller.IsTargetInActiveVision(worldPos, targetHeight))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a world position has been discovered/explored by the Police team.
        /// </summary>
        public bool IsPositionExplored(Vector3 worldPos)
        {
            Vector2 arenaSize = arenaMax - arenaMin;
            float u = (worldPos.x - arenaMin.x) / arenaSize.x;
            float v = (worldPos.z - arenaMin.y) / arenaSize.y;

            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

            return IsPositionVisible(worldPos);
        }
    }
}
