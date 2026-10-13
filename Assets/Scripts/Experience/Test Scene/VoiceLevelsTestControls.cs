using DroneDDW.NarratorOrb;
using DroneDDW.Neighborhood.Experience;
using DroneDDW.Voice;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DroneDDW.Experience.TestScene
{
	// The Voice Levels Test scene: loads the Neighborhood on top, as the experience does, so the people speak from their
	// real spots through their real voices, and plays the orb next to them, where it floats in the experience. Play it
	// over Link, select Content/Data/Voice/Voice Levels.asset and tune while listening: changes are heard at once and
	// kept after Play mode.
	// Keys: 1 the orb, 2 Grandma, 3 the worker, 4 the student, A / X on a controller (or Space) the next one in that
	// order, S the orb spatial (as in passthrough) or stereo (as in the Neighborhood, where it isn't seen).
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Test Scene/Voice Levels Test Controls")]
	internal sealed class VoiceLevelsTestControls : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private NarratorOrbController _orb;

		[Tooltip("The orb's line to compare with: one of its lines in the Neighborhood")]
		[SerializeField] private AudioClip _orbLine;

		[SerializeField] private Transform _head;
		[SerializeField] private Transform _trackingSpace;

		[Tooltip("Plays the next voice: A / X, and Space")]
		[SerializeField] private InputAction _nextAction;

		[SerializeField] private string _neighborhoodSceneName = "Neighborhood";

		[Tooltip("Each person's line, in the order Grandma, worker, student (their introductions)")]
		[SerializeField] private string[] _characterLines = { "GRA_01", "WOR_01", "STU_01" };

		#endregion

		#region Fields

		private static readonly VoiceRole[] Order = { VoiceRole.Narrator, VoiceRole.Grandma, VoiceRole.Worker, VoiceRole.Student };

		private NeighborhoodExperience _neighborhood;
		private int _next;
		private bool _isOrbSpatial;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_nextAction.Enable();
		}

		private void OnDisable()
		{
			_nextAction.Disable();
		}

		private void Start()
		{
			_orb.SetSpatial(_isOrbSpatial);
			SceneManager.sceneLoaded += HandleSceneLoaded;
			SceneManager.LoadSceneAsync(_neighborhoodSceneName, LoadSceneMode.Additive);
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= HandleSceneLoaded;
		}

		#endregion

		#region Update

		private void Update()
		{
			Keyboard keyboard = Keyboard.current;
			if (_nextAction.WasPressedThisFrame())
			{
				Play(Order[_next]);
			}
			else if (keyboard == null)
			{
				return;
			}
			else if (keyboard.digit1Key.wasPressedThisFrame)
			{
				Play(VoiceRole.Narrator);
			}
			else if (keyboard.digit2Key.wasPressedThisFrame)
			{
				Play(VoiceRole.Grandma);
			}
			else if (keyboard.digit3Key.wasPressedThisFrame)
			{
				Play(VoiceRole.Worker);
			}
			else if (keyboard.digit4Key.wasPressedThisFrame)
			{
				Play(VoiceRole.Student);
			}
			else if (keyboard.sKey.wasPressedThisFrame)
			{
				_isOrbSpatial = !_isOrbSpatial;
				_orb.SetSpatial(_isOrbSpatial);
				Debug.Log($"[Voice Levels] The orb is now {(_isOrbSpatial ? "spatial, as in passthrough" : "stereo, as in the Neighborhood")}.", this);
			}
		}

		#endregion

		#region Event handlers

		// The Neighborhood takes the light and sky, follows this rig's head and keeps its people quiet until asked
		private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name != _neighborhoodSceneName)
			{
				return;
			}

			SceneManager.SetActiveScene(scene);
			_neighborhood = FindAnyObjectByType<NeighborhoodExperience>();
			if (_neighborhood != null)
			{
				_neighborhood.AttachRig(_head, _trackingSpace);
				_neighborhood.Voice.ReactToChanges = false;
			}

			Debug.Log("[Voice Levels] Ready: 1 orb, 2 Grandma, 3 worker, 4 student, A / X or Space the next one, S orb "
				+ "spatial or stereo. Tune Content/Data/Voice/Voice Levels.asset while listening.", this);
		}

		#endregion

		#region Internal

		internal void Configure(NarratorOrbController orb, AudioClip orbLine, Transform head, Transform trackingSpace,
			InputAction nextAction)
		{
			_orb = orb;
			_orbLine = orbLine;
			_head = head;
			_trackingSpace = trackingSpace;
			_nextAction = nextAction;
		}

		#endregion

		#region Private

		private void Play(VoiceRole role)
		{
			_next = (System.Array.IndexOf(Order, role) + 1) % Order.Length;
			if (_neighborhood != null)
			{
				_neighborhood.Voice.Stop();
			}

			_orb.StopSpeaking();
			if (role == VoiceRole.Narrator)
			{
				_orb.Speak(_orbLine);
			}
			else if (_neighborhood != null)
			{
				_neighborhood.Voice.PlayLine(_characterLines[(int)role - 1]);
			}

			Debug.Log($"[Voice Levels] {role} at {Decibels(role):+0.0;-0.0;0} dB.", this);
		}

		private float Decibels(VoiceRole role)
		{
			VoiceGain gain = role == VoiceRole.Narrator ? _orb.GetComponentInChildren<VoiceGain>() : null;
			if (gain != null)
			{
				return gain.Decibels;
			}

			foreach (VoiceGain candidate in FindObjectsByType<VoiceGain>(FindObjectsSortMode.None))
			{
				if (candidate.Role == role)
				{
					return candidate.Decibels;
				}
			}

			return 0f;
		}

		#endregion
	}
}
