using Unity.Netcode;
using UnityEngine;
using HunterVsHider.Player;

namespace HunterVsHider.Managers
{
    public class NetworkHUD : MonoBehaviour
    {
        [Header("Legacy UI Configuration (Disabled)")]
        public bool showGUI = false;
        public int guiOffsetX = 15;
        public int guiOffsetY = 15;

        private void Start()
        {
            // Process command-line arguments for automated testing / standalone instances
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-mode" && i + 1 < args.Length)
                {
                    string mode = args[i + 1].ToLower();
                    if (mode == "host")
                    {
                        NetworkManager.Singleton.StartHost();
                    }
                    else if (mode == "client")
                    {
                        NetworkManager.Singleton.StartClient();
                    }
                    else if (mode == "server")
                    {
                        NetworkManager.Singleton.StartServer();
                    }
                }
            }
        }
    }
}
