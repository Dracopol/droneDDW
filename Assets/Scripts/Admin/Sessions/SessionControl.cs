using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Admin.Sessions
{
	// The running session as the admin menu controls it:
	// - Pause freezes it: time stops (animations, physics, delays on scaled time) and all audio pauses, until Resume
	// - Reset session starts the scene over
	// - Reset player location turns and moves the XR rig so the head is above the scene's Player Spot, facing its way
	// Other systems can listen to the events, e.g. to switch off hand interactions while paused.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Session Control")]
	public sealed class SessionControl : MonoBehaviour
	{
		#region Events

		public event Action OnPause;
		public event Action OnResume;

		// Just before the scene reloads
		public event Action OnReset;

		public event Action OnPlayerLocationReset;

		#endregion

		#region Serialized fields

		[Tooltip("The player's XR rig, which Reset player location turns and moves")]
		[SerializeField] private XROrigin _origin;

		[Tooltip("Where Reset player location puts the player")]
		[SerializeField] private PlayerSpot _playerSpot;

		#endregion

		#region Fields

		// Below this the head looks almost straight up or down and its facing is unreliable
		private const float MinFlatForwardLength = 0.2f;

		private float _timeScaleBeforePause = 1f;

		#endregion

		#region Properties

		public bool IsPaused { get; private set; }

		#endregion

		#region Setup

		// Leaving the scene (or Play mode) while paused mustn't leave the next scene frozen
		private void OnDestroy()
		{
			if (IsPaused)
			{
				RestoreTimeAndAudio();
			}
		}

		#endregion

		#region Public

		public void Pause()
		{
			if (IsPaused)
			{
				return;
			}

			IsPaused = true;
			_timeScaleBeforePause = Time.timeScale;
			Time.timeScale = 0f;
			AudioListener.pause = true;
			OnPause?.Invoke();
		}

		public void Resume()
		{
			if (!IsPaused)
			{
				return;
			}

			RestoreTimeAndAudio();
			OnResume?.Invoke();
		}

		// Resumes and loads the active scene again from the start
		public void ResetSession()
		{
			Resume();
			OnReset?.Invoke();
			Scene scene = SceneManager.GetActiveScene();
			Debug.Log($"[Admin] Resetting the session: reloading {scene.name}.", this);
			if (scene.buildIndex >= 0)
			{
				SceneManager.LoadScene(scene.buildIndex);
				return;
			}

#if UNITY_EDITOR
			// A test scene that isn't in the build list can only be reloaded by its path, in the Editor
			UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
				new LoadSceneParameters(LoadSceneMode.Single));
#else
			Debug.LogError($"[Admin] Can't reload {scene.name}: it isn't in the build's scene list.", this);
#endif
		}

		// False when the scene has no rig or no Player Spot to put the player on
		public bool ResetPlayerLocation()
		{
			if (_origin == null || _origin.Camera == null || _playerSpot == null)
			{
				Debug.LogWarning("[Admin] Reset player location needs Session Control's XR Origin and Player Spot.", this);
				return false;
			}

			Transform head = _origin.Camera.transform;
			Vector3 up = _origin.transform.up;
			Vector3 headForward = Vector3.ProjectOnPlane(head.forward, up);
			if (headForward.magnitude < MinFlatForwardLength)
			{
				headForward = Vector3.ProjectOnPlane(head.up, up);
			}

			float angleDegrees = Vector3.SignedAngle(headForward, _playerSpot.Forward, up);
			_origin.RotateAroundCameraUsingOriginUp(angleDegrees);

			Vector3 target = _playerSpot.Position;
			target.y = head.position.y;
			_origin.MoveCameraToWorldLocation(target);
			Debug.Log($"[Admin] Player location reset: turned {angleDegrees:0} degrees, head now at {head.position}.", this);
			OnPlayerLocationReset?.Invoke();
			return true;
		}

		#endregion

		#region Internal

		internal void Configure(XROrigin origin, PlayerSpot playerSpot)
		{
			_origin = origin;
			_playerSpot = playerSpot;
		}

		#endregion

		#region Private

		private void RestoreTimeAndAudio()
		{
			IsPaused = false;
			Time.timeScale = _timeScaleBeforePause;
			AudioListener.pause = false;
		}

		#endregion
	}
}
