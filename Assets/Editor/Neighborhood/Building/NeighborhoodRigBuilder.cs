using DroneDDW.FingerGrab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if NEIGHBORHOOD_XRI
using Unity.XR.CoreUtils;
#endif

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>
    /// Seats the viewer: a copy of the project's XR rig (the root with an XR Origin in the main scene), with the hands
    /// set up as in the Interaction Tests scenes and grabbing with the finger grab and its shared settings. The camera
    /// clears to the sky (the main scene's clears to transparent black for passthrough). It is a copy: rebuild after
    /// changing the rig there. Ported from the park's rig builder (full VR only).
    /// </summary>
    internal static class NeighborhoodRigBuilder
    {
        #region Fields

        internal const string MainScenePath = "Assets/Scenes/Questionnaire.unity";

        private const string LeftHandVisualName = "LeftHandQuestVisual";
        private const string HandVisualizerName = "Hand Visualizer";

        #endregion

        #region Internal

        /// <summary>
        /// Copies the rig to the given position, facing +Z. Returns its camera, or null when the main scene or its rig is
        /// missing, and hands out the rig's tracking space: the camera offset its hand joints are relative to.
        /// </summary>
        internal static Camera CopyXRRig(Scene scene, Vector3 position, float farClipPlane, out Transform trackingSpace)
        {
            trackingSpace = null;
#if NEIGHBORHOOD_XRI
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
            {
                return null;
            }

            Camera camera = null;
            Scene source = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject rootObject in source.GetRootGameObjects())
                {
                    if (!rootObject.TryGetComponent(out XROrigin sourceOrigin) || sourceOrigin.Camera == null)
                    {
                        continue;
                    }

                    GameObject copy = Object.Instantiate(rootObject);
                    copy.name = rootObject.name;
                    SceneManager.MoveGameObjectToScene(copy, scene);
                    copy.transform.SetPositionAndRotation(position, Quaternion.identity);
                    UseInteractionTestHands(copy.transform);
                    XROrigin origin = copy.GetComponent<XROrigin>();
                    trackingSpace = origin.CameraFloorOffsetObject.transform;
                    camera = origin.Camera;
                    camera.clearFlags = CameraClearFlags.Skybox;
                    camera.farClipPlane = Mathf.Max(camera.farClipPlane, farClipPlane);
                    break;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
            }

            if (camera != null)
            {
                // Thumb plus any finger grabs the slider, tuned by the shared Finger Grab Settings asset
                int fingerGrabHandCount = FingerGrabRigSetup.AddToScene(scene);
                Debug.Log($"[Neighborhood] Finger grab set up on {fingerGrabHandCount} hands.");
            }

            return camera;
#else
            return null;
#endif
        }

        #endregion

        #region Private

        /// <summary>
        /// Matches the hand visuals of the Interaction Tests scenes: both Quest hand meshes on, the XR Hands
        /// sample visualizer off. The main scene currently hides the left mesh and shows the visualizer instead.
        /// </summary>
        private static void UseInteractionTestHands(Transform rig)
        {
            SetActiveInRig(rig, LeftHandVisualName, true);
            SetActiveInRig(rig, HandVisualizerName, false);
        }

        private static void SetActiveInRig(Transform rig, string objectName, bool isActive)
        {
            Transform target = NeighborhoodBuildUtility.FindDeep(rig, objectName);
            if (target == null)
            {
                Debug.LogWarning($"[Neighborhood] The XR rig has no {objectName}, so its hand visuals stay as in the main scene.");
                return;
            }

            target.gameObject.SetActive(isActive);
        }

        #endregion
    }
}
