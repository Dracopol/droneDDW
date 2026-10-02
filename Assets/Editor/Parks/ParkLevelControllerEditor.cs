using DroneDDW.Parks.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Inspectors
{
	/// <summary>
	/// Adds level buttons to the controller: in edit mode they switch which level's objects are on
	/// (handy for editing one level), in play mode they run the real transition.
	/// </summary>
	[CustomEditor(typeof(ParkLevelController))]
	internal sealed class ParkLevelControllerEditor : UnityEditor.Editor
	{
		#region Public

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			ParkLevelController controller = (ParkLevelController)target;
			EditorGUILayout.Space();
			if (Application.isPlaying)
			{
				DrawPlayModeControls(controller);
			}
			else
			{
				DrawEditModeControls(controller);
			}
		}

		public override bool RequiresConstantRepaint()
		{
			return Application.isPlaying;
		}

		#endregion

		#region Private

		private static void DrawEditModeControls(ParkLevelController controller)
		{
			EditorGUILayout.LabelField("Show level while editing", EditorStyles.boldLabel);
			using (new EditorGUILayout.HorizontalScope())
			{
				for (int level = 0; level < ParkLevels.Count; level++)
				{
					string label = $"{level}  ({controller.CountElementsInLevel(level)})";
					if (GUILayout.Button(new GUIContent(label, ParkLevels.GetName(level))))
					{
						controller.ShowLevelInEditor(level);
						EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
					}
				}
			}

			EditorGUILayout.HelpBox("Numbers in brackets are the changing objects in that level. "
				+ "This only changes what is switched on while you edit: Play always starts at Start Level.", MessageType.None);
		}

		private static void DrawPlayModeControls(ParkLevelController controller)
		{
			int current = controller.CurrentLevel;
			string state = controller.IsTransitioning ? "moving to" : "at";
			EditorGUILayout.LabelField("Park", $"{state} level {current} ({ParkLevels.GetName(current)})");
			using (new EditorGUILayout.HorizontalScope())
			{
				for (int level = 0; level < ParkLevels.Count; level++)
				{
					if (GUILayout.Button(new GUIContent(level.ToString(), ParkLevels.GetName(level))))
					{
						controller.RequestLevel(level);
					}
				}
			}
		}

		#endregion
	}
}
