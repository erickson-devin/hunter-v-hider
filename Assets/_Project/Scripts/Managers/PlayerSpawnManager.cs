using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HunterVsHider.Managers
{
    public class PlayerSpawnManager : MonoBehaviour
    {
        public static PlayerSpawnManager Instance { get; private set; }

        [Header("Default Spawn Positions")]
        [Tooltip("Safe spawn locations away from walls and central obstacles.")]
        [SerializeField]
        private Vector3[] defaultSpawnPoints = new Vector3[]
        {
            new Vector3(-4f, 1f, -18f), // South / Spawn 0 (Host / Police)
            new Vector3(4f, 1f, -18f),  // South / Spawn 1 (Client 1)
            new Vector3(0f, 1f, 18f),   // North / Spawn 2 (Assassin)
            new Vector3(-6f, 1f, 18f),  // North / Spawn 3
            new Vector3(6f, 1f, 18f)    // North / Spawn 4
        };

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

        private void HandleClientConnected(ulong clientId)
        {
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
                Vector3 spawnPos = GetSpawnPositionForClient(clientId);
                client.PlayerObject.transform.position = spawnPos;

                // If player has a Rigidbody, update its position cleanly
                var rb = client.PlayerObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = spawnPos;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                Debug.Log($"[PlayerSpawnManager] Positioned player ClientId {clientId} at safe spawn: {spawnPos}");
            }
        }

        public Vector3 GetSpawnPositionForClient(ulong clientId)
        {
            int index = (int)(clientId % (ulong)defaultSpawnPoints.Length);
            return defaultSpawnPoints[index];
        }
    }
}
