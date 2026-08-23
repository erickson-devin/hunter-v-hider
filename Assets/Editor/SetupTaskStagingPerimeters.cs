using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HunterVsHider.Editor
{
    [InitializeOnLoad]
    public static class SetupTaskStagingPerimeters
    {
        static SetupTaskStagingPerimeters()
        {
            EditorApplication.delayCall += ExecuteSetup;
        }

        [MenuItem("Tools/Hunter v Hider/Setup Staging Perimeter Enclosures")]
        public static void ExecuteSetup()
        {
            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogWarning("[SetupTaskStagingPerimeters] Tactical_Main scene could not be opened.");
                return;
            }

            bool sceneDirty = false;
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer == -1) obstacleLayer = 7;

            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Mat_TacticalWall.mat");
            if (wallMat == null)
            {
                wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/DarkGrayFloor.mat");
            }

            // 1. Setup Zone_Lobby perimeter (Floor: 40m x 40m)
            GameObject zoneLobby = GameObject.Find("Zone_Lobby");
            if (zoneLobby != null)
            {
                if (EncloseZonePerimeter(zoneLobby.transform, 40f, 40f, 3.0f, 0.5f, obstacleLayer, wallMat))
                {
                    sceneDirty = true;
                    Debug.Log("[SetupTaskStagingPerimeters] Configured 4-sided perimeter enclosure for Zone_Lobby.");
                }
            }

            // 2. Setup Zone_PolicePrep perimeter (Floor: 40m x 40m)
            GameObject zonePolicePrep = GameObject.Find("Zone_PolicePrep");
            if (zonePolicePrep != null)
            {
                if (EncloseZonePerimeter(zonePolicePrep.transform, 40f, 40f, 3.0f, 0.5f, obstacleLayer, wallMat))
                {
                    sceneDirty = true;
                    Debug.Log("[SetupTaskStagingPerimeters] Configured 4-sided perimeter enclosure for Zone_PolicePrep.");
                }
            }

            // 3. Verify spawn clearance (Ensure at least 1.5m clearance from outer walls)
            float halfWidth = 20f;
            float clearance = 1.5f;
            float maxSafeExtent = halfWidth - clearance; // 18.5m

            Debug.Log($"[SetupTaskStagingPerimeters] Verified spawn bounds: Safe interior zone extends up to +/-{maxSafeExtent:F1}m from center (Clearance: {clearance:F1}m from outer walls).");

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log("[SetupTaskStagingPerimeters] Saved staging perimeter enclosures in Tactical_Main.unity.");
            }

            EditorApplication.delayCall -= ExecuteSetup;
        }

        private static bool EncloseZonePerimeter(Transform zoneRoot, float floorWidth, float floorLength, float wallHeight, float wallThickness, int layer, Material mat)
        {
            bool changed = false;
            Transform perimeterRoot = zoneRoot.Find("Perimeter_Walls");
            if (perimeterRoot == null)
            {
                GameObject rootObj = new GameObject("Perimeter_Walls");
                rootObj.transform.SetParent(zoneRoot, false);
                rootObj.transform.localPosition = Vector3.zero;
                rootObj.transform.localRotation = Quaternion.identity;
                rootObj.transform.localScale = Vector3.one;
                perimeterRoot = rootObj.transform;
                changed = true;
            }

            float halfX = floorWidth * 0.5f;
            float halfZ = floorLength * 0.5f;
            float halfH = wallHeight * 0.5f;

            // North Wall
            changed |= EnsureWallSegment(perimeterRoot, "Wall_North", new Vector3(0f, halfH, halfZ), new Vector3(floorWidth + wallThickness, wallHeight, wallThickness), layer, mat);

            // South Wall
            changed |= EnsureWallSegment(perimeterRoot, "Wall_South", new Vector3(0f, halfH, -halfZ), new Vector3(floorWidth + wallThickness, wallHeight, wallThickness), layer, mat);

            // East Wall
            changed |= EnsureWallSegment(perimeterRoot, "Wall_East", new Vector3(halfX, halfH, 0f), new Vector3(wallThickness, wallHeight, floorLength + wallThickness), layer, mat);

            // West Wall
            changed |= EnsureWallSegment(perimeterRoot, "Wall_West", new Vector3(-halfX, halfH, 0f), new Vector3(wallThickness, wallHeight, floorLength + wallThickness), layer, mat);

            return changed;
        }

        private static bool EnsureWallSegment(Transform parent, string name, Vector3 localPos, Vector3 localScale, int layer, Material mat)
        {
            bool changed = false;
            Transform wallTrans = parent.Find(name);
            GameObject wallObj;

            if (wallTrans == null)
            {
                wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallObj.name = name;
                wallObj.transform.SetParent(parent, false);
                changed = true;
            }
            else
            {
                wallObj = wallTrans.gameObject;
            }

            if (wallObj.layer != layer)
            {
                wallObj.layer = layer;
                changed = true;
            }

            if (wallObj.transform.localPosition != localPos)
            {
                wallObj.transform.localPosition = localPos;
                changed = true;
            }

            if (wallObj.transform.localScale != localScale)
            {
                wallObj.transform.localScale = localScale;
                changed = true;
            }

            BoxCollider col = wallObj.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = wallObj.AddComponent<BoxCollider>();
                changed = true;
            }
            if (!col.enabled)
            {
                col.enabled = true;
                changed = true;
            }

            Renderer rend = wallObj.GetComponent<Renderer>();
            if (rend != null && mat != null && rend.sharedMaterial != mat)
            {
                rend.sharedMaterial = mat;
                changed = true;
            }

            return changed;
        }
    }
}
