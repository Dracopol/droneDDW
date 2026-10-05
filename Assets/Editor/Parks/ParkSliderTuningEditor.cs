using DroneDDW.Parks.Table;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Inspectors
{
	/// <summary>
	/// Shows the push settings asset in place, so selecting the "Slider Tuning" object in the Hierarchy is enough to tune
	/// the hand spheres and the knob's boxes, with a switch for drawing them.
	/// </summary>
	[CustomEditor(typeof(ParkSliderTuning))]
	internal sealed class ParkSliderTuningEditor : UnityEditor.Editor
	{
		#region Fields

		private UnityEditor.Editor _settingsEditor;

		#endregion

		#region Setup

		private void OnDisable()
		{
			if (_settingsEditor != null)
			{
				DestroyImmediate(_settingsEditor);
			}
		}

		#endregion

		#region Public

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			ParkSliderTuning tuning = (ParkSliderTuning)target;
			DrawViewToggle(tuning.View);

			ParkPushSettings settings = tuning.Settings;
			if (settings == null)
			{
				EditorGUILayout.HelpBox("Assign the Park Push Settings asset (Assets/Content/Data/Parks).", MessageType.Warning);
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.HelpBox("These values live in the asset, shared by the park and the slider tuning scene. "
				+ "Changes made in Play mode are kept, and saved when Play mode ends.", MessageType.Info);
			CreateCachedEditor(settings, null, ref _settingsEditor);
			_settingsEditor.OnInspectorGUI();
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

		#endregion
	}
}
