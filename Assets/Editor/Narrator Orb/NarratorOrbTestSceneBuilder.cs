using DroneDDW.NarratorOrb.TestScene;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DroneDDW.NarratorOrb.Editor
{
	// Menu: DroneDDW > Narrator Orb > Create Test Scene. Builds Scenes/Narrator Orb/Narrator Orb Test.unity:
	// - a copy of the main scene's XR rig and passthrough layer, so it starts in passthrough like the tutorial
	// - the Narrator Orb prefab 0.8 m in front of the rig at head height, facing the rig's camera, playing the sample
	//   narration on a loop from its 3D voice
	// - a compare camera with the web preview's framing (0.634 m away, 40° vertical field of view, its background) that
	//   draws the Game view only, for comparing with the browser side by side
	// - the test controls: 0 to 4 pick a step, Space plays and pauses, F faces the compare camera
	// Run Build Orb Assets first. Rebuilding replaces the scene.
	internal static class NarratorOrbTestSceneBuilder
	{
		#region Fields

		internal const string ScenePath = "Assets/Scenes/Narrator Orb/Narrator Orb Test.unity";

		private const string MenuPath = "DroneDDW/Narrator Orb/Create Test Scene";
		private const string DialogTitle = "Narrator Orb test scene";
		private const string MainScenePath = "Assets/Scenes/Questionnaire.unity";
		private const float OrbDistance = 0.8f;

		// The preview's camera starts at (0.12, 0.06, 0.62) from the orb, and the orb turns to face it
		private const float CompareDistance = 0.634f;
		private const float CompareFieldOfView = 40f;
		private const float CompareNearClip = 0.01f;
		private const float CompareFarClip = 20f;
		private const float SeatedEyeHeight = 1.2f;

		private static readonly Color32 CompareBackground = new(0x04, 0x06, 0x1a, 0xff);

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Create()
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NarratorOrbAssetBuilder.PrefabPath);
			AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(NarratorOrbAssetBuilder.SampleClipPath);
			if (prefab == null || clip == null)
			{
				Debug.LogError($"[Narrator Orb] The test scene needs {NarratorOrbAssetBuilder.PrefabPath} and "
					+ $"{NarratorOrbAssetBuilder.SampleClipPath}: run DroneDDW > Narrator Orb > Build Orb Assets "
					+ "first.");
				return;
			}

			bool isReplacing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
			if (!Application.isBatchMode && isReplacing
				&& !EditorUtility.DisplayDialog(DialogTitle, $"Replace {ScenePath}?", "Replace", "Cancel"))
			{
				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			Transform head = CopyMainSceneRig(scene);
			if (head == null)
			{
				Debug.LogWarning($"[Narrator Orb] No XR rig found in {MainScenePath}, so the orb faces a plain head "
					+ "point and only the compare camera draws.");
				head = new GameObject("Head").transform;
				head.position = new Vector3(0f, SeatedEyeHeight, 0f);
			}

			Vector3 headPosition = head.position;
			Vector3 orbPosition = headPosition + Vector3.forward * OrbDistance;
			GameObject orbObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
			Quaternion facingHead = Quaternion.LookRotation(headPosition - orbPosition);
			orbObject.transform.SetPositionAndRotation(orbPosition, facingHead);
			NarratorOrbController orb = orbObject.GetComponent<NarratorOrbController>();
			orb.ConfigureHead(head);
			PrefabUtility.RecordPrefabInstancePropertyModifications(orb);

			AudioSource voice = orb.Voice;
			voice.clip = clip;
			voice.loop = true;
			voice.playOnAwake = true;
			PrefabUtility.RecordPrefabInstancePropertyModifications(voice);

			Camera compareCamera = CreateCompareCamera(orbPosition, headPosition);
			GameObject controls = new("Narrator Orb Test Controls");
			controls.AddComponent<NarratorOrbTestControls>().Configure(orb, voice, compareCamera);

			NarratorOrbAssetBuilder.EnsureFolder(ScenePath.Substring(0, ScenePath.LastIndexOf('/')));
			EditorSceneManager.SaveScene(scene, ScenePath);
			Debug.Log($"[Narrator Orb] Built {ScenePath}: the orb {OrbDistance} m in front of the rig, the compare "
				+ $"camera {CompareDistance} m from it.");
		}

		// Copies the main scene's XR rig (the root with an XR Origin) to the origin, and the root holding its
		// passthrough layer. It is a copy: rebuild the scene after changing the rig there. Returns the rig's camera.
		private static Transform CopyMainSceneRig(Scene scene)
		{
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
			{
				return null;
			}

			Scene source = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
			try
			{
				Transform head = null;
				foreach (GameObject rootObject in source.GetRootGameObjects())
				{
					bool isRig = rootObject.TryGetComponent(out XROrigin origin) && origin.Camera != null;
					bool isPassthrough = rootObject.GetComponentInChildren<OVRPassthroughLayer>(true) != null;
					if (!isRig && !isPassthrough)
					{
						continue;
					}

					GameObject copy = Object.Instantiate(rootObject);
					copy.name = rootObject.name;
					SceneManager.MoveGameObjectToScene(copy, scene);
					if (isRig)
					{
						copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
						head = copy.GetComponent<XROrigin>().Camera.transform;
					}
				}

				return head;
			}
			finally
			{
				EditorSceneManager.CloseScene(source, true);
			}
		}

		// Draws only the Game view (URP leaves it out of the headset), after the XR camera, so the Game view shows this
		// framing
		private static Camera CreateCompareCamera(Vector3 orbPosition, Vector3 headPosition)
		{
			Vector3 position = orbPosition + (headPosition - orbPosition).normalized * CompareDistance;
			GameObject cameraObject = new("Compare Camera (browser framing)");
			cameraObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(orbPosition - position));
			Camera camera = cameraObject.AddComponent<Camera>();
			camera.fieldOfView = CompareFieldOfView;
			camera.nearClipPlane = CompareNearClip;
			camera.farClipPlane = CompareFarClip;
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = CompareBackground;
			camera.allowHDR = false;
			camera.allowMSAA = false;
			camera.depth = 0f;
			camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
			return camera;
		}

		#endregion
	}
}
