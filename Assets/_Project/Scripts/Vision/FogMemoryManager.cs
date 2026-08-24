using UnityEngine;

namespace HunterVsHider.Vision
{
    [RequireComponent(typeof(Camera))]
    public class FogMemoryManager : MonoBehaviour
    {
        public static FogMemoryManager Instance { get; private set; }

        public event System.Action<RenderTexture> OnFoWTextureReady;

        [Header("Render Texture Targets")]
        [Tooltip("The output RenderTexture consumed by the world / post-process shader.")]
        public RenderTexture fovMaskRT;

        [Header("Texture Resolution & Filtering")]
        [Tooltip("Resolution of the Fog of War mask textures (e.g. 2048).")]
        public int textureResolution = 2048;

        [Header("Memory Configuration")]
        public Material decayMaterial;

        [Tooltip("The persistent gray value retained for explored terrain (0.5 = 50% opacity).")]
        [Range(0.1f, 0.9f)]
        public float memoryFloor = 0.5f;

        private Camera visionCamera;
        private RenderTexture rawVisRT;
        private RenderTexture memoryBufferRT;
        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            visionCamera = GetComponent<Camera>();
            InitializeTextures();
        }

        private void Start()
        {
            if (!isInitialized)
            {
                InitializeTextures();
            }
        }

        private void InitializeTextures()
        {
            if (isInitialized) return;

            int width = fovMaskRT != null ? Mathf.Max(fovMaskRT.width, textureResolution) : textureResolution;
            int height = fovMaskRT != null ? Mathf.Max(fovMaskRT.height, textureResolution) : textureResolution;

            // 1. Raw visibility render texture rendered directly by VisionCamera
            rawVisRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "RawVis_RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rawVisRT.Create();

            // Direct VisionCamera to render solely the raw current-frame wedges into rawVisRT
            visionCamera.targetTexture = rawVisRT;

            // 2. Persistent memory buffer accumulating explored regions
            memoryBufferRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "MemoryBuffer_RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            memoryBufferRT.Create();

            if (fovMaskRT == null)
            {
                fovMaskRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "FoVMask_RT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                fovMaskRT.Create();
            }

            // Clear to pure black (Unexplored: RGBA 0,0,0,0)
            ClearRenderTexture(memoryBufferRT, Color.clear);
            ClearRenderTexture(fovMaskRT, Color.clear);

            // 3. Fallback memory material if not assigned in inspector
            if (decayMaterial == null)
            {
                Shader decayShader = Shader.Find("HunterVsHider/FoW_MemoryDecay");
                if (decayShader != null)
                {
                    decayMaterial = new Material(decayShader);
                }
            }

            isInitialized = true;
            OnFoWTextureReady?.Invoke(fovMaskRT);
        }

        private void LateUpdate()
        {
            if (!isInitialized || decayMaterial == null || rawVisRT == null || memoryBufferRT == null) return;

            // State guard: Do not accumulate memory during non-combat phases (Lobby / PrepPhase)
            var matchMgr = HunterVsHider.Managers.MatchManager.Instance ?? HunterVsHider.Managers.MatchManager.Singleton;
            bool isNonCombat = matchMgr == null || 
                               matchMgr.CurrentState == HunterVsHider.Managers.MatchState.WaitingForPlayers ||
                               matchMgr.CurrentState == HunterVsHider.Managers.MatchState.PrepPhase ||
                               matchMgr.CurrentState == HunterVsHider.Managers.MatchState.RoleAssignment;

            if (isNonCombat)
            {
                // Suppress persistent memory accumulation in non-combat phases
                return;
            }

            decayMaterial.SetTexture("_CurrentVisTex", rawVisRT);
            decayMaterial.SetFloat("_MemoryFloor", memoryFloor);

            RenderTexture tempRT = RenderTexture.GetTemporary(memoryBufferRT.width, memoryBufferRT.height, 0, RenderTextureFormat.ARGB32);
            tempRT.filterMode = FilterMode.Bilinear;
            tempRT.wrapMode = TextureWrapMode.Clamp;

            // Pass 0: Instant Memory Accumulation (writes memory floor 0.5 into memoryBufferRT)
            Graphics.Blit(memoryBufferRT, tempRT, decayMaterial, 0);
            Graphics.Blit(tempRT, memoryBufferRT);

            // Pass 1: Combined Mask Output (outputs 1.0 active, 0.5 instant memory, 0.0 unexplored into fovMaskRT)
            if (fovMaskRT != null)
            {
                Graphics.Blit(memoryBufferRT, fovMaskRT, decayMaterial, 1);
            }

            RenderTexture.ReleaseTemporary(tempRT);
        }

        public void ResetFog()
        {
            if (rawVisRT != null) ClearRenderTexture(rawVisRT, Color.clear);
            if (memoryBufferRT != null) ClearRenderTexture(memoryBufferRT, Color.clear);
            if (fovMaskRT != null) ClearRenderTexture(fovMaskRT, Color.clear);
            Debug.Log("[FogMemoryManager] ResetFog() -> Flushed rawVisRT, memoryBufferRT, and fovMaskRT to 100% pitch black (Color.clear).");
            OnFoWTextureReady?.Invoke(fovMaskRT);
        }

        public void ClearExploredMemoryGrid()
        {
            ResetFog();
        }

        /// <summary>
        /// Initializes or clears the entire arena mask to the explored memory state (no black fog).
        /// Used by the Assassin who sees the entire map in gray memory by default.
        /// </summary>
        /// <param name="memoryLevel">Gray level matching memoryFloor (default 0.5).</param>
        public void SetAllToMemory(float memoryLevel = 0.5f)
        {
            Color memColor = new Color(memoryLevel, memoryLevel, memoryLevel, 1f);
            if (memoryBufferRT != null) ClearRenderTexture(memoryBufferRT, memColor);
            if (fovMaskRT != null) ClearRenderTexture(fovMaskRT, memColor);
        }

        private void ClearRenderTexture(RenderTexture rt, Color color)
        {
            if (rt == null) return;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, color);
            RenderTexture.active = prev;
        }

        private void OnDestroy()
        {
            if (rawVisRT != null)
            {
                rawVisRT.Release();
                Destroy(rawVisRT);
            }
            if (memoryBufferRT != null)
            {
                memoryBufferRT.Release();
                Destroy(memoryBufferRT);
            }
        }
    }
}
