using UnityEngine;
using HunterVsHider.Vision;

namespace HunterVsHider.Player
{
    /// <summary>
    /// FOVMeshRenderer extends FieldOfView to provide full backwards compatibility
    /// with existing scenes and prefabs while using the decoupled 2.5D VisionController architecture.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FOVMeshRenderer : FieldOfView
    {
    }
}
