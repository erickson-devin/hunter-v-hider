using UnityEngine;

namespace HunterVsHider.Vision
{
    /// <summary>
    /// DynamicFog component providing DFM2 Fog of War settings,
    /// World Center, World Size, and Texture Resolution calibration.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(FoW_PostProcessFeature))]
    public class DynamicFog : MonoBehaviour
    {
        [Header("DFM2 Fog of War World Bounds")]
        [Tooltip("World center point of the Fog of War volume (X: 0, Y: 0, Z: 0).")]
        public Vector3 worldCenter = Vector3.zero;

        [Tooltip("Physical world dimensions in meters (X: 300, Z: 300).")]
        public Vector2 worldSize = new Vector2(300f, 300f);

        [Header("Texture Resolution Calibration")]
        [Tooltip("Texture resolution for FoV mask rendering (1024 or 2048).")]
        public int textureSize = 2048;

        [Header("DFM2 Line-of-Sight Layer Mask")]
        [Tooltip("Layer mask determining which objects block Line of Sight and cast Fog Shadows (Obstacle layer).")]
        public LayerMask obstacleMask = 1 << 7; // Layer 7: Obstacle

        public static DynamicFog Instance { get; private set; }

        private FoW_PostProcessFeature fowFeature;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            SyncWithPostProcessFeature();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            SyncWithPostProcessFeature();
        }

        private void OnValidate()
        {
            SyncWithPostProcessFeature();
        }

        [Header("Target Tracking (Flattened Ground Plane)")]
        [Tooltip("The tracked player transform in World Space.")]
        public Transform trackedPlayer;

        public Vector3 LastFlattenedTargetPos { get; private set; }

        public void SetFogOfWarAlpha(Vector3 targetPos, float radius, float alpha)
        {
            // Flatten target to physical floor plane (Y = 0) to eliminate 60-degree camera parallax offset
            Vector3 fogTargetPos = new Vector3(targetPos.x, 0f, targetPos.z);
            LastFlattenedTargetPos = fogTargetPos;
        }

        public void UpdatePlayerTracking(Transform localPlayer)
        {
            if (localPlayer == null) return;
            trackedPlayer = localPlayer;
            Vector3 fogTargetPos = new Vector3(localPlayer.position.x, 0f, localPlayer.position.z);
            LastFlattenedTargetPos = fogTargetPos;
        }

        public void SyncWithPostProcessFeature()
        {
            if (fowFeature == null) fowFeature = GetComponent<FoW_PostProcessFeature>();
            if (fowFeature != null)
            {
                fowFeature.worldCenter = worldCenter;
                fowFeature.worldSize = worldSize;
                fowFeature.textureSize = textureSize;
            }
        }
    }
}
