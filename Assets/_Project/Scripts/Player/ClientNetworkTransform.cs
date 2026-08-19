using Unity.Netcode.Components;
using UnityEngine;

namespace HunterVsHider.Player
{
    /// <summary>
    /// Client/Owner-authoritative NetworkTransform override for fast-paced responsive tactical movement.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
