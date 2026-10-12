using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Neighborhood.Experience;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Experience
{
	// Takes the participant from passthrough into the Neighborhood and back, each time behind a fade to black:
	// - in: loads the Neighborhood scene on top of this one, makes it the active scene (so its sky, fog and light
	//   apply), hands it this rig's head and hands, hides passthrough and the passthrough-only props (the practice
	//   table, the cards, this scene's light), and the camera clears to the sky
	// - out: makes this scene active again, unloads the Neighborhood, and brings passthrough, its clear and the light
	//   back
	// The session (rig, admin menu, Session Control) stays in this scene all along. The view stays black while the
	// Neighborhood loads, which can take a few seconds on the headset.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Environment Switch")]
	internal sealed class EnvironmentSwitch : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ViewFade _fade;
		[SerializeField] private OVRPassthroughLayer _passthroughLayer;

		[Tooltip("The headset camera")]
		[SerializeField] private Camera _camera;

		[Tooltip("The XR Origin's camera offset: the hand joints are relative to it")]
		[SerializeField] private Transform _trackingSpace;

		[Tooltip("Shown in passthrough only: hidden while in the Neighborhood")]
		[SerializeField] private ExperienceProp[] _passthroughOnlyProps;

		[Tooltip("Shown again on the way back to passthrough, e.g. this scene's light")]
		[SerializeField] private ExperienceProp[] _passthroughLighting;

		[Tooltip("The Neighborhood scene's name; it has to be in the build's scene list")]
		[SerializeField] private string _neighborhoodSceneName = "Neighborhood";

		#endregion

		#region Fields

		private CameraClearFlags _passthroughClearFlags;
		private Color _passthroughBackground;
		private Scene _neighborhoodScene;

		#endregion

		#region Properties

		// The loaded Neighborhood's experience, or null in passthrough
		internal NeighborhoodExperience Neighborhood { get; private set; }

		internal bool IsInNeighborhood => _neighborhoodScene.IsValid() && _neighborhoodScene.isLoaded;

		#endregion

		#region Setup

		private void Awake()
		{
			_passthroughClearFlags = _camera.clearFlags;
			_passthroughBackground = _camera.backgroundColor;
		}

		#endregion

		#region Internal

		internal void Configure(ViewFade fade, OVRPassthroughLayer passthroughLayer, Camera headCamera,
			Transform trackingSpace, ExperienceProp[] passthroughOnlyProps, ExperienceProp[] passthroughLighting)
		{
			_fade = fade;
			_passthroughLayer = passthroughLayer;
			_camera = headCamera;
			_trackingSpace = trackingSpace;
			_passthroughOnlyProps = passthroughOnlyProps;
			_passthroughLighting = passthroughLighting;
		}

		// whileBlack runs once everything has switched, before the view fades back in
		internal async UniTask ToNeighborhoodAsync(float fadeSeconds, float blackHoldSeconds, Action whileBlack,
			CancellationToken cancellationToken)
		{
			await _fade.FadeToAsync(1f, fadeSeconds, cancellationToken);
			await SceneManager.LoadSceneAsync(_neighborhoodSceneName, LoadSceneMode.Additive)
				.ToUniTask(cancellationToken: cancellationToken);
			_neighborhoodScene = SceneManager.GetSceneByName(_neighborhoodSceneName);
			SceneManager.SetActiveScene(_neighborhoodScene);
			Neighborhood = FindNeighborhood(_neighborhoodScene);
			if (Neighborhood != null)
			{
				Neighborhood.AttachRig(_camera.transform, _trackingSpace);
			}
			else
			{
				Debug.LogError($"[Experience] {_neighborhoodSceneName} has no NeighborhoodExperience.", this);
			}

			if (_passthroughLayer != null)
			{
				_passthroughLayer.hidden = true;
			}

			_camera.clearFlags = CameraClearFlags.Skybox;
			SetProps(_passthroughOnlyProps, false);
			SetProps(_passthroughLighting, false);
			whileBlack?.Invoke();
			Debug.Log($"[Experience] In the Neighborhood ({SceneManager.sceneCount} scenes loaded).", this);
			await UniTask.Delay(TimeSpan.FromSeconds(blackHoldSeconds), DelayType.Realtime,
				cancellationToken: cancellationToken);
			await _fade.FadeToAsync(0f, fadeSeconds, cancellationToken);
		}

		internal async UniTask ToPassthroughAsync(float fadeSeconds, float blackHoldSeconds, Action whileBlack,
			CancellationToken cancellationToken)
		{
			await _fade.FadeToAsync(1f, fadeSeconds, cancellationToken);
			SceneManager.SetActiveScene(gameObject.scene);
			Neighborhood = null;
			if (IsInNeighborhood)
			{
				await SceneManager.UnloadSceneAsync(_neighborhoodScene).ToUniTask(cancellationToken: cancellationToken);
			}

			if (_passthroughLayer != null)
			{
				_passthroughLayer.hidden = false;
			}

			_camera.clearFlags = _passthroughClearFlags;
			_camera.backgroundColor = _passthroughBackground;
			SetProps(_passthroughLighting, true);
			whileBlack?.Invoke();
			Debug.Log("[Experience] Back in passthrough.", this);
			await UniTask.Delay(TimeSpan.FromSeconds(blackHoldSeconds), DelayType.Realtime,
				cancellationToken: cancellationToken);
			await _fade.FadeToAsync(0f, fadeSeconds, cancellationToken);
		}

		#endregion

		#region Private

		private static NeighborhoodExperience FindNeighborhood(Scene scene)
		{
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				NeighborhoodExperience experience = root.GetComponentInChildren<NeighborhoodExperience>(true);
				if (experience != null)
				{
					return experience;
				}
			}

			return null;
		}

		private static void SetProps(ExperienceProp[] props, bool isShown)
		{
			foreach (ExperienceProp prop in props)
			{
				if (isShown)
				{
					prop.Show();
				}
				else
				{
					prop.Hide();
				}
			}
		}

		#endregion
	}
}
