using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;

namespace HunterVsHider.Managers
{
    /// <summary>
    /// Manages Unity Relay allocation, authentication, and matchmaking join codes.
    /// Standard MonoBehaviour Singleton (not a NetworkBehaviour).
    /// </summary>
    public class RelayManager : MonoBehaviour
    {
        public static RelayManager Instance { get; private set; }

        [Header("Relay Settings")]
        [Tooltip("Max connected players allowed for Relay allocations.")]
        [SerializeField] private int defaultMaxPlayers = 10;

        [Header("Connection Protocol")]
        [Tooltip("Protocol type for Relay (dtls is recommended for secure UDP).")]
        [SerializeField] private string connectionType = "dtls";

        [Header("Runtime Status (Read-Only)")]
        [SerializeField] private bool isAuthenticated = false;
        [SerializeField] private string currentJoinCode = string.Empty;

        public bool IsAuthenticated => isAuthenticated;
        public string CurrentJoinCode => currentJoinCode;

        // Events for UI bindings
        public event Action<string> OnJoinCodeGenerated;
        public event Action<string> OnStatusMessageChanged;
        public event Action<bool> OnAuthenticationChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private async void Start()
        {
            await InitializeRelayServicesAsync();
        }

        /// <summary>
        /// Initializes Unity Services and signs in anonymously.
        /// </summary>
        public async Task<bool> InitializeRelayServicesAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    OnStatusMessageChanged?.Invoke("Initializing Unity Services...");
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    OnStatusMessageChanged?.Invoke("Signing in anonymously...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                isAuthenticated = AuthenticationService.Instance.IsSignedIn;
                OnAuthenticationChanged?.Invoke(isAuthenticated);

                Debug.Log($"[RelayManager] Successfully signed in anonymously. Player ID: {AuthenticationService.Instance.PlayerId}");
                OnStatusMessageChanged?.Invoke($"Signed in (ID: {AuthenticationService.Instance.PlayerId.Substring(0, Mathf.Min(8, AuthenticationService.Instance.PlayerId.Length))}...)");
                return true;
            }
            catch (Exception ex)
            {
                isAuthenticated = false;
                OnAuthenticationChanged?.Invoke(false);
                Debug.LogError($"[RelayManager] Initialization / Authentication failed: {ex.Message}\n{ex.StackTrace}");
                OnStatusMessageChanged?.Invoke($"Auth Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a Relay Host allocation, generates a 6-character Join Code,
        /// configures the UnityTransport component, and starts the Netcode Host.
        /// </summary>
        /// <param name="maxPlayers">Maximum number of clients + host (default: 10)</param>
        /// <returns>Generated Join Code string, or null on failure</returns>
        public async Task<string> CreateRelayHost(int maxPlayers = 10)
        {
            if (maxPlayers <= 0) maxPlayers = defaultMaxPlayers;

            try
            {
                if (!isAuthenticated || !AuthenticationService.Instance.IsSignedIn)
                {
                    bool authOk = await InitializeRelayServicesAsync();
                    if (!authOk)
                    {
                        Debug.LogError("[RelayManager] Cannot create host: Authentication failed.");
                        return null;
                    }
                }

                OnStatusMessageChanged?.Invoke("Allocating Relay server for Host...");
                Debug.Log($"[RelayManager] Requesting Relay allocation for {maxPlayers} players...");

                // 1. Request allocation
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);

                // 2. Generate Join Code from allocation ID
                currentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log($"[RelayManager] Host Allocation created successfully! Join Code: {currentJoinCode}");

                // 3. Extract UnityTransport from NetworkManager
                if (NetworkManager.Singleton == null)
                {
                    Debug.LogError("[RelayManager] NetworkManager.Singleton is null! Make sure NetworkManager exists in the scene.");
                    OnStatusMessageChanged?.Invoke("Error: NetworkManager not found!");
                    return null;
                }

                UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    Debug.LogError("[RelayManager] UnityTransport component not found on NetworkManager!");
                    OnStatusMessageChanged?.Invoke("Error: UnityTransport not found!");
                    return null;
                }

                // 4. Configure Relay server data on transport
                bool isSecure = connectionType.Equals("dtls", StringComparison.OrdinalIgnoreCase);
                transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData,
                    isSecure
                );

                // 5. Start Host
                bool started = NetworkManager.Singleton.StartHost();
                if (started)
                {
                    Debug.Log($"[RelayManager] Netcode Host started with Relay Join Code: {currentJoinCode}");
                    OnJoinCodeGenerated?.Invoke(currentJoinCode);
                    OnStatusMessageChanged?.Invoke($"Host Running! Code: {currentJoinCode}");
                    return currentJoinCode;
                }
                else
                {
                    Debug.LogError("[RelayManager] NetworkManager.Singleton.StartHost() returned false!");
                    OnStatusMessageChanged?.Invoke("Failed to start Netcode Host.");
                    return null;
                }
            }
            catch (RelayServiceException relayEx)
            {
                Debug.LogError($"[RelayManager] RelayServiceException in CreateRelayHost: {relayEx.Message} (Error code: {relayEx.ErrorCode})");
                OnStatusMessageChanged?.Invoke($"Relay Error: {relayEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelayManager] Exception in CreateRelayHost: {ex.Message}\n{ex.StackTrace}");
                OnStatusMessageChanged?.Invoke($"Host Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Joins an existing Relay Host allocation using the provided Join Code,
        /// configures the UnityTransport component, and starts the Netcode Client.
        /// </summary>
        /// <param name="joinCode">6-character alphanumeric join code</param>
        /// <returns>True if join succeeded and client started, false otherwise</returns>
        public async Task<bool> JoinRelayClient(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                Debug.LogError("[RelayManager] Join Code cannot be null or whitespace.");
                OnStatusMessageChanged?.Invoke("Please enter a valid Join Code.");
                return false;
            }

            joinCode = joinCode.Trim().ToUpper();

            try
            {
                if (!isAuthenticated || !AuthenticationService.Instance.IsSignedIn)
                {
                    bool authOk = await InitializeRelayServicesAsync();
                    if (!authOk)
                    {
                        Debug.LogError("[RelayManager] Cannot join client: Authentication failed.");
                        return false;
                    }
                }

                OnStatusMessageChanged?.Invoke($"Joining Relay with code {joinCode}...");
                Debug.Log($"[RelayManager] Requesting JoinAllocation with code: {joinCode}");

                // 1. Request Join Allocation using Join Code
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                Debug.Log($"[RelayManager] JoinAllocation succeeded! AllocationId: {joinAllocation.AllocationId}");

                // 2. Extract UnityTransport from NetworkManager
                if (NetworkManager.Singleton == null)
                {
                    Debug.LogError("[RelayManager] NetworkManager.Singleton is null! Make sure NetworkManager exists in the scene.");
                    OnStatusMessageChanged?.Invoke("Error: NetworkManager not found!");
                    return false;
                }

                UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    Debug.LogError("[RelayManager] UnityTransport component not found on NetworkManager!");
                    OnStatusMessageChanged?.Invoke("Error: UnityTransport not found!");
                    return false;
                }

                // 3. Configure Relay server data on transport
                bool isSecure = connectionType.Equals("dtls", StringComparison.OrdinalIgnoreCase);
                transport.SetClientRelayData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData,
                    isSecure
                );

                // 4. Start Client
                bool started = NetworkManager.Singleton.StartClient();
                if (started)
                {
                    currentJoinCode = joinCode;
                    Debug.Log($"[RelayManager] Netcode Client started successfully connecting to Relay code: {joinCode}");
                    OnStatusMessageChanged?.Invoke($"Connecting to Host ({joinCode})...");
                    return true;
                }
                else
                {
                    Debug.LogError("[RelayManager] NetworkManager.Singleton.StartClient() returned false!");
                    OnStatusMessageChanged?.Invoke("Failed to start Netcode Client.");
                    return false;
                }
            }
            catch (RelayServiceException relayEx)
            {
                Debug.LogError($"[RelayManager] RelayServiceException in JoinRelayClient: {relayEx.Message} (Error code: {relayEx.ErrorCode})");
                OnStatusMessageChanged?.Invoke($"Invalid Code or Relay Error: {relayEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelayManager] Exception in JoinRelayClient: {ex.Message}\n{ex.StackTrace}");
                OnStatusMessageChanged?.Invoke($"Join Error: {ex.Message}");
                return false;
            }
        }
    }
}
