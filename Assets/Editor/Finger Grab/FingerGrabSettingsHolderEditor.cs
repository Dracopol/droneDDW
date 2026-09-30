using UnityEditor;

namespace DroneDDW.FingerGrab.Editor
{
	// Shows the holder's settings asset in place, so selecting the "Finger Grab Settings" object in the Hierarchy is
	// enough to toggle the labels and tune the fingers
	[CustomEditor(typeof(FingerGrabSettingsHolder))]
	internal class FingerGrabSettingsHolderEditor : UnityEditor.Editor
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

			FingerGrabSettings settings = ((FingerGrabSettingsHolder)target).Settings;
			if (settings == null)
			{
				EditorGUILayout.HelpBox("Assign a Finger Grab Settings asset (Assets/Content/Data).", MessageType.Warning);
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.HelpBox("These values live in the asset, shared by every scene that uses it. "
				+ "Changes made in Play mode are kept.", MessageType.Info);
			CreateCachedEditor(settings, null, ref _settingsEditor);
			_settingsEditor.OnInspectorGUI();
		}

		#endregion
	}
}
