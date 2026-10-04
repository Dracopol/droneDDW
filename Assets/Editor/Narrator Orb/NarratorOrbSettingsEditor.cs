using System;
using System.Collections.Generic;
using DroneDDW.NarratorOrb.Settings;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Editor
{
	// Paste JSON reads the web preview's "Copy settings" output (or a copy from here) from the clipboard; Copy JSON
	// puts every value on it in the same format, with the Unity-only values under "unity". Pasting can be undone.
	[CustomEditor(typeof(NarratorOrbSettings))]
	internal sealed class NarratorOrbSettingsEditor : UnityEditor.Editor
	{
		#region Fields

		private string _status;
		private MessageType _statusType;

		#endregion

		#region Public

		public override void OnInspectorGUI()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button(new GUIContent("Paste JSON",
					"Reads the clipboard: the web preview's \"Copy settings\" output, or a copy from here")))
				{
					Paste();
				}

				if (GUILayout.Button(new GUIContent("Copy JSON",
					"Copies every value in the web preview's format, with the Unity-only values under \"unity\"")))
				{
					Copy();
				}
			}

			if (!string.IsNullOrEmpty(_status))
			{
				EditorGUILayout.HelpBox(_status, _statusType);
			}

			EditorGUILayout.Space();
			DrawDefaultInspector();
		}

		#endregion

		#region Private

		private void Paste()
		{
			NarratorOrbSettings settings = (NarratorOrbSettings)target;
			Undo.RecordObject(settings, "Paste Narrator Orb Settings");
			try
			{
				List<string> problems = NarratorOrbSettingsJson.Apply(settings, EditorGUIUtility.systemCopyBuffer);
				EditorUtility.SetDirty(settings);
				_status = problems.Count == 0
					? "Pasted every value."
					: "Pasted, except:\n" + string.Join("\n", problems);
				_statusType = problems.Count == 0 ? MessageType.Info : MessageType.Warning;
			}
			catch (ArgumentException exception)
			{
				_status = $"The clipboard doesn't hold settings JSON: {exception.Message}";
				_statusType = MessageType.Error;
			}
		}

		private void Copy()
		{
			EditorGUIUtility.systemCopyBuffer = NarratorOrbSettingsJson.ToJson((NarratorOrbSettings)target);
			_status = "Copied as JSON.";
			_statusType = MessageType.Info;
		}

		#endregion
	}
}
