using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using HunterVsHider.Managers;

namespace HunterVsHider.EditorScripts
{
    public class AuditSceneUI
    {
        [MenuItem("Tools/Hunter v Hider/Audit Scene UI & Cleanup Legacy")]
        public static void ExecuteAudit()
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
                Debug.LogError($"[AuditSceneUI] Could not open scene {scenePath}!");
                return;
            }

            bool sceneDirty = false;
            Debug.Log("[AuditSceneUI] Scanning all GameObjects in Tactical_Main...");

            var rootObjects = activeScene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                CheckAndCleanHierarchy(root, ref sceneDirty);
            }

            // Check MatchManager
            MatchManager mm = Object.FindAnyObjectByType<MatchManager>();
            if (mm != null)
            {
                Debug.Log("[AuditSceneUI] MatchManager verified (Legacy OnGUI completely removed).");
            }

            // Check NetworkManager
            NetworkManager nm = Object.FindAnyObjectByType<NetworkManager>();
            if (nm != null)
            {
                // Check if any HUD or legacy component attached
                Component[] comps = nm.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c == null)
                    {
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(nm.gameObject);
                        sceneDirty = true;
                        Debug.Log("[AuditSceneUI] Removed Missing Mono Script on NetworkManager.");
                        continue;
                    }
                    string cType = c.GetType().Name;
                    if (cType.Contains("HUD") || cType.Contains("NetworkManagerHUD") || cType.Contains("DebugUI"))
                    {
                        Object.DestroyImmediate(c, true);
                        sceneDirty = true;
                        Debug.Log($"[AuditSceneUI] Removed legacy component '{cType}' from NetworkManager.");
                    }
                }
            }

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log("[AuditSceneUI] Scene cleaned and saved successfully!");
            }
            else
            {
                Debug.Log("[AuditSceneUI] Scene is already clean. No changes needed.");
            }

            EditorApplication.delayCall -= ExecuteAudit;
        }

        private static void CheckAndCleanHierarchy(GameObject obj, ref bool dirty)
        {
            if (obj == null) return;

            // Remove any missing script components
            int missingRemoved = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
            if (missingRemoved > 0)
            {
                dirty = true;
                Debug.Log($"[AuditSceneUI] Removed {missingRemoved} missing scripts on {obj.name}");
            }

            string nameLower = obj.name.ToLower();
            if (nameLower.Contains("networkmanagerhud") || 
                nameLower.Contains("debug_network") || 
                nameLower.Contains("legacy_matchcontroller") ||
                nameLower.Contains("matchcontrollerpanel") ||
                nameLower.Contains("matchcontroller_hud"))
            {
                Debug.Log($"[AuditSceneUI] Deleting obsolete legacy GameObject: {obj.name}");
                Object.DestroyImmediate(obj);
                dirty = true;
                return;
            }

            // Recursively inspect children
            for (int i = obj.transform.childCount - 1; i >= 0; i--)
            {
                CheckAndCleanHierarchy(obj.transform.GetChild(i).gameObject, ref dirty);
            }
        }
    }
}
