using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using HunterVsHider.Managers;

namespace HunterVsHider.EditorScripts
{
    public class RemoveLegacyNetworkHUD
    {
        [MenuItem("Tools/Hunter v Hider/Remove Legacy Network HUD")]
        public static void ExecuteRemove()
        {
            if (ParrelSync.ClonesManager.IsClone()) return;
            if (Application.isPlaying) return;

            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[RemoveLegacyNetworkHUD] Could not open scene {scenePath}!");
                return;
            }

            bool sceneDirty = false;

            // Find NetworkManager in scene
            NetworkManager nm = Object.FindAnyObjectByType<NetworkManager>();
            if (nm != null)
            {
                NetworkHUD hud = nm.GetComponent<NetworkHUD>();
                if (hud != null)
                {
                    Object.DestroyImmediate(hud, true);
                    sceneDirty = true;
                    Debug.Log("[RemoveLegacyNetworkHUD] Successfully removed NetworkHUD component from NetworkManager.");
                }

                // Also check for any generic NetworkManagerHUD or missing scripts
                int missingCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(nm.gameObject);
                if (missingCount > 0)
                {
                    sceneDirty = true;
                    Debug.Log($"[RemoveLegacyNetworkHUD] Removed {missingCount} missing scripts from NetworkManager.");
                }
            }

            // Also search all scene objects for any NetworkHUD component
            var allHUDs = Object.FindObjectsByType<NetworkHUD>(FindObjectsInactive.Include);
            foreach (var h in allHUDs)
            {
                if (h != null)
                {
                    Object.DestroyImmediate(h, true);
                    sceneDirty = true;
                    Debug.Log($"[RemoveLegacyNetworkHUD] Removed NetworkHUD component from {h.gameObject.name}.");
                }
            }

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log("[RemoveLegacyNetworkHUD] Saved Tactical_Main.unity after removing legacy Network HUD.");
            }
            else
            {
                Debug.Log("[RemoveLegacyNetworkHUD] Network HUD is already completely removed.");
            }

            EditorApplication.delayCall -= ExecuteRemove;
        }
    }
}
