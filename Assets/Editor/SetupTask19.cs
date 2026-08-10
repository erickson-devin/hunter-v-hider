using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class SetupTask19
{
    static SetupTask19()
    {
        EditorApplication.delayCall += ExecuteSetup;
    }

    private static void ExecuteSetup()
    {
        if (Application.isPlaying) return;

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "Tactical_Main")
        {
            return;
        }

        // 1. Create Layer
        CreateLayer("Obstacle");

        // 2. Create _TacticalMap
        GameObject mapRoot = GameObject.Find("_TacticalMap");
        if (mapRoot == null)
        {
            mapRoot = new GameObject("_TacticalMap");
            mapRoot.transform.position = Vector3.zero;
        }
        else
        {
            // Clear existing map to avoid duplicates on re-run
            while (mapRoot.transform.childCount > 0)
            {
                Object.DestroyImmediate(mapRoot.transform.GetChild(0).gameObject);
            }
        }

        // Material for walls
        Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Wall_Slate.mat");

        // 3. Build Perimeter Walls
        // X: -25 to 25, Z: -25 to 25
        CreateWall(mapRoot, "Wall_North", new Vector3(0, 1.5f, 25f), new Vector3(50, 3, 0.5f), wallMat);
        CreateWall(mapRoot, "Wall_South", new Vector3(0, 1.5f, -25f), new Vector3(50, 3, 0.5f), wallMat);
        CreateWall(mapRoot, "Wall_East", new Vector3(25f, 1.5f, 0), new Vector3(0.5f, 3, 50), wallMat);
        CreateWall(mapRoot, "Wall_West", new Vector3(-25f, 1.5f, 0), new Vector3(0.5f, 3, 50), wallMat);

        // 4. Build Interior Partitions
        // Central Hub (10x10) - Needs 2.5m doorways (gap)
        float pieceLen = 3.75f;
        float pieceCenter = 3.125f;

        // North wall of central hub (Z = 5)
        CreateWall(mapRoot, "Central_N_Left", new Vector3(-pieceCenter, 1.5f, 5f), new Vector3(pieceLen, 3, 0.5f), wallMat);
        CreateWall(mapRoot, "Central_N_Right", new Vector3(pieceCenter, 1.5f, 5f), new Vector3(pieceLen, 3, 0.5f), wallMat);

        // South wall of central hub (Z = -5)
        CreateWall(mapRoot, "Central_S_Left", new Vector3(-pieceCenter, 1.5f, -5f), new Vector3(pieceLen, 3, 0.5f), wallMat);
        CreateWall(mapRoot, "Central_S_Right", new Vector3(pieceCenter, 1.5f, -5f), new Vector3(pieceLen, 3, 0.5f), wallMat);

        // West wall of central hub (X = -5)
        CreateWall(mapRoot, "Central_W_Top", new Vector3(-5f, 1.5f, pieceCenter), new Vector3(0.5f, 3, pieceLen), wallMat);
        CreateWall(mapRoot, "Central_W_Bot", new Vector3(-5f, 1.5f, -pieceCenter), new Vector3(0.5f, 3, pieceLen), wallMat);

        // East wall of central hub (X = 5)
        CreateWall(mapRoot, "Central_E_Top", new Vector3(5f, 1.5f, pieceCenter), new Vector3(0.5f, 3, pieceLen), wallMat);
        CreateWall(mapRoot, "Central_E_Bot", new Vector3(5f, 1.5f, -pieceCenter), new Vector3(0.5f, 3, pieceLen), wallMat);

        // Additional partition to create a West Room (X = -15) spanning Z = -15 to 15
        CreateWall(mapRoot, "WestRoom_Wall_Top", new Vector3(-15f, 1.5f, 10f), new Vector3(0.5f, 3, 10f), wallMat);
        CreateWall(mapRoot, "WestRoom_Wall_Bot", new Vector3(-15f, 1.5f, -10f), new Vector3(0.5f, 3, 10f), wallMat);

        // East Room (X = 15) spanning Z = -15 to 15
        CreateWall(mapRoot, "EastRoom_Wall_Top", new Vector3(15f, 1.5f, 10f), new Vector3(0.5f, 3, 10f), wallMat);
        CreateWall(mapRoot, "EastRoom_Wall_Bot", new Vector3(15f, 1.5f, -10f), new Vector3(0.5f, 3, 10f), wallMat);

        // Set Player spawn pos
        GameObject player = GameObject.Find("Player") ?? GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = new Vector3(0, 1f, -20f);
            EditorUtility.SetDirty(player);
        }

        EditorUtility.SetDirty(mapRoot);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("Tactical Map Generated with Layer and Walls! Player Spawn updated.");

        EditorApplication.delayCall -= ExecuteSetup;
    }

    private static void CreateWall(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent.transform, false);
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.layer = LayerMask.NameToLayer("Obstacle");
        
        MeshRenderer renderer = wall.GetComponent<MeshRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private static void CreateLayer(string layerName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        bool layerExists = false;
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
            {
                layerExists = true;
                break;
            }
        }

        if (!layerExists)
        {
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"Created layer: {layerName}");
                    break;
                }
            }
        }
    }
}
