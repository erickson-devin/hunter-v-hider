using UnityEngine;

namespace HunterVsHider.Vision
{
    /// <summary>
    /// Static and singleton alias for the Fog of War vision system (FogMemoryManager).
    /// Exposes event-driven FoW texture ready callbacks and instance access.
    /// </summary>
    public static class FogOfWarManager
    {
        public static FogMemoryManager Instance => FogMemoryManager.Instance;

        public static event System.Action<RenderTexture> OnFoWTextureReady
        {
            add
            {
                if (FogMemoryManager.Instance != null)
                {
                    FogMemoryManager.Instance.OnFoWTextureReady += value;
                }
            }
            remove
            {
                if (FogMemoryManager.Instance != null)
                {
                    FogMemoryManager.Instance.OnFoWTextureReady -= value;
                }
            }
        }
    }
}
