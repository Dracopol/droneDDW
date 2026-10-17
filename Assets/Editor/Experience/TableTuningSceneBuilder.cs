using DroneDDW.Experience.TestScene;
using DroneDDW.FingerGrab.Editor;
using DroneDDW.Neighborhood.Editor.Building;
using DroneDDW.Neighborhood.Table;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DroneDDW.Experience.Editor
{
	// Menu: DroneDDW > Experience > Build Table Tuning Scene. Builds a scene to find where the table should stand, in
	// passthrough like the experience's practice part: the main scene's XR rig at the seat with the finger grab, and the
	// Neighborhood's whole table (map, slider and lock-in button) without the street. The sticks move the table nearer or
	// further and up or down, and a label behind it shows the distances (TableTuningControls). The values go into
	// Content/Neighborhood/Data/Neighborhood Table Settings.asset, which the Neighborhood and the experience are built
	// with: rebuild those two (the Neighborhood first) once the table is right. Rebuilding replaces this scene. It isn't in
	// the build: play it over Link. Build the Neighborhood scene first (the table uses its materials).
	internal static class TableTuningSceneBuilder
	{
		#region Fields

		internal const string ScenePath = "Assets/Scenes/Experience/Table Tuning.unity";

		private const string MenuPath = "DroneDDW/Experience/Build Table Tuning Scene";
		private const string RegularFontPath = "Assets/Content/Fonts/Inter/Inter Regular SDF.asset";

		// The label stands behind the table, a little above the eyes; one unit is a millimetre
		private static readonly Vector2 LabelSize = new(900f, 210f);
		private static readonly Vector3 LabelOffsetFromEyes = new(0f, 0.15f, 1.6f);

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Build()
		{
			TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
			if (font == null)
			{
				Debug.LogError($"[Experience] The table tuning scene needs {RegularFontPath}.");
				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			ExperienceSceneBuilder.ReadSeat(out float seatHeight, out float eyeHeight);
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			XROrigin origin = ExperienceSceneBuilder.CopyMainSceneParts(scene, new Vector3(0f, seatHeight, 0f), out _,
				out GameObject ghostHands);
			if (origin == null)
			{
				Debug.LogError("[Experience] The table tuning scene needs the main scene's XR rig; none was found.");
				return;
			}

			// Only the experience's hand check uses them
			if (ghostHands != null)
			{
				Object.DestroyImmediate(ghostHands);
			}

			FingerGrabRigSetup.AddToScene(scene);
			Transform head = origin.Camera.transform;
			Transform trackingSpace = origin.CameraFloorOffsetObject != null
				? origin.CameraFloorOffsetObject.transform
				: head.parent;
			LockButtonTestSceneBuilder.CreateLight();

			NeighborhoodTableParts table = NeighborhoodTableBuilder.BuildForTuning(head, trackingSpace);
			NeighborhoodTablePlacement placement = table.Root != null
				? table.Root.GetComponent<NeighborhoodTablePlacement>()
				: null;
			if (placement == null || table.LockButton == null || table.Preview == null)
			{
				Debug.LogError("[Experience] The table tuning scene: the table, its button or its map couldn't be built "
					+ "(see above).");
				return;
			}

			NeighborhoodTableBuilder.AddHandSpheresView(table);
			GameObject controlsObject = new("Table Tuning");
			TableTuningControls controls = controlsObject.AddComponent<TableTuningControls>();
			TMP_Text label = LockButtonTestSceneBuilder.CreateLabel(controlsObject.transform,
				new Vector3(0f, seatHeight + eyeHeight, 0f) + LabelOffsetFromEyes, font, LabelSize);
			controls.Configure(placement, origin, eyeHeight, table.LockButton.transform, table.Slider.transform,
				table.Preview.transform, label, CreateStickAction("Table Distance", "LeftHand", "upArrow", "downArrow"),
				CreateStickAction("Table Height", "RightHand", "pageUp", "pageDown"),
				ExperienceSceneBuilder.CreateButtonAction("Reset Eyes", "<Keyboard>/space", "PrimaryButton"));

			LockButtonTestSceneBuilder.EnsureFolder(ScenePath.Substring(0, ScenePath.LastIndexOf('/')));
			EditorSceneManager.SaveScene(scene, ScenePath);
			Debug.Log($"[Experience] Built {ScenePath}: the table's near edge "
				+ $"{placement.Settings.EdgeDistanceMeters * 100f:0} cm in front of the eyes, its top "
				+ $"{placement.Settings.TopHeightMeters * 100f:0} cm above the floor.");
		}

		// One controller's stick, read for its up and down, and two keys doing the same
		private static InputAction CreateStickAction(string name, string hand, string upKey, string downKey)
		{
			InputAction action = new(name, InputActionType.Value, expectedControlType: "Vector2");
			action.AddBinding($"<XRController>{{{hand}}}/{{Primary2DAxis}}");
			action.AddCompositeBinding("2DVector")
				.With("Up", $"<Keyboard>/{upKey}")
				.With("Down", $"<Keyboard>/{downKey}");
			return action;
		}

		#endregion
	}
}
