using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HunterVsHider.Managers;
using HunterVsHider.Vision;

[InitializeOnLoad]
public class SetupTaskMatchManager
{
    static SetupTaskMatchManager()
    {
        EditorApplication.delayCall += ExecuteSetup;
    }

    [MenuItem("Tools/Hunter v Hider/Setup Match State and Zones")]
    public static void ExecuteSetup()
    {
        if (Application.isPlaying) return;

        Debug.Log("[SetupTaskMatchManager] Starting Match State & Physical Zones Setup...");
        bool anyChanged = false;

        string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        if (!activeScene.IsValid())
        {
            Debug.LogError($"[SetupTaskMatchManager] Could not open scene {scenePath}!");
            return;
        }

        // Material for lobby / prep floors
        Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_DarkGray.mat");
        if (floorMat == null)
        {
            floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Mat_DarkGray.mat");
        }

        // 1. Setup Zone_Lobby (Position: X=1000, Y=0, Z=0)
        GameObject zoneLobby = GameObject.Find("Zone_Lobby");
        if (zoneLobby == null)
        {
            zoneLobby = new GameObject("Zone_Lobby");
            anyChanged = true;
        }
        zoneLobby.transform.position = new Vector3(1000f, 0f, 0f);
        zoneLobby.transform.rotation = Quaternion.identity;
        zoneLobby.transform.localScale = Vector3.one;

        // Ensure floor platform for Zone_Lobby
        Transform lobbyFloor = zoneLobby.transform.Find("Floor_Lobby");
        if (lobbyFloor == null)
        {
            GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObj.name = "Floor_Lobby";
            floorObj.transform.SetParent(zoneLobby.transform, false);
            floorObj.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floorObj.transform.localScale = new Vector3(40f, 1f, 40f);
            if (floorMat != null)
            {
                floorObj.GetComponent<Renderer>().sharedMaterial = floorMat;
            }
            anyChanged = true;
            Debug.Log("[SetupTaskMatchManager] Created floor platform for Zone_Lobby.");
        }

        // Ensure light for Zone_Lobby
        Transform lobbyLight = zoneLobby.transform.Find("Light_Lobby");
        if (lobbyLight == null)
        {
            GameObject lightObj = new GameObject("Light_Lobby");
            lightObj.transform.SetParent(zoneLobby.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 15f, 0f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 60f;
            l.intensity = 2f;
            l.color = new Color(0.9f, 0.95f, 1.0f);
            anyChanged = true;
        }

        // 2. Setup Zone_PolicePrep (Position: X=2000, Y=0, Z=0)
        GameObject zonePolicePrep = GameObject.Find("Zone_PolicePrep");
        if (zonePolicePrep == null)
        {
            zonePolicePrep = new GameObject("Zone_PolicePrep");
            anyChanged = true;
        }
        zonePolicePrep.transform.position = new Vector3(2000f, 0f, 0f);
        zonePolicePrep.transform.rotation = Quaternion.identity;
        zonePolicePrep.transform.localScale = Vector3.one;

        // Ensure floor platform for Zone_PolicePrep
        Transform prepFloor = zonePolicePrep.transform.Find("Floor_PolicePrep");
        if (prepFloor == null)
        {
            GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObj.name = "Floor_PolicePrep";
            floorObj.transform.SetParent(zonePolicePrep.transform, false);
            floorObj.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floorObj.transform.localScale = new Vector3(40f, 1f, 40f);
            if (floorMat != null)
            {
                floorObj.GetComponent<Renderer>().sharedMaterial = floorMat;
            }
            anyChanged = true;
            Debug.Log("[SetupTaskMatchManager] Created floor platform for Zone_PolicePrep.");
        }

        // Ensure light for Zone_PolicePrep
        Transform prepLight = zonePolicePrep.transform.Find("Light_PolicePrep");
        if (prepLight == null)
        {
            GameObject lightObj = new GameObject("Light_PolicePrep");
            lightObj.transform.SetParent(zonePolicePrep.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 15f, 0f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 60f;
            l.intensity = 2f;
            l.color = new Color(0.85f, 0.9f, 1.0f);
            anyChanged = true;
        }

        // 3. Setup Zone_CombatArena (Position: X=0, Y=0, Z=0)
        GameObject zoneCombatArena = GameObject.Find("Zone_CombatArena");
        if (zoneCombatArena == null)
        {
            zoneCombatArena = new GameObject("Zone_CombatArena");
            anyChanged = true;
        }
        zoneCombatArena.transform.position = Vector3.zero;
        zoneCombatArena.transform.rotation = Quaternion.identity;
        zoneCombatArena.transform.localScale = Vector3.one;

        // 4. Setup MatchManager GameObject
        GameObject matchManagerObj = GameObject.Find("MatchManager");
        if (matchManagerObj == null)
        {
            matchManagerObj = new GameObject("MatchManager");
            anyChanged = true;
            Debug.Log("[SetupTaskMatchManager] Created MatchManager GameObject in Tactical_Main.");
        }

        if (matchManagerObj.GetComponent<NetworkObject>() == null)
        {
            matchManagerObj.AddComponent<NetworkObject>();
            anyChanged = true;
        }

        MatchManager matchMgr = matchManagerObj.GetComponent<MatchManager>();
        if (matchMgr == null)
        {
            matchMgr = matchManagerObj.AddComponent<MatchManager>();
            anyChanged = true;
        }

        matchMgr.zoneLobby = zoneLobby.transform;
        matchMgr.zonePolicePrep = zonePolicePrep.transform;
        matchMgr.zoneCombatArena = zoneCombatArena.transform;
        EditorUtility.SetDirty(matchMgr);

        // 5. Configure PlayerSpawnManager on NetworkManager
        GameObject netManagerObj = GameObject.Find("NetworkManager");
        if (netManagerObj != null)
        {
            PlayerSpawnManager spawnMgr = netManagerObj.GetComponent<PlayerSpawnManager>();
            if (spawnMgr != null)
            {
                spawnMgr.zoneLobby = zoneLobby.transform;
                EditorUtility.SetDirty(spawnMgr);
                anyChanged = true;
            }
        }

        // 6. Configure VisionCamera for 250m x 250m Combat Arena
        GameObject visionCamObj = GameObject.Find("VisionCamera");
        if (visionCamObj != null)
        {
            visionCamObj.transform.position = new Vector3(0f, 150f, 0f);
            Camera visionCam = visionCamObj.GetComponent<Camera>();
            if (visionCam != null)
            {
                visionCam.orthographic = true;
                visionCam.orthographicSize = 125f; // 250m x 250m arena
                visionCam.farClipPlane = 300f;
                EditorUtility.SetDirty(visionCam);
                anyChanged = true;
            }
        }

        // 7. Configure FoW_PostProcessFeature on Main Camera for 250m x 250m
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            FoW_PostProcessFeature fowPost = mainCam.GetComponent<FoW_PostProcessFeature>();
            if (fowPost != null)
            {
                fowPost.mapBoundsMin = new Vector2(-125f, -125f);
                fowPost.mapBoundsSize = new Vector2(250f, 250f);
                EditorUtility.SetDirty(fowPost);
                anyChanged = true;
            }
        }

        if (anyChanged)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupTaskMatchManager] Setup completed & Tactical_Main scene saved successfully.");
        }

        EditorApplication.delayCall -= ExecuteSetup;
    }
}
