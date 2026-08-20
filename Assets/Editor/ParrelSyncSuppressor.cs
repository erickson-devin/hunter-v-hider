using UnityEditor;
using UnityEngine;

namespace HunterVsHider.EditorScripts
{
    /// <summary>
    /// Ensures that ParrelSync clone instances never execute asset saving,
    /// scene saving, or disk writing operations that trigger dialog popups.
    /// </summary>
    [InitializeOnLoad]
    public class ParrelSyncSuppressor
    {
        static ParrelSyncSuppressor()
        {
            ApplyParrelSyncPreferences();
        }

        [MenuItem("Tools/Hunter v Hider/Configure ParrelSync Preferences")]
        public static void ApplyParrelSyncPreferences()
        {
            bool isClone = ParrelSync.ClonesManager.IsClone();

            if (isClone)
            {
                // In a clone instance, strictly suppress any auto-saving
                EditorPrefs.SetBool("ParrelSync_DisableAssetSavingInClones", true);
                Debug.Log("[ParrelSyncSuppressor] Running in ParrelSync CLONE instance -> Asset & scene saving strictly suppressed.");
            }
            else
            {
                // In the main project, ensure preference is saved
                EditorPrefs.SetBool("ParrelSync_DisableAssetSavingInClones", true);
                Debug.Log("[ParrelSyncSuppressor] Running in MAIN editor instance -> Configured ParrelSync clone saving suppression.");
            }
        }
    }
}
