using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HunterVsHider.Player;
using HunterVsHider.Managers;

[InitializeOnLoad]
public class SetupTaskNGOFoundation
{
    static SetupTaskNGOFoundation()
    {
        EditorApplication.delayCall += ExecuteSetup;
    }

    [MenuItem("Tools/Hunter v Hider/Setup NGO Foundation")]
    public static void ExecuteSetup()
    {
        if (Application.isPlaying) return;

        Debug.Log("[SetupTaskNGOFoundation] Starting NGO Foundation Setup...");
        bool anyChanged = false;

        // 1. Setup Player Prefab
        string playerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab != null)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(playerPrefabPath);
            bool prefabDirty = false;

            if (prefabRoot.GetComponent<NetworkObject>() == null)
            {
                prefabRoot.AddComponent<NetworkObject>();
                prefabDirty = true;
                Debug.Log("[SetupTaskNGOFoundation] Added NetworkObject to Player.prefab");
            }

            if (prefabRoot.GetComponent<PlayerNetworkState>() == null)
            {
                prefabRoot.AddComponent<PlayerNetworkState>();
                prefabDirty = true;
                Debug.Log("[SetupTaskNGOFoundation] Added PlayerNetworkState to Player.prefab");
            }

            var netTransform = prefabRoot.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (netTransform == null)
            {
                netTransform = prefabRoot.AddComponent<Unity.Netcode.Components.NetworkTransform>();
                prefabDirty = true;
                Debug.Log("[SetupTaskNGOFoundation] Added NetworkTransform to Player.prefab");
            }

            // Configure Owner Authority and optimized sync axes
            if (netTransform.AuthorityMode != Unity.Netcode.Components.NetworkTransform.AuthorityModes.Owner ||
                netTransform.SyncScaleX || netTransform.SyncScaleY || netTransform.SyncScaleZ ||
                netTransform.SyncRotAngleX || !netTransform.SyncRotAngleY || netTransform.SyncRotAngleZ)
            {
                netTransform.AuthorityMode = Unity.Netcode.Components.NetworkTransform.AuthorityModes.Owner;
                netTransform.SyncPositionX = true;
                netTransform.SyncPositionY = true;
                netTransform.SyncPositionZ = true;
                netTransform.SyncRotAngleX = false;
                netTransform.SyncRotAngleY = true;
                netTransform.SyncRotAngleZ = false;
                netTransform.SyncScaleX = false;
                netTransform.SyncScaleY = false;
                netTransform.SyncScaleZ = false;
                netTransform.Interpolate = true;
                prefabDirty = true;
                Debug.Log("[SetupTaskNGOFoundation] Configured NetworkTransform settings on Player.prefab");
            }

            if (prefabDirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, playerPrefabPath);
                anyChanged = true;
                Debug.Log("[SetupTaskNGOFoundation] Saved Player.prefab changes.");
            }
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
        else
        {
            Debug.LogError($"[SetupTaskNGOFoundation] Player prefab not found at {playerPrefabPath}!");
        }

        // 2. Register Player Prefab in DefaultNetworkPrefabs.asset
        string netPrefabsAssetPath = "Assets/DefaultNetworkPrefabs.asset";
        NetworkPrefabsList netPrefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(netPrefabsAssetPath);
        if (netPrefabsList != null && playerPrefab != null)
        {
            if (!netPrefabsList.Contains(playerPrefab))
            {
                NetworkPrefab netPrefab = new NetworkPrefab();
                netPrefab.Prefab = playerPrefab;
                netPrefabsList.Add(netPrefab);
                EditorUtility.SetDirty(netPrefabsList);
                AssetDatabase.SaveAssets();
                anyChanged = true;
                Debug.Log("[SetupTaskNGOFoundation] Registered Player.prefab in DefaultNetworkPrefabs.asset.");
            }
        }

        // 3. Setup Scene Tactical_Main
        string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        if (activeScene.IsValid())
        {
            bool sceneDirty = false;

            GameObject netManagerObj = GameObject.Find("NetworkManager");
            if (netManagerObj == null)
            {
                netManagerObj = new GameObject("NetworkManager");
                sceneDirty = true;
                Debug.Log("[SetupTaskNGOFoundation] Created NetworkManager GameObject in Tactical_Main.");
            }

            UnityTransport transport = netManagerObj.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = netManagerObj.AddComponent<UnityTransport>();
                sceneDirty = true;
                Debug.Log("[SetupTaskNGOFoundation] Added UnityTransport component.");
            }

            NetworkManager netManager = netManagerObj.GetComponent<NetworkManager>();
            if (netManager == null)
            {
                netManager = netManagerObj.AddComponent<NetworkManager>();
                sceneDirty = true;
                Debug.Log("[SetupTaskNGOFoundation] Added NetworkManager component.");
            }

            // Attach NetworkHUD for easy Play Mode controls & role testing
            if (netManagerObj.GetComponent<NetworkHUD>() == null)
            {
                netManagerObj.AddComponent<NetworkHUD>();
                sceneDirty = true;
            }

            // Attach PlayerSpawnManager for safe spawn positions
            if (netManagerObj.GetComponent<PlayerSpawnManager>() == null)
            {
                netManagerObj.AddComponent<PlayerSpawnManager>();
                sceneDirty = true;
            }

            // Configure NetworkManager via SerializedObject to ensure all references serialize cleanly into the scene file
            SerializedObject netManagerSo = new SerializedObject(netManager);
            SerializedProperty networkConfigProp = netManagerSo.FindProperty("NetworkConfig");
            if (networkConfigProp != null)
            {
                SerializedProperty transportProp = networkConfigProp.FindPropertyRelative("NetworkTransport");
                if (transportProp != null && transportProp.objectReferenceValue != transport)
                {
                    transportProp.objectReferenceValue = transport;
                    sceneDirty = true;
                }

                SerializedProperty playerPrefabProp = networkConfigProp.FindPropertyRelative("PlayerPrefab");
                if (playerPrefabProp != null && playerPrefabProp.objectReferenceValue != playerPrefab)
                {
                    playerPrefabProp.objectReferenceValue = playerPrefab;
                    sceneDirty = true;
                }

                SerializedProperty prefabsListProp = networkConfigProp.FindPropertyRelative("Prefabs");
                if (prefabsListProp != null && netPrefabsList != null)
                {
                    SerializedProperty networkPrefabsAssetProp = prefabsListProp.FindPropertyRelative("NetworkPrefabsLists");
                    if (networkPrefabsAssetProp != null)
                    {
                        bool found = false;
                        for (int i = 0; i < networkPrefabsAssetProp.arraySize; i++)
                        {
                            if (networkPrefabsAssetProp.GetArrayElementAtIndex(i).objectReferenceValue == netPrefabsList)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            int newIdx = networkPrefabsAssetProp.arraySize;
                            networkPrefabsAssetProp.InsertArrayElementAtIndex(newIdx);
                            networkPrefabsAssetProp.GetArrayElementAtIndex(newIdx).objectReferenceValue = netPrefabsList;
                            sceneDirty = true;
                        }
                    }
                }

                netManagerSo.ApplyModifiedProperties();
            }

            if (sceneDirty)
            {
                EditorUtility.SetDirty(netManagerObj);
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                anyChanged = true;
                Debug.Log("[SetupTaskNGOFoundation] Saved Tactical_Main scene with NetworkManager configured.");
            }
        }

        if (anyChanged)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupTaskNGOFoundation] Setup completed successfully.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
