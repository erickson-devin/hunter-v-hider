using UnityEngine;

namespace HunterVsHider.Player
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FieldOfView : MonoBehaviour
    {
        public float viewRadius = 5f;
        [Range(0, 360)]
        public float viewAngle = 90f;

        public void FindVisibleTargets()
        {
            // Placeholder for Physics2D overlap checks and raycasting
        }
    }
}
