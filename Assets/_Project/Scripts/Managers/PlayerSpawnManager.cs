using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HunterVsHider.Managers
{
    public class PlayerSpawnManager : MonoBehaviour
    {
        public static PlayerSpawnManager Instance { get; private set; }

        [Header("Zone Spawning")]
        [Tooltip("Transform representing Zone_Lobby.")]
        public Transform zoneLobby;

        [Tooltip("Fallback lobby spawn position if Zone_Lobby Transform is unassigned.")]
        public Vector3 fallbackLobbyPosition = new Vector3(1000f, 0f, 0f);

        private readonly HashSet<ulong> spawnedClients = new HashSet<ulong>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            FindLobbyZoneIfNull();
            SnapCameraToLobby();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }
        }

        public void SnapCameraToLobby()
        {
            FindLobbyZoneIfNull();
            Vector3 lobbyBase = zoneLobby != null ? zoneLobby.position : fallbackLobbyPosition;
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<HunterVsHider.Cameras.CameraFollow>();
                Vector3 offset = follow != null ? follow.offset : new Vector3(0f, 18f, -10.4f);
                float pitch = follow != null ? follow.pitch : 60f;

                cam.transform.position = lobbyBase + offset;
                cam.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
                Debug.Log($"[PlayerSpawnManager] Snapped camera to Zone_Lobby view at {cam.transform.position}");
            }
        }

        private void FindLobbyZoneIfNull()
        {
            if (zoneLobby == null)
            {
                var obj = GameObject.Find("Zone_Lobby");
                if (obj != null) zoneLobby = obj.transform;
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            // If local client connected, ensure camera is looking at lobby
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                SnapCameraToLobby();
            }

            if (!NetworkManager.Singleton.IsServer) return;

            // Wait a frame or position immediately when player object is available
            StartCoroutine(PositionPlayerCoroutine(clientId));
        }

        private System.Collections.IEnumerator PositionPlayerCoroutine(ulong clientId)
        {
            // Wait for player NetworkObject to be instantiated
            yield return null;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) yield break;

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
                client.PlayerObject != null)
            {
                Vector3 spawnPos = GetLobbySpawnPosition(clientId);
                
                var playerState = client.PlayerObject.GetComponent<Player.PlayerNetworkState>();
                if (playerState != null)
                {
                    playerState.ServerTeleport(spawnPos, Quaternion.identity);
                }
                else
                {
                    client.PlayerObject.transform.position = spawnPos;
                    var rb = client.PlayerObject.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.position = spawnPos;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }

                Debug.Log($"[PlayerSpawnManager] Positioned player ClientId {clientId} at Zone_Lobby: {spawnPos}");
            }
        }

        public Vector3 GetLobbySpawnPosition(ulong clientId)
        {
            FindLobbyZoneIfNull();
            Vector3 lobbyBase = zoneLobby != null ? zoneLobby.position : fallbackLobbyPosition;
            
            float offsetX = (clientId % 4) * 2.5f - 3.75f;
            float offsetZ = (clientId / 4) * 2.5f;
            return lobbyBase + new Vector3(offsetX, 1f, offsetZ);
        }
    }
}
