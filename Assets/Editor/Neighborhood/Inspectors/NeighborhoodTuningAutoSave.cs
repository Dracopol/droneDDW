using UnityEditor;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Inspectors
{
    /// <summary>
    /// Writes the shared tuning assets to disk as Play mode ends: the slider's push settings, the table's placement and
    /// the finger grab settings. Unity keeps changes made in Play mode in memory anyway, but until they are saved a crash would lose them.
    /// </summary>
    [InitializeOnLoad]
    internal static class NeighborhoodTuningAutoSave
    {
        #region Fields

        // Found by type name, so this needs no reference to the finger grab's internals
        private static readonly string[] AssetTypes =
        {
            "NeighborhoodPushSettings", "NeighborhoodTableSettings", "FingerGrabSettings",
        };

        #endregion

        #region Setup

        static NeighborhoodTuningAutoSave()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        #endregion

        #region Event handlers

        private static void HandlePlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            foreach (string type in AssetTypes)
            {
                foreach (string guid in AssetDatabase.FindAssets($"t:{type}", new[] { "Assets" }))
                {
                    Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset == null || !EditorUtility.IsDirty(asset))
                    {
                        continue;
                    }

                    AssetDatabase.SaveAssetIfDirty(asset);
                    Debug.Log($"[Neighborhood] Saved the tuning in {asset.name}.");
                }
            }
        }

        #endregion
    }
}
