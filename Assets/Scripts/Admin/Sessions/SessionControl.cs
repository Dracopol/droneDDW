using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Admin.Sessions
{
	// The running session as the admin menu controls it, and who it is for:
	// - Check-in: with check-in on, nothing starts until the admin scans or types the participant's ticket number and
	//   starts the session (the admin menu opens on that screen); OnSessionStart then tells the experience
	// - RecordChoice writes the participant's row to Sessions.csv: Id, Group, Form choice, Date, Time
	// - Pause freezes the session: time stops (animations, physics, delays on scaled time) and all audio pauses, until
	//   Resume
	// - Reset session starts the same participant over (the scene reloads, keeping the participant); End session goes
	//   back to check-in for the next one
	// - Reset player location turns and moves the XR rig so the head is above the scene's Player Spot, facing its way
	// Other systems can listen to the events, e.g. to switch off hand interactions while paused.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Session Control")]
	public sealed class SessionControl : MonoBehaviour
	{
		#region Events

		public event Action OnPause;
		public event Action OnResume;

		// Just before the scene reloads, for Reset session and End session alike
		public event Action OnReset;

		public event Action OnPlayerLocationReset;

		// The admin started the session for the participant (not raised when Reset session starts them over)
		public event Action OnSessionStart;

		#endregion

		#region Serialized fields

		[Tooltip("The player's XR rig, which Reset player location turns and moves")]
		[SerializeField] private XROrigin _origin;

		[Tooltip("Where Reset player location puts the player")]
		[SerializeField] private PlayerSpot _playerSpot;

		[Tooltip("Where Sessions.csv is: the admin menu's settings")]
		[SerializeField] private AdminMenuSettings _settings;

		[Tooltip("On: the scene waits for the admin to check a participant in (the admin menu opens on the ticket scan "
			+ "screen). Off for test scenes that run without a participant.")]
		[SerializeField] private bool _isCheckInRequired;

		#endregion

		#region Fields

		// Every participant is in group A for now
		private const string ParticipantGroup = "A";
		private const string DateFormat = "yyyy-MM-dd";
		private const string TimeFormat = "HH:mm";

		// Below this the head looks almost straight up or down and its facing is unreliable
		private const float MinFlatForwardLength = 0.2f;

		// The participant Reset session starts over, carried across the scene reload
		private static string CarriedParticipantId;

		private float _timeScaleBeforePause = 1f;
		private string _sessionsFolderOverride;

		#endregion

		#region Properties

		public bool IsPaused { get; private set; }

		// The participant's ticket number, or null before check-in
		public string ParticipantId { get; private set; }

		public string Group => ParticipantGroup;
		public bool HasParticipant => ParticipantId != null;

		// Waiting for the admin to check a participant in
		public bool IsCheckingIn => _isCheckInRequired && !HasParticipant;

		// Started over by Reset session: the participant is already wearing the headset
		public bool IsRestart { get; private set; }

		#endregion

		#region Setup

		private void Awake()
		{
			if (CarriedParticipantId == null)
			{
				return;
			}

			ParticipantId = CarriedParticipantId;
			CarriedParticipantId = null;
			IsRestart = true;
			Debug.Log($"[Admin] Session started over for participant {ParticipantId}.", this);
		}

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

		// The admin checked the participant in: their ticket number, as read or typed
		public void StartSession(string participantId)
		{
			if (string.IsNullOrWhiteSpace(participantId))
			{
				Debug.LogWarning("[Admin] A session needs a participant number.", this);
				return;
			}

			ParticipantId = participantId.Trim();
			IsRestart = false;
			Debug.Log($"[Admin] Session started for participant {ParticipantId}, group {Group}.", this);
			OnSessionStart?.Invoke();
		}

		// Writes the participant's row to Sessions.csv, timed now. False (and an error in the log) when there's no
		// participant or the file can't be written.
		public bool RecordChoice(string formChoice)
		{
			if (!HasParticipant)
			{
				Debug.LogError($"[Admin] Can't record \"{formChoice}\": no participant is checked in.", this);
				return false;
			}

			if (_settings == null)
			{
				Debug.LogError($"[Admin] Can't record \"{formChoice}\": Session Control has no admin menu settings to "
					+ "say where the sessions file is.", this);
				return false;
			}

			DateTime now = DateTime.Now;
			(string Column, string Value)[] cells =
			{
				(SessionColumns.Id, ParticipantId),
				(SessionColumns.Group, Group),
				(SessionColumns.FormChoice, formChoice),
				(SessionColumns.Date, now.ToString(DateFormat, CultureInfo.InvariantCulture)),
				(SessionColumns.Time, now.ToString(TimeFormat, CultureInfo.InvariantCulture)),
			};

			SessionHistory sessions = CreateHistory();
			try
			{
				List<string> missingColumns = sessions.Append(cells);
				if (missingColumns.Count > 0)
				{
					Debug.LogWarning($"[Admin] {sessions.SessionsPath} has no {string.Join(", ", missingColumns)} "
						+ "column, so the row leaves those out.", this);
				}

				Debug.Log($"[Admin] Recorded participant {ParticipantId}: {formChoice}, to {sessions.SessionsPath}.",
					this);
				return true;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Debug.LogError($"[Admin] Couldn't record participant {ParticipantId}'s choice to "
					+ $"{sessions.SessionsPath}: {exception.Message}", this);
				return false;
			}
		}

		// Resumes and starts the same participant over: the scene reloads from the start, without check-in
		public void ResetSession()
		{
			CarriedParticipantId = ParticipantId;
			Reload("Resetting the session");
		}

		// Resumes and goes back to check-in for the next participant
		public void EndSession()
		{
			CarriedParticipantId = null;
			Reload("Ending the session");
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

		internal void ConfigureSessions(AdminMenuSettings settings, bool isCheckInRequired)
		{
			_settings = settings;
			_isCheckInRequired = isCheckInRequired;
		}

		// The Play mode checks point the sessions file at a temporary folder, so they never touch real sessions
		internal void UseSessionsFolder(string folder)
		{
			_sessionsFolderOverride = folder;
		}

		#endregion

		#region Private

		// Reloads the scene this component is in, which isn't always the active one: while the Neighborhood is loaded
		// on top of the experience, it is
		private void Reload(string reason)
		{
			Resume();
			OnReset?.Invoke();
			Scene scene = gameObject.scene;
			Debug.Log($"[Admin] {reason}: reloading {scene.name}.", this);
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

		private SessionHistory CreateHistory()
		{
			return SessionHistory.FromSettings(_settings, _sessionsFolderOverride);
		}

		private void RestoreTimeAndAudio()
		{
			IsPaused = false;
			Time.timeScale = _timeScaleBeforePause;
			AudioListener.pause = false;
		}

		#endregion
	}
}
