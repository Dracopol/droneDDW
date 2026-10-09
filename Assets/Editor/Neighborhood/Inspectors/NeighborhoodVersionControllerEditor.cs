using DroneDDW.Neighborhood.Versions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Inspectors
{
    /// <summary>
    /// Adds version buttons to the controller: in edit mode they switch which version's objects are on
    /// (handy for editing one version), in play mode they run the real transition.
    /// </summary>
    [CustomEditor(typeof(NeighborhoodVersionController))]
    internal sealed class NeighborhoodVersionControllerEditor : UnityEditor.Editor
    {
        #region Public

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            NeighborhoodVersionController controller = (NeighborhoodVersionController)target;
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

        private static void DrawEditModeControls(NeighborhoodVersionController controller)
        {
            EditorGUILayout.LabelField("Show version while editing", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < NeighborhoodVersions.Count; i++)
                {
                    NeighborhoodVersion version = (NeighborhoodVersion)i;
                    if (GUILayout.Button($"{version}  ({controller.CountElementsInVersion(version)})"))
                    {
                        controller.ShowVersionInEditor(version);
                        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                    }
                }
            }

            EditorGUILayout.HelpBox("Numbers in brackets are the changing objects in that version. This only changes what "
                + "is switched on while you edit (the people keep their start pose): Play always starts at Start Version.",
                MessageType.None);
        }

        private static void DrawPlayModeControls(NeighborhoodVersionController controller)
        {
            string state = controller.IsTransitioning ? "moving to" : "at";
            EditorGUILayout.LabelField("Neighborhood", $"{state} {controller.CurrentVersion}");
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < NeighborhoodVersions.Count; i++)
                {
                    NeighborhoodVersion version = (NeighborhoodVersion)i;
                    if (GUILayout.Button(version.ToString()))
                    {
                        controller.RequestVersion(version);
                    }
                }
            }
        }

        #endregion
    }
}
