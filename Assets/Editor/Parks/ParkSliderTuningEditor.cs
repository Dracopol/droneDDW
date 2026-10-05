using DroneDDW.Parks.Table;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Inspectors
{
	/// <summary>
	/// Shows the push and board follow settings assets in place, so selecting the "Slider Tuning" object in the Hierarchy
	/// is enough to tune the hand spheres, the knob's boxes and how the board follows, with a switch for drawing the
	/// spheres and boxes and, in Play mode, a line saying what the board is doing.
	/// </summary>
	[CustomEditor(typeof(ParkSliderTuning))]
	internal sealed class ParkSliderTuningEditor : UnityEditor.Editor
	{
		#region Fields

		private UnityEditor.Editor _pushEditor;
		private UnityEditor.Editor _followEditor;

		#endregion

		#region Setup

		private void OnDisable()
		{
			if (_pushEditor != null)
			{
				DestroyImmediate(_pushEditor);
			}

			if (_followEditor != null)
			{
				DestroyImmediate(_followEditor);
			}
		}

		#endregion

		#region Public

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			ParkSliderTuning tuning = (ParkSliderTuning)target;
			DrawViewToggle(tuning.View);

			EditorGUILayout.Space();
			EditorGUILayout.HelpBox("These values live in two assets in Assets/Content/Data/Parks, shared by the park and "
				+ "the slider tuning scene. Changes made in Play mode are kept, and saved when Play mode ends.",
				MessageType.Info);

			DrawSection("Hands and knob", tuning.Settings, ref _pushEditor, "Park Push Settings");
			DrawSection("Board follow", tuning.FollowSettings, ref _followEditor, "Park Board Follow Settings");
			if (Application.isPlaying)
			{
				DrawFollowStatus(tuning.BoardFollow);
			}
		}

		public override bool RequiresConstantRepaint()
		{
			return Application.isPlaying;
		}

		#endregion

		#region Private

		private static void DrawViewToggle(Behaviour view)
		{
			if (view == null)
			{
				return;
			}

			EditorGUI.BeginChangeCheck();
			bool isShown = EditorGUILayout.Toggle(new GUIContent("Show Spheres And Boxes",
				"Draws the hand spheres and the knob's push and grab boxes. In Play mode this lasts until Play ends."),
				view.enabled);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(view, "Show spheres and boxes");
				view.enabled = isShown;
			}
		}

		private void DrawSection(string title, Object settings, ref UnityEditor.Editor settingsEditor, string assetName)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
			if (settings == null)
			{
				EditorGUILayout.HelpBox($"Assign the {assetName} asset (Assets/Content/Data/Parks).", MessageType.Warning);
				return;
			}

			CreateCachedEditor(settings, null, ref settingsEditor);
			settingsEditor.OnInspectorGUI();
		}

		/// <summary>What the board is doing, and how far the head is from leaving the dead zone.</summary>
		private static void DrawFollowStatus(ParkBoardFollow follow)
		{
			if (follow == null)
			{
				return;
			}

			ParkBoardFollowSettings settings = follow.Settings;
			string offsets = $"turned {follow.TurnOffsetDegrees:F0}° of {settings.StartAngleDegrees:F0}°, "
				+ $"moved {follow.MoveOffsetMeters * 100f:F0} of {settings.StartDistanceMeters * 100f:F0} cm";
			string status = follow.State switch
			{
				ParkBoardFollow.FollowState.NotPlaced => "waiting for the headset",
				ParkBoardFollow.FollowState.Waiting => $"waiting ({offsets})",
				ParkBoardFollow.FollowState.Following => $"following ({offsets})",
				_ => "frozen (slider in use)",
			};

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Board", status);
		}

		#endregion
	}
}
