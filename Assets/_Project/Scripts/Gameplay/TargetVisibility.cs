using UnityEngine;
using HunterVsHider.Vision;

namespace HunterVsHider.Gameplay
{
    public class TargetVisibility : MonoBehaviour
    {
        [Header("Target Height Offset")]
        [SerializeField] private float checkHeight = 1.0f;

        [Header("Initial State")]
        [SerializeField] private bool startsVisible = false;

        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;
        private bool isCurrentlyVisible = false;

        public bool IsVisible => isCurrentlyVisible;

        private void Awake()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);

            SetVisibility(startsVisible);
        }

        private void Update()
        {
            if (FogOfWarManager.IsShuttingDown) return;

            bool shouldBeVisible = false;

            if (FogOfWarManager.Instance != null)
            {
                shouldBeVisible = FogOfWarManager.Instance.IsPositionVisible(transform.position, checkHeight);
            }

            if (shouldBeVisible != isCurrentlyVisible)
            {
                SetVisibility(shouldBeVisible);
            }
        }

        public void SetVisibility(bool visible)
        {
            isCurrentlyVisible = visible;

            if (cachedRenderers != null)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] != null)
                    {
                        cachedRenderers[i].enabled = visible;
                    }
                }
            }

            if (cachedColliders != null)
            {
                for (int i = 0; i < cachedColliders.Length; i++)
                {
                    if (cachedColliders[i] != null)
                    {
                        cachedColliders[i].enabled = visible;
                    }
                }
            }
        }
    }
}
