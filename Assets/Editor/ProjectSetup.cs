using UnityEngine;
using UnityEditor;

public class ProjectSetup : EditorWindow
{
    [MenuItem("Tools/Setup Physics Matrix")]
    public static void SetupPhysics()
    {
        // Physics collision: Player (6) vs Projectile (10)
        int playerLayer = 6; // Player
        int projectileLayer = 10; // Projectile
        
        Physics.IgnoreLayerCollision(playerLayer, projectileLayer, true);
        Debug.Log("Physics collision matrix updated: Player and Projectile will ignore each other.");

        // We can't directly save TimeManager without proper serialization if it's Unity 6 format,
        // but we can set Time.fixedDeltaTime in playmode or leave it to standard ProjectSettings.
        // Let's modify TimeManager via SerializedObject safely
        SerializedObject timeManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
        SerializedProperty mRate = timeManager.FindProperty("Fixed Timestep.m_Rate");
        if (mRate != null)
        {
            mRate.FindPropertyRelative("m_Numerator").intValue = 60;
            mRate.FindPropertyRelative("m_Denominator").intValue = 1;
            timeManager.ApplyModifiedProperties();
            Debug.Log("Set Fixed Timestep to 60 FPS in Unity 6+ format.");
        }
        else
        {
            SerializedProperty fixedTime = timeManager.FindProperty("Fixed Timestep");
            if (fixedTime != null)
            {
                fixedTime.floatValue = 0.016667f;
                timeManager.ApplyModifiedProperties();
                Debug.Log("Set Fixed Timestep to 0.016667.");
            }
        }
    }
}
