using UnityEngine;

namespace HunterVsHider.Cameras
{
    /// <summary>
    /// PlayerCamera controller providing direct access to camera perspectives,
    /// tactical follow mode, and Assassin sky view transitions.
    /// </summary>
    [RequireComponent(typeof(CameraFollow))]
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera Instance { get; private set; }

        private CameraFollow cameraFollow;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            cameraFollow = GetComponent<CameraFollow>();
        }

        /// <summary>
        /// Activates the top-down 90-degree Sky View framing the entire arena grid for the Assassin.
        /// </summary>
        public void ActivateAssassinSkyView()
        {
            if (cameraFollow == null) cameraFollow = GetComponent<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.ActivateAssassinSkyView();
            }
        }

        /// <summary>
        /// Activates the top-down 90-degree Sky View framing the entire arena grid with specified map size.
        /// </summary>
        public void ActivateAssassinSkyView(int mapSize)
        {
            if (cameraFollow == null) cameraFollow = GetComponent<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.ActivateAssassinSkyView(mapSize);
            }
        }

        /// <summary>
        /// Resets the camera back to the standard 60-degree tactical follow view.
        /// </summary>
        public void ResetToTacticalView()
        {
            if (cameraFollow == null) cameraFollow = GetComponent<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.ResetToTacticalView();
            }
        }
    }
}
