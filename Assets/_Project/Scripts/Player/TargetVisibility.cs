using UnityEngine;

namespace HunterVsHider.Player
{
    /// <summary>
    /// Line-of-sight visibility marker component.
    /// Tracks whether this player entity is currently visible to the local client.
    /// Synchronized frame-by-frame by PlayerVisibilityManager.
    /// </summary>
    public class TargetVisibility : MonoBehaviour
    {
        [Tooltip("True if this entity is currently in direct line of sight / FOV of the local player.")]
        public bool IsVisible { get; set; } = true;
    }
}
