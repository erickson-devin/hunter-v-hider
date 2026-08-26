using UnityEngine;

namespace HunterVsHider.Vision
{
    /// <summary>
    /// Wrapper / Facade component aliasing DynamicFOV for standard vision and weapon FOV hooks.
    /// </summary>
    public class FieldOfView : MonoBehaviour
    {
        private DynamicFOV dynamicFOV;

        public bool isVisionMaskSuppressed
        {
            get
            {
                if (dynamicFOV == null) dynamicFOV = GetComponentInChildren<DynamicFOV>(true);
                return (dynamicFOV != null) && dynamicFOV.isVisionMaskSuppressed;
            }
            set
            {
                if (dynamicFOV == null) dynamicFOV = GetComponentInChildren<DynamicFOV>(true);
                if (dynamicFOV != null) dynamicFOV.isVisionMaskSuppressed = value;
            }
        }

        private void Awake()
        {
            dynamicFOV = GetComponent<DynamicFOV>();
            if (dynamicFOV == null) dynamicFOV = GetComponentInChildren<DynamicFOV>();
            if (dynamicFOV == null) dynamicFOV = GetComponentInParent<DynamicFOV>();
        }

        public void UpdateWeaponVisionProfile(float targetAngle, float targetDistance, float transitionDuration = 0.2f)
        {
            if (dynamicFOV == null) Awake();
            if (dynamicFOV != null)
            {
                dynamicFOV.UpdateWeaponVisionProfile(targetAngle, targetDistance, transitionDuration);
            }
        }

        public void ClearExploredMemoryGrid()
        {
            if (dynamicFOV == null) Awake();
            if (dynamicFOV != null)
            {
                dynamicFOV.ClearExploredMemoryGrid();
            }
        }

        public float ViewAngle => (dynamicFOV != null) ? dynamicFOV.viewAngle : 90f;
        public float ViewRadius => (dynamicFOV != null) ? dynamicFOV.viewRadius : 15f;
    }
}
