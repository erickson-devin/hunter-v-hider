using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class SetupTask15
{
    static SetupTask15()
    {
        EditorApplication.delayCall += ExecuteSetup;
    }

    private static void ExecuteSetup()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "Tactical_Main")
        {
            return;
        }

        bool changed = false;
        
        GameObject player = GameObject.Find("Player") ?? GameObject.FindGameObjectWithTag("Player");
        Vector3 basePos = player != null ? player.transform.position + player.transform.forward * 10f : new Vector3(0, 1f, 10f);
        basePos.y = 1f;

        if (GameObject.Find("TargetDummy_01") == null)
        {
            GameObject dummy1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy1.name = "TargetDummy_01";
            dummy1.transform.position = basePos + Vector3.left * 2f;
            
            if (dummy1.GetComponent<TargetDummy>() == null)
                dummy1.AddComponent<TargetDummy>();
                
            EditorUtility.SetDirty(dummy1);
            changed = true;
            Debug.Log("Created TargetDummy_01");
        }

        if (GameObject.Find("TargetDummy_02") == null)
        {
            GameObject dummy2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy2.name = "TargetDummy_02";
            dummy2.transform.position = basePos + Vector3.right * 2f;
            
            if (dummy2.GetComponent<TargetDummy>() == null)
                dummy2.AddComponent<TargetDummy>();

            EditorUtility.SetDirty(dummy2);
            changed = true;
            Debug.Log("Created TargetDummy_02");
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("Saved Tactical_Main scene with target dummies.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
