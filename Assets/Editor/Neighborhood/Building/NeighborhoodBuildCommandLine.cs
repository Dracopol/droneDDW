using UnityEditor;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>
    /// Batch-mode entry point, the same as the menu:
    /// Unity -batchmode -quit -projectPath &lt;project&gt; -executeMethod DroneDDW.Neighborhood.Editor.Building.NeighborhoodBuildCommandLine.Build
    /// Exits with code 1 when the Neighborhood content folder is incomplete.
    /// </summary>
    public static class NeighborhoodBuildCommandLine
    {
        #region Public

        public static void Build()
        {
            if (NeighborhoodSceneBuilder.Run(false) == null)
            {
                EditorApplication.Exit(1);
            }
        }

        #endregion
    }
}
