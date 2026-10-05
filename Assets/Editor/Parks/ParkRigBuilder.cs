using DroneDDW.FingerGrab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if PARKS_XRI
using Unity.XR.CoreUtils;
#endif

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// Seats the viewer in a park scene: a copy of the project's XR rig (the root with an XR Origin in the main scene),
	/// with the hands set up as in the Interaction Tests scenes and grabbing with the finger grab and its shared settings.
	/// For full VR the camera clears to the sky; for passthrough it keeps the main scene's transparent clear, and the root
	/// holding the main scene's passthrough layer comes along. It is a copy: rebuild after changing the rig there.
	/// </summary>
	internal static class ParkRigBuilder
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
		internal static Camera CopyXRRig(Scene scene, Vector3 position, bool isPassthrough, float farClipPlane,
			out Transform trackingSpace)
		{
			trackingSpace = null;
#if PARKS_XRI
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
					bool isRig = rootObject.TryGetComponent(out XROrigin sourceOrigin) && sourceOrigin.Camera != null;
					bool isPassthroughLayer = isPassthrough
						&& rootObject.GetComponentInChildren<OVRPassthroughLayer>(true) != null;
					if ((!isRig || camera != null) && !isPassthroughLayer)
					{
						continue;
					}

					GameObject copy = Object.Instantiate(rootObject);
					copy.name = rootObject.name;
					SceneManager.MoveGameObjectToScene(copy, scene);
					if (!isRig)
					{
						continue;
					}

					copy.transform.SetPositionAndRotation(position, Quaternion.identity);
					UseInteractionTestHands(copy.transform);
					XROrigin origin = copy.GetComponent<XROrigin>();
					trackingSpace = origin.CameraFloorOffsetObject.transform;
					camera = origin.Camera;

					// The main scene's camera clears to transparent black for passthrough; full VR needs the sky
					if (!isPassthrough)
					{
						camera.clearFlags = CameraClearFlags.Skybox;
					}

					camera.farClipPlane = Mathf.Max(camera.farClipPlane, farClipPlane);
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
				Debug.Log($"[Parks] Finger grab set up on {fingerGrabHandCount} hands.");
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
			Transform target = ParkAssetUtility.FindDeep(rig, objectName);
			if (target == null)
			{
				Debug.LogWarning($"[Parks] The XR rig has no {objectName}, so its hand visuals stay as in the main scene.");
				return;
			}

			target.gameObject.SetActive(isActive);
		}

		#endregion
	}
}
