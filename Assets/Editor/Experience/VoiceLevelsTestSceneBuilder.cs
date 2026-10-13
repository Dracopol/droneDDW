using DroneDDW.Experience.TestScene;
using DroneDDW.NarratorOrb;
using DroneDDW.Neighborhood.Editor.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Experience.Editor
{
	// Menu: DroneDDW > Experience > Build Voice Levels Test Scene. A scene to set how loud the people are against the orb
	// (Content/Data/Voice/Voice Levels.asset): the project's XR rig at the seat, the orb where it floats in the experience,
	// and controls that load the Neighborhood on top (its people speak from their real spots) and play each voice on a
	// key. Not in the build: play it over Link. Build the Neighborhood and Experience scenes first.
	internal static class VoiceLevelsTestSceneBuilder
	{
		#region Fields

		internal const string ScenePath = "Assets/Scenes/Experience/Voice Levels Test.unity";

		private const string MenuPath = "DroneDDW/Experience/Build Voice Levels Test Scene";
		private const float CameraFarMeters = 1100f;

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Build()
		{
			GameObject orbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExperienceSceneBuilder.OrbPrefabPath);
			ExperienceSettings settings = AssetDatabase.LoadAssetAtPath<ExperienceSettings>(ExperienceSceneBuilder.SettingsPath);
			if (orbPrefab == null || settings == null)
			{
				Debug.LogError("[Experience] The voice levels test needs the Narrator Orb prefab and the Experience Settings: "
					+ "build the orb's assets and the Experience scene first.");
				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			ExperienceSceneBuilder.ReadSeat(out float seatHeight, out float eyeHeight);
			Vector3 seat = new(0f, seatHeight, 0f);
			Vector3 eyes = new(0f, seatHeight + eyeHeight, 0f);
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			Camera camera = NeighborhoodRigBuilder.CopyXRRig(scene, seat, CameraFarMeters, out Transform trackingSpace);
			if (camera == null)
			{
				Debug.LogError("[Experience] The voice levels test needs the project's XR rig (see the warning above).");
				return;
			}

			NarratorOrbController orb = ExperienceSceneBuilder.PlaceOrb(orbPrefab, scene, camera.transform,
				eyes + ExperienceSceneBuilder.OrbHomeOffset, out _);
			GameObject controlsObject = new("Voice Levels Test Controls");
			VoiceLevelsTestControls controls = controlsObject.AddComponent<VoiceLevelsTestControls>();
			controls.Configure(orb, settings.NeighborhoodWelcome, camera.transform, trackingSpace,
				ExperienceSceneBuilder.CreateButtonAction("Next Voice", "<Keyboard>/space", "PrimaryButton"));

			EditorSceneManager.SaveScene(scene, ScenePath);
			Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
			Debug.Log($"[Experience] Built {ScenePath}: play it, then 1 orb, 2 Grandma, 3 worker, 4 student, A / X or Space "
				+ "the next one, S orb spatial or stereo; tune Content/Data/Voice/Voice Levels.asset.");
		}

		#endregion
	}
}
