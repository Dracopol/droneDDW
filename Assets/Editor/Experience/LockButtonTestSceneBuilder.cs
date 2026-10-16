using DroneDDW.Experience.TestScene;
using DroneDDW.FingerGrab.Editor;
using DroneDDW.Neighborhood.Editor.Building;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DroneDDW.Experience.Editor
{
	// Menu: DroneDDW > Experience > Build Lock Button Test Scenes. Builds two scenes to compare where the lock-in button
	// goes, each in passthrough like the experience's practice part: the main scene's XR rig at the seat with the finger
	// grab, and the practice table (the Neighborhood's table, slider and button, without the miniatures):
	// - Lock Button Corner Test: the button in the table's right corner, between the slider's end and the table's edge
	// - Lock Button Stand Test: a bigger button on a stand beside the table's right edge
	// A label behind the table shows the slider's notch, how far the cap is down and how full the ring is; a few seconds
	// after a lock-in the button resets, to try again. Rebuilding replaces the scenes. They aren't in the build: play them
	// over Link. Build the Neighborhood scene first (the button uses its materials).
	internal static class LockButtonTestSceneBuilder
	{
		#region Fields

		internal const string ScenesFolder = "Assets/Scenes/Experience/Lock Button Tests";
		internal const string CornerSceneName = "Lock Button Corner Test";
		internal const string StandSceneName = "Lock Button Stand Test";

		private const string MenuPath = "DroneDDW/Experience/Build Lock Button Test Scenes";
		private const string RegularFontPath = "Assets/Content/Fonts/Inter/Inter Regular SDF.asset";

		// The label stands behind the table, a little above the eyes; one unit is a millimetre
		private const float LabelWidth = 640f;
		private const float LabelHeight = 190f;
		private const float LabelFontSize = 28f;
		private const float LabelPadding = 20f;
		private const float MetersPerMillimeter = 0.001f;

		private static readonly Vector3 LabelOffsetFromEyes = new(0f, 0.1f, 1.35f);
		private static readonly Color LabelBackground = new(0.07f, 0.08f, 0.1f, 0.85f);

		#endregion

		#region Internal

		internal static string GetScenePath(string sceneName)
		{
			return $"{ScenesFolder}/{sceneName}.unity";
		}

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Build()
		{
			TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
			if (font == null)
			{
				Debug.LogError($"[Experience] The lock button test scenes need {RegularFontPath}.");
				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			EnsureFolder(ScenesFolder);
			bool isCornerBuilt = BuildScene(CornerSceneName, NeighborhoodLockButtonPlacement.TableCorner, font);
			bool isStandBuilt = BuildScene(StandSceneName, NeighborhoodLockButtonPlacement.Stand, font);
			if (isCornerBuilt && isStandBuilt)
			{
				Debug.Log($"[Experience] Built the lock button test scenes in {ScenesFolder}.");
			}
		}

		private static bool BuildScene(string sceneName, NeighborhoodLockButtonPlacement placement, TMP_FontAsset font)
		{
			ExperienceSceneBuilder.ReadSeat(out float seatHeight, out float eyeHeight);
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			XROrigin origin = ExperienceSceneBuilder.CopyMainSceneParts(scene, new Vector3(0f, seatHeight, 0f), out _,
				out GameObject ghostHands);
			if (origin == null)
			{
				Debug.LogError($"[Experience] {sceneName} needs the main scene's XR rig; none was found.");
				return false;
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
			CreateLight();

			NeighborhoodTableParts table = NeighborhoodTableBuilder.BuildPractice(head, trackingSpace, placement);
			if (table.Root == null || table.LockButton == null)
			{
				Debug.LogError($"[Experience] {sceneName}: the practice table or its button couldn't be built (see above).");
				return false;
			}

			GameObject readoutObject = new("Lock Button Test");
			LockButtonTestReadout readout = readoutObject.AddComponent<LockButtonTestReadout>();
			readout.Configure(table.LockButton, table.Slider,
				CreateLabel(readoutObject.transform, new Vector3(0f, seatHeight + eyeHeight, 0f) + LabelOffsetFromEyes, font,
					new Vector2(LabelWidth, LabelHeight)));

			EditorSceneManager.SaveScene(scene, GetScenePath(sceneName));
			return true;
		}

		internal static void CreateLight()
		{
			GameObject lightObject = new("Light");
			lightObject.AddComponent<Light>().type = LightType.Directional;
			lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
		}

		// A dark panel with the readout's lines, facing the seat
		internal static TMP_Text CreateLabel(Transform parent, Vector3 position, TMP_FontAsset font, Vector2 size)
		{
			GameObject labelObject = new("Label", typeof(RectTransform));
			RectTransform labelRect = (RectTransform)labelObject.transform;
			labelRect.SetParent(parent, false);
			Canvas canvas = labelObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.WorldSpace;
			canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
				| AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
			labelRect.sizeDelta = size;
			labelRect.localScale = Vector3.one * MetersPerMillimeter;
			labelRect.position = position;

			Image background = CreateUiPart<Image>("Background", labelRect);
			Stretch(background.rectTransform, 0f);
			background.color = LabelBackground;
			background.raycastTarget = false;

			TextMeshProUGUI text = CreateUiPart<TextMeshProUGUI>("Text", labelRect);
			Stretch(text.rectTransform, LabelPadding);
			text.font = font;
			text.fontSize = LabelFontSize;
			text.alignment = TextAlignmentOptions.MidlineLeft;
			text.textWrappingMode = TextWrappingModes.Normal;
			text.color = Color.white;
			text.raycastTarget = false;
			text.text = "Slider: -\nButton: -";
			return text;
		}

		private static T CreateUiPart<T>(string name, Transform parent) where T : Component
		{
			GameObject part = new(name, typeof(RectTransform));
			part.transform.SetParent(parent, false);
			return part.AddComponent<T>();
		}

		private static void Stretch(RectTransform rect, float inset)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = new Vector2(inset, inset);
			rect.offsetMax = new Vector2(-inset, -inset);
		}

		internal static void EnsureFolder(string folder)
		{
			if (AssetDatabase.IsValidFolder(folder))
			{
				return;
			}

			int slash = folder.LastIndexOf('/');
			string parent = folder.Substring(0, slash);
			EnsureFolder(parent);
			AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
		}

		#endregion
	}
}
