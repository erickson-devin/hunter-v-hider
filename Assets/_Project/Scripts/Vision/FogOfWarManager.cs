using UnityEngine;

namespace HunterVsHider.Vision
{
    /// <summary>
    /// Fog of War vision manager component.
    /// Provides local client RenderTexture fovMaskRT and instance texture ready event access.
    /// Attached to Player entity and/or Fog system root to ensure local client authority resolution.
    /// </summary>
    public class FogOfWarManager : MonoBehaviour
    {
        public static FogOfWarManager Instance { get; private set; }

        /// <summary>
        /// Instance event fired when this FoW manager's mask RenderTexture is generated or updated.
        /// </summary>
        public event System.Action<RenderTexture> OnFoWTextureReady;

        [Tooltip("Output FoW mask RenderTexture consumed by the world / UI shaders.")]
        [SerializeField] private RenderTexture customFovMaskRT;

        public RenderTexture fovMaskRT
        {
            get
            {
                if (FogMemoryManager.Instance != null && FogMemoryManager.Instance.fovMaskRT != null)
                {
                    return FogMemoryManager.Instance.fovMaskRT;
                }
                return customFovMaskRT;
            }
            set
            {
                customFovMaskRT = value;
                OnFoWTextureReady?.Invoke(customFovMaskRT);
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void OnEnable()
        {
            if (FogMemoryManager.Instance != null)
            {
                FogMemoryManager.Instance.OnFoWTextureReady += HandleFogMemoryTextureReady;
            }
        }

        private void OnDisable()
        {
            if (FogMemoryManager.Instance != null)
            {
                FogMemoryManager.Instance.OnFoWTextureReady -= HandleFogMemoryTextureReady;
            }
        }

        private void Start()
        {
            if (FogMemoryManager.Instance != null)
            {
                FogMemoryManager.Instance.OnFoWTextureReady -= HandleFogMemoryTextureReady;
                FogMemoryManager.Instance.OnFoWTextureReady += HandleFogMemoryTextureReady;
            }

            if (fovMaskRT != null)
            {
                OnFoWTextureReady?.Invoke(fovMaskRT);
            }
        }

        private void HandleFogMemoryTextureReady(RenderTexture rt)
        {
            OnFoWTextureReady?.Invoke(rt);
        }

        public void ResetFog()
        {
            FogMemoryManager.Instance?.ResetFog();
            if (fovMaskRT != null)
            {
                OnFoWTextureReady?.Invoke(fovMaskRT);
            }
        }

        public void ClearExploredMemoryGrid()
        {
            FogMemoryManager.Instance?.ClearExploredMemoryGrid();
            if (fovMaskRT != null)
            {
                OnFoWTextureReady?.Invoke(fovMaskRT);
            }
        }

        public void SetAllToMemory(float memoryFloor = 0.5f)
        {
            FogMemoryManager.Instance?.SetAllToMemory(memoryFloor);
            if (fovMaskRT != null)
            {
                OnFoWTextureReady?.Invoke(fovMaskRT);
            }
        }
    }
}
