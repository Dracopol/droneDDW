using System;
using DroneDDW.Parks.Experience;
using DroneDDW.Parks.Levels;
using DroneDDW.Parks.Table;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// Menu: DroneDDW / Parks / Build Slider Tuning Scene (AR). Builds Scenes/Park Tunning/Park Slider Tuning.unity, a
	/// passthrough scene for tuning how the hands meet the park's slider while seeing the room and the monitor:
	/// - a copy of the main scene's XR rig and passthrough layer, with the finger grab and its shared settings
	/// - the park's floating board with only the slider, on a strip, following the head exactly like in the park
	/// - an empty level controller wired like the park's, so releasing the slider locks in a level with the same sounds
	///   and lights (and the debug keys 1-5 and the arrows still move it)
	/// - the "Slider Tuning" object, showing the shared push settings (sphere sizes, margin, spacing, push and grab
	///   boxes) and board follow settings (position, dead zone, turn and move speed), with the spheres and boxes drawn
	///   and, in Play mode, what the board is doing
	/// Uses the park's built assets (its materials and rig model), so build the level park once first.
	/// Rebuilding replaces the scene; the tuning lives in the settings assets, which it never touches.
	/// </summary>
	internal static class ParkSliderTuningSceneBuilder
	{
		#region Fields

		private const string MenuPath = "DroneDDW/Parks/Build Slider Tuning Scene (AR)";
		private const string DialogTitle = "Slider tuning scene";
		private const string ScenesFolder = ParkAssetUtility.ScenesFolder + "/Park Tunning";
		private const string ScenePath = ScenesFolder + "/Park Slider Tuning.unity";
		private const int StartLevel = 2;
		private const float LightIntensity = 1f;

		private static readonly Vector3 LightEuler = new Vector3(50f, 30f, 0f);

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Build()
		{
			GameObject rigModel = ParkAssetUtility.LoadModel(ParkBoardBuilder.RigModelName);
			if (rigModel == null)
			{
				Debug.LogError($"[Parks] No {ParkBoardBuilder.RigModelName}.fbx in {ParkAssetUtility.ModelsFolder}, so "
					+ "there is no slider to tune. Build the level park first.");
				return;
			}

			bool isReplacing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
			if (!Application.isBatchMode && isReplacing
				&& !EditorUtility.DisplayDialog(DialogTitle, $"Replace {ScenePath}?", "Replace", "Cancel"))
			{
				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			ParkPushSettings settings = ParkAssetUtility.LoadOrCreatePushSettings();
			ParkBoardFollowSettings followSettings = ParkAssetUtility.LoadOrCreateFollowSettings();
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			Camera camera = ParkRigBuilder.CopyXRRig(scene, Vector3.zero, true, 0f, out Transform trackingSpace);
			if (camera == null)
			{
				Debug.LogWarning($"[Parks] No XR rig found in {ParkRigBuilder.MainScenePath}: the tuning scene has no "
					+ "hands or passthrough.");
			}

			CreateLight();
			GameObject experience = new GameObject("Park_Experience");
			ParkLevelController controller = experience.AddComponent<ParkLevelController>();
			controller.Configure(Array.Empty<ParkLevelElement>(), null, StartLevel);

			Transform head = camera != null ? camera.transform : null;
			ParkBoardBuilder.Board board = ParkBoardBuilder.Build(rigModel, true, Vector3.zero, StartLevel, head,
				trackingSpace, controller, settings, followSettings, true);
			ParkExperience coordinator = experience.AddComponent<ParkExperience>();
			coordinator.Configure(controller, board.Slider, null, null);
			ParkLevelDebugKeys debugKeys = experience.AddComponent<ParkLevelDebugKeys>();
			debugKeys.Configure(controller);
			ParkBoardBuilder.AddTuningObject(settings, followSettings, board);

			ParkAssetUtility.EnsureFolder(ScenesFolder);
			EditorSceneManager.SaveScene(scene, ScenePath);
			AssetDatabase.SaveAssets();
			Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
			Debug.Log($"[Parks] Built {ScenePath}: the slider in passthrough, tuned by the shared push settings.");
		}

		/// <summary>Lights the knob, notches and glyphs, which use URP Lit; the room comes from passthrough.</summary>
		private static void CreateLight()
		{
			Light light = new GameObject("Directional Light").AddComponent<Light>();
			light.type = LightType.Directional;
			light.lightmapBakeType = LightmapBakeType.Realtime;
			light.shadows = LightShadows.None;
			light.intensity = LightIntensity;
			light.transform.rotation = Quaternion.Euler(LightEuler);
		}

		#endregion
	}
}
