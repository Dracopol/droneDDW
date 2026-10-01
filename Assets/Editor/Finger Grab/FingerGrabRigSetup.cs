using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace DroneDDW.FingerGrab.Editor
{
	// Switches a scene's hand rig to the finger grab: the thumb with any finger selects (tip to tip, or knuckle to
	// knuckle), grabs attach and near hovering happen between the fingers, and there are no far (ray) grabs. Both
	// hands read their thresholds from one "Finger Grab Settings" object, which points at the shared settings asset.
	// The rig's pinch reader stays in the scene unused, so going back is one select input reference per hand. Public
	// because the park builder, in another assembly, applies it to the rig it copies.
	public static class FingerGrabRigSetup
	{
		#region Fields

		private const string MenuRoot = "DroneDDW/Interaction/";
		private const string LogPrefix = "[DDW Finger Grab]";
		private const string UndoName = "Add Finger Grab";
		private const string SourceScenePath = "Assets/Scenes/Interaction Tests/Slider Lever Test/Grab Slider Test.unity";
		private const string TestScenePath = "Assets/Scenes/Interaction Tests/Slider Lever Test/Finger Grab Slider Test.unity";
		private const string SettingsAssetPath = "Assets/Content/Data/Finger Grab Settings.asset";
		private const string SettingsObjectName = "Finger Grab Settings";
		private const string FingerGrabName = "Finger Grab";
		private const string ReadoutName = "Finger Grab Readout";
		private const float ReadoutFontSize = 0.13f;

		private static readonly Vector2 ReadoutSizeMeters = new(0.34f, 0.12f);

		#endregion

		#region Public

		// Sets up both hands of the scene's XR rig and the scene's "Finger Grab Settings" object, or brings existing ones
		// up to date. Returns how many hands use the finger grab afterwards.
		public static int AddToScene(Scene scene)
		{
			XROrigin origin = FindInScene<XROrigin>(scene);
			if (origin == null)
			{
				Debug.LogError($"{LogPrefix} {scene.name} has no XR Origin.");
				return 0;
			}

			FingerGrabSettingsHolder settingsHolder = FindOrCreateSettingsHolder(scene);
			int handCount = 0;
			foreach (NearFarInteractor interactor in origin.GetComponentsInChildren<NearFarInteractor>(true))
			{
				if (AddToHand(origin, interactor, settingsHolder))
				{
					handCount++;
				}
			}

			return handCount;
		}

		#endregion

		#region Private

		[MenuItem(MenuRoot + "Add Finger Grab To Rig In Open Scene")]
		private static void AddToOpenScene()
		{
			Scene scene = SceneManager.GetActiveScene();
			int handCount = AddToScene(scene);
			if (handCount > 0)
			{
				EditorSceneManager.MarkSceneDirty(scene);
			}

			Debug.Log($"{LogPrefix} Set up the finger grab on {handCount} hands in {scene.name}. Save the scene to keep it.");
		}

		[MenuItem(MenuRoot + "Create Finger Grab Slider Test")]
		private static void CreateFingerGrabSliderTest()
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) != null)
			{
				Debug.LogWarning($"{LogPrefix} {TestScenePath} already exists, opening it instead.");
				EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
				return;
			}

			// The copy gets its own GUID, and everything but the rig stays identical to the pinch version
			if (!AssetDatabase.CopyAsset(SourceScenePath, TestScenePath))
			{
				Debug.LogError($"{LogPrefix} Could not copy {SourceScenePath}.");
				return;
			}

			Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
			int handCount = AddToScene(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log($"{LogPrefix} Created {TestScenePath} with the finger grab on {handCount} hands.");
		}

		// For batch mode: brings the existing test scene up to date and saves it
		private static void UpdateFingerGrabSliderTest()
		{
			Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
			int handCount = AddToScene(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log($"{LogPrefix} Updated {TestScenePath}: {handCount} hands read from {SettingsAssetPath}.");
		}

		private static T FindInScene<T>(Scene scene) where T : Component
		{
			foreach (GameObject rootObject in scene.GetRootGameObjects())
			{
				T component = rootObject.GetComponentInChildren<T>(true);
				if (component != null)
				{
					return component;
				}
			}

			return null;
		}

		private static FingerGrabSettingsHolder FindOrCreateSettingsHolder(Scene scene)
		{
			FingerGrabSettingsHolder settingsHolder = FindInScene<FingerGrabSettingsHolder>(scene);
			if (settingsHolder == null)
			{
				GameObject holderObject = new(SettingsObjectName);
				Undo.RegisterCreatedObjectUndo(holderObject, UndoName);
				SceneManager.MoveGameObjectToScene(holderObject, scene);
				settingsHolder = holderObject.AddComponent<FingerGrabSettingsHolder>();
			}

			if (settingsHolder.Settings == null)
			{
				Undo.RecordObject(settingsHolder, UndoName);
				settingsHolder.Configure(LoadOrCreateSettings());
				EditorUtility.SetDirty(settingsHolder);
			}

			return settingsHolder;
		}

		private static FingerGrabSettings LoadOrCreateSettings()
		{
			FingerGrabSettings settings = AssetDatabase.LoadAssetAtPath<FingerGrabSettings>(SettingsAssetPath);
			if (settings != null)
			{
				return settings;
			}

			settings = ScriptableObject.CreateInstance<FingerGrabSettings>();
			AssetDatabase.CreateAsset(settings, SettingsAssetPath);
			AssetDatabase.SaveAssets();
			Debug.Log($"{LogPrefix} Created {SettingsAssetPath} with the default thresholds.");
			return settings;
		}

		// Adds the finger grab to one hand, or points a hand that already has it at the settings holder
		private static bool AddToHand(XROrigin origin, NearFarInteractor interactor, FingerGrabSettingsHolder settingsHolder)
		{
			// The interactor sits on a hand object ("Left Hand", "Right Hand") next to the rig's pose objects
			Transform hand = interactor.transform.parent;
			Handedness handedness = interactor.handedness == InteractorHandedness.Left ? Handedness.Left : Handedness.Right;
			Transform trackingSpace = origin.CameraFloorOffsetObject.transform;

			Transform existing = hand.Find(FingerGrabName);
			if (existing != null && existing.TryGetComponent(out FingerGrabButtonReader existingReader))
			{
				Undo.RecordObject(existingReader, UndoName);
				existingReader.Configure(handedness, trackingSpace, settingsHolder);
				EditorUtility.SetDirty(existingReader);
				return true;
			}

			if (!interactor.TryGetComponent(out InteractionAttachController attachController)
				|| attachController.transformToFollow == null)
			{
				Debug.LogError($"{LogPrefix} {hand.name}: the Near-Far Interactor has no attach pose to replace.");
				return false;
			}

			Transform pinchPose = attachController.transformToFollow;

			GameObject grabObject = new(FingerGrabName);
			Undo.RegisterCreatedObjectUndo(grabObject, UndoName);
			grabObject.transform.SetParent(hand, false);
			FingerGrabButtonReader reader = grabObject.AddComponent<FingerGrabButtonReader>();
			reader.Configure(handedness, trackingSpace, settingsHolder);
			FingerGrabPoint grabPoint = grabObject.AddComponent<FingerGrabPoint>();
			grabPoint.Configure(reader, pinchPose);
			AddReadout(grabObject.transform, reader, origin.Camera.transform);

			Undo.RecordObject(interactor, UndoName);
			interactor.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
			interactor.selectInput.SetObjectReference(reader);
			interactor.enableFarCasting = false;
			EditorUtility.SetDirty(interactor);

			Undo.RecordObject(attachController, UndoName);
			attachController.transformToFollow = grabObject.transform;
			EditorUtility.SetDirty(attachController);

			if (interactor.nearInteractionCaster is InteractionCasterBase nearCaster)
			{
				Undo.RecordObject(nearCaster, UndoName);
				nearCaster.castOrigin = grabObject.transform;
				EditorUtility.SetDirty(nearCaster);
			}

			return true;
		}

		private static void AddReadout(Transform parent, FingerGrabButtonReader reader, Transform head)
		{
			GameObject readoutObject = new(ReadoutName, typeof(RectTransform));
			readoutObject.transform.SetParent(parent, false);
			TextMeshPro label = readoutObject.AddComponent<TextMeshPro>();
			label.rectTransform.sizeDelta = ReadoutSizeMeters;
			label.fontSize = ReadoutFontSize;
			label.alignment = TextAlignmentOptions.Center;
			label.textWrappingMode = TextWrappingModes.NoWrap;
			FingerGrabReadout readout = readoutObject.AddComponent<FingerGrabReadout>();
			readout.Configure(reader, label, head);
		}

		#endregion
	}
}
