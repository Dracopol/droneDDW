using UnityEditor;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Inspectors
{
	/// <summary>
	/// Writes the shared tuning assets to disk as Play mode ends: the push and board follow settings and the finger grab
	/// settings, which the "Slider Tuning" and "Finger Grab Settings" objects edit live. Unity keeps such changes in
	/// memory anyway, but until they are saved a crash would lose them.
	/// </summary>
	[InitializeOnLoad]
	internal static class ParkTuningAutoSave
	{
		#region Fields

		// Found by type name, so this needs no reference to the finger grab's internals
		private static readonly string[] AssetTypes =
		{
			"ParkPushSettings", "ParkBoardFollowSettings", "FingerGrabSettings",
		};

		#endregion

		#region Setup

		static ParkTuningAutoSave()
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
					Debug.Log($"[Parks] Saved the tuning in {asset.name}.");
				}
			}
		}

		#endregion
	}
}
