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

        [Header("Target References")]
        [SerializeField] private Transform playerTransform;

        [Header("Arena Bounds Settings (50m x 50m)")]
        [SerializeField] private Vector2 arenaMin = new Vector2(-25f, -25f);
        [SerializeField] private Vector2 arenaMax = new Vector2(25f, 25f);

        [Header("Texture Settings")]
        [SerializeField] private int textureResolution = 1024;

        [Header("Materials & Shaders")]
        [SerializeField] private Material fowBlitMaterial;
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
        public RenderTexture ActiveVisionTexture => activeVisionRT;
        public Vector2 ArenaMin => arenaMin;
        public Vector2 ArenaMax => arenaMax;
        public Vector2 ArenaSize => arenaMax - arenaMin;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                SafeDestroy(gameObject);
                return;
            }
            _instance = this;

            FindPlayerIfNull();
            InitializeResources();
            EnsureCameraSetup();
        }

        private void Start()
        {
            FindPlayerIfNull();
            EnsureCameraSetup();
        }

        private void FindPlayerIfNull()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                if (player != null) playerTransform = player.transform;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            ReleaseResources();
        }

        public void EnsureCameraSetup()
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

                // Ensure screen-space darkness overlay quad exists under Main Camera
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

                // Put screen darkness on Layer 2 (Ignore Raycast)
                screenDarkness.layer = 2;

                screenDarkness.transform.localPosition = new Vector3(0f, 0f, 1.5f);
                screenDarkness.transform.localRotation = Quaternion.identity;
                screenDarkness.transform.localScale = new Vector3(50f, 50f, 1f);

                Collider col = screenDarkness.GetComponent<Collider>();
                if (col != null) SafeDestroy(col);

                MeshRenderer mr = screenDarkness.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (fowBlitMaterial == null)
                    {
                        Shader blitShader = Shader.Find("Custom/FogOfWarBlit");
                        if (blitShader != null)
                        {
                            fowBlitMaterial = new Material(blitShader);
                        }
                    }

                    if (fowBlitMaterial != null)
                    {
                        fowBlitMaterial.SetColor("_UnexploredColor", new Color(0.0f, 0.0f, 0.0f, 1.0f));
                        fowBlitMaterial.SetColor("_ExploredColor", new Color(0.05f, 0.07f, 0.09f, 0.70f));
                        fowBlitMaterial.SetFloat("_UnexploredAlpha", 1.0f);
                        fowBlitMaterial.SetFloat("_ExploredAlpha", 0.70f);
                        if (combinedFoWRT != null)
                        {
                            fowBlitMaterial.SetTexture("_FogOfWarTex", combinedFoWRT);
                        }
                        mr.material = fowBlitMaterial;
                    }

                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
            }
        }

        public void InitializeResources()
        {
            if (stampShader == null) stampShader = Shader.Find("Custom/FoW_Stamp");
            if (accumulateShader == null) accumulateShader = Shader.Find("Custom/FoW_Accumulate");

            if (stampShader != null && stampMaterial == null) stampMaterial = new Material(stampShader);
            if (accumulateShader != null && accumulateMaterial == null) accumulateMaterial = new Material(accumulateShader);

            if (activeVisionRT == null || !activeVisionRT.IsCreated())
            {
                activeVisionRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32)
                {
                    name = "FoW_ActiveVision_RT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                activeVisionRT.Create();
            }

            if (combinedFoWRT == null || !combinedFoWRT.IsCreated())
            {
                combinedFoWRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32)
                {
                    name = "FoW_Combined_RT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                combinedFoWRT.Create();
            }

            if (prevCombinedFoWRT == null || !prevCombinedFoWRT.IsCreated())
            {
                prevCombinedFoWRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32)
                {
                    name = "FoW_PrevCombined_RT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                prevCombinedFoWRT.Create();
            }

            // Clear textures to 0 (Unexplored)
            ClearTexture(activeVisionRT, Color.clear);
            ClearTexture(combinedFoWRT, Color.clear);
            ClearTexture(prevCombinedFoWRT, Color.clear);

            if (renderCmdBuffer == null)
            {
                renderCmdBuffer = new CommandBuffer
                {
                    name = "FoW_RenderActiveVision"
                };
            }

            Vector2 size = arenaMax - arenaMin;
            Shader.SetGlobalTexture("_FogOfWarTex", combinedFoWRT);
            Shader.SetGlobalVector("_FoWArenaMin", new Vector4(arenaMin.x, arenaMin.y, 0f, 0f));
            Shader.SetGlobalVector("_FoWArenaSize", new Vector4(size.x, size.y, 0f, 0f));
        }

        private void ReleaseResources()
        {
            if (activeVisionRT != null) { activeVisionRT.Release(); SafeDestroy(activeVisionRT); activeVisionRT = null; }
            if (combinedFoWRT != null) { combinedFoWRT.Release(); SafeDestroy(combinedFoWRT); combinedFoWRT = null; }
            if (prevCombinedFoWRT != null) { prevCombinedFoWRT.Release(); SafeDestroy(prevCombinedFoWRT); prevCombinedFoWRT = null; }

            if (stampMaterial != null) SafeDestroy(stampMaterial);
            if (accumulateMaterial != null) SafeDestroy(accumulateMaterial);
            if (renderCmdBuffer != null) { renderCmdBuffer.Release(); renderCmdBuffer = null; }
        }

        private void SafeDestroy(Object obj)
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

        private void ClearTexture(RenderTexture rt, Color color)
        {
            if (rt == null || !rt.IsCreated()) return;
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
            if (stampMaterial == null || accumulateMaterial == null || activeVisionRT == null || !activeVisionRT.IsCreated() || renderCmdBuffer == null)
            {
                InitializeResources();
                if (stampMaterial == null || accumulateMaterial == null || renderCmdBuffer == null) return;
            }

            if (activeVisionControllers.Count == 0)
            {
                VisionController[] controllers = Object.FindObjectsByType<VisionController>(FindObjectsInactive.Exclude);
                for (int c = 0; c < controllers.Length; c++)
                {
                    RegisterVisionController(controllers[c]);
                }
            }

            Vector2 size = arenaMax - arenaMin;

            if (stampMaterial != null)
            {
                stampMaterial.SetVector("_FoWArenaMin", new Vector4(arenaMin.x, arenaMin.y, 0f, 0f));
                stampMaterial.SetVector("_FoWArenaSize", new Vector4(size.x, size.y, 0f, 0f));
            }

            // 1. Render all active Police FOV meshes into ActiveVisionRT (Green channel = 1.0)
            renderCmdBuffer.Clear();
            renderCmdBuffer.SetRenderTarget(activeVisionRT);
            renderCmdBuffer.ClearRenderTarget(true, true, Color.clear);

            for (int i = 0; i < activeVisionControllers.Count; i++)
            {
                var controller = activeVisionControllers[i];
                if (controller == null || !controller.isActiveAndEnabled) continue;

                controller.CalculateVision();
                var fov = controller.GetComponentInChildren<FieldOfView>() ?? controller.GetComponent<FieldOfView>();
                if (fov != null)
                {
                    fov.EnsureComponents();
                    fov.GenerateFOVMesh();
                    if (fov.CurrentMesh != null && fov.CurrentMesh.vertexCount > 2)
                    {
                        renderCmdBuffer.DrawMesh(fov.CurrentMesh, fov.transform.localToWorldMatrix, stampMaterial);
                    }
                }
            }

            Graphics.ExecuteCommandBuffer(renderCmdBuffer);

            // 2. Accumulate active vision into persistent discovery buffer (Red = Discovery, Green = Active Vision)
            accumulateMaterial.SetTexture("_PrevCombinedTex", prevCombinedFoWRT);
            Graphics.Blit(activeVisionRT, combinedFoWRT, accumulateMaterial);

            // Ping-pong discovery buffer
            Graphics.Blit(combinedFoWRT, prevCombinedFoWRT);

            // 3. Update Global & Material Shader Properties
            Shader.SetGlobalTexture("_FogOfWarTex", combinedFoWRT);
            Shader.SetGlobalVector("_FoWArenaMin", new Vector4(arenaMin.x, arenaMin.y, 0f, 0f));
            Shader.SetGlobalVector("_FoWArenaSize", new Vector4(size.x, size.y, 0f, 0f));

            if (fowBlitMaterial != null)
            {
                fowBlitMaterial.SetTexture("_FogOfWarTex", combinedFoWRT);
                fowBlitMaterial.SetVector("_FoWArenaMin", new Vector4(arenaMin.x, arenaMin.y, 0f, 0f));
                fowBlitMaterial.SetVector("_FoWArenaSize", new Vector4(size.x, size.y, 0f, 0f));
            }
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
