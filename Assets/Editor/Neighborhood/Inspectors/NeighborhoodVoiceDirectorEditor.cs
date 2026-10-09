using DroneDDW.Neighborhood.Voice;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Inspectors
{
    /// <summary>Play-mode buttons on the voice director: introductions, reactions, stop, and every line by ID.</summary>
    [CustomEditor(typeof(NeighborhoodVoiceDirector))]
    internal sealed class NeighborhoodVoiceDirectorEditor : UnityEditor.Editor
    {
        #region Fields

        private const int ButtonsPerRow = 6;

        #endregion

        #region Public

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            NeighborhoodVoiceDirector director = (NeighborhoodVoiceDirector)target;
            EditorGUILayout.Space();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("In play mode this shows buttons to play the introductions, the reactions and each line.",
                    MessageType.None);
                return;
            }

            NeighborhoodVoiceLine current = director.CurrentLine;
            EditorGUILayout.LabelField("Speaking", current != null ? $"{current.Id}  \"{current.Text}\"" : "-");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Introductions"))
                {
                    director.PlayIntroductions();
                }

                if (GUILayout.Button("Stop"))
                {
                    director.Stop();
                }
            }

            NeighborhoodVoiceLibrary library = director.Library;
            if (library == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Lines", EditorStyles.boldLabel);
            int column = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (NeighborhoodVoiceLine line in library.Lines)
            {
                if (column == ButtonsPerRow)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    column = 0;
                }

                string label = line.Clip != null ? line.Id : line.Id + " *";
                if (GUILayout.Button(new GUIContent(label, $"{line.Trigger}: \"{line.Text}\"")))
                {
                    director.PlayLine(line.Id);
                }

                column++;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("* = no WAV linked yet (the face moves without sound). Run DroneDDW > Neighborhood > "
                + "Link Voice Lines after importing the WAVs.", MessageType.None);
        }

        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        #endregion
    }
}
