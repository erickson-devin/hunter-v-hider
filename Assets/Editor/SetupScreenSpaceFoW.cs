using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HunterVsHider.EditorScripts
{
    public static class SetupScreenSpaceFoW
    {
        [InitializeOnLoadMethod]
        public static void RunSetup()
        {
            if (SessionState.GetBool("SetupScreenSpaceFoWDone", false)) return;
            SessionState.SetBool("SetupScreenSpaceFoWDone", true);

            string scenePath = "Assets/_Project/Scenes/Tactical_Main.unity";
            Scene currentScene = EditorSceneManager.GetActiveScene();
            bool wasAnotherSceneActive = currentScene.path != scenePath;

            Scene targetScene;
            if (wasAnotherSceneActive)
            {
                targetScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            else
            {
                targetScene = currentScene;
            }

            if (targetScene.IsValid())
            {
                bool sceneModified = false;

                // 1. Remove old FoW_GlobalDarkness
                GameObject[] roots = targetScene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root.name == "FoW_GlobalDarkness")
                    {
                        Object.DestroyImmediate(root);
                        sceneModified = true;
                        Debug.Log("Removed old FoW_GlobalDarkness");
                        break; // Destroy Immediate alters the root list, safe because we break
                    }
                }

                // 2. Setup Screen-Space Darkness on Main Camera
                Camera mainCam = null;
                // Try finding by tag first
                GameObject mainCamGO = GameObject.FindGameObjectWithTag("MainCamera");
                if (mainCamGO != null && mainCamGO.scene == targetScene)
                {
                    mainCam = mainCamGO.GetComponent<Camera>();
                }
                else
                {
                    // Fallback to checking root objects
                    foreach (var root in roots)
                    {
                        if (root.name == "Main Camera" || root.name == "MainCamera")
                        {
                            mainCam = root.GetComponent<Camera>();
                            if (mainCam == null) mainCam = root.GetComponentInChildren<Camera>();
                            if (mainCam != null) break;
                        }
                    }
                }

                if (mainCam != null)
                {
                    Transform cameraTransform = mainCam.transform;
                    Transform existingScreenDarkness = cameraTransform.Find("FoW_ScreenDarkness");
                    GameObject screenDarkness;

                    if (existingScreenDarkness == null)
                    {
                        screenDarkness = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        screenDarkness.name = "FoW_ScreenDarkness";
                        screenDarkness.transform.SetParent(cameraTransform);
                        sceneModified = true;
                        Debug.Log("Created FoW_ScreenDarkness child on Main Camera");
                    }
                    else
                    {
                        screenDarkness = existingScreenDarkness.gameObject;
                    }

                    // Transform
                    Transform dt = screenDarkness.transform;
                    if (dt.localPosition != new Vector3(0, 0, 2)) { dt.localPosition = new Vector3(0, 0, 2); sceneModified = true; }
                    if (dt.localEulerAngles != Vector3.zero) { dt.localEulerAngles = Vector3.zero; sceneModified = true; }
                    if (dt.localScale != new Vector3(50, 50, 1)) { dt.localScale = new Vector3(50, 50, 1); sceneModified = true; }

                    // Collider
                    var collider = screenDarkness.GetComponent<MeshCollider>();
                    if (collider != null)
                    {
                        Object.DestroyImmediate(collider);
                        sceneModified = true;
                    }

                    // Material
                    string shadowMatPath = "Assets/_Project/Materials/Mat_FoWShadow.mat";
                    Material shadowMat = AssetDatabase.LoadAssetAtPath<Material>(shadowMatPath);
                    if (shadowMat != null)
                    {
                        var mr = screenDarkness.GetComponent<MeshRenderer>();
                        if (mr != null && mr.sharedMaterial != shadowMat)
                        {
                            mr.sharedMaterial = shadowMat;
                            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            mr.receiveShadows = false;
                            sceneModified = true;
                        }
                    }
                    else
                    {
                        Debug.LogError("Could not find Mat_FoWShadow.mat to assign to Screen Darkness!");
                    }
                }
                else
                {
                    Debug.LogError("Could not find Main Camera in Tactical_Main scene!");
                }

                if (sceneModified)
                {
                    EditorSceneManager.MarkSceneDirty(targetScene);
                    EditorSceneManager.SaveScene(targetScene);
                    Debug.Log("Saved Tactical_Main.unity with Screen-Space FoW modifications.");
                }
            }

            if (wasAnotherSceneActive)
            {
                EditorSceneManager.CloseScene(targetScene, true);
            }
            
            AssetDatabase.SaveAssets();
        }
    }
}
