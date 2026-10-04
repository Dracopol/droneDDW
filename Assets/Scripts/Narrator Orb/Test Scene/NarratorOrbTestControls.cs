using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDDW.NarratorOrb.TestScene
{
	// Controls for the Narrator Orb test scene. Once the headset reports its first pose, the orb moves 0.8 m in front
	// of the head at eye height (the tracking space's forward is rarely where a seated player looks), taking the
	// compare camera with it; R places it there again. Keys: 0 to 4 pick a step, Space plays, pauses and resumes the
	// narration, F makes the orb face the compare camera (the Game view) instead of the headset and back. Without a
	// headset nothing moves, so the Game view keeps the web preview's framing. The compare camera only draws in the
	// editor.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Narrator Orb/Narrator Orb Test Controls")]
	internal sealed class NarratorOrbTestControls : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private NarratorOrbController _orb;
		[SerializeField] private AudioSource _voice;

		[Tooltip("Draws the Game view with the web preview's framing, for side-by-side comparison")]
		[SerializeField] private Camera _compareCamera;

		[Tooltip("Start facing the compare camera instead of the headset (F toggles)")]
		[SerializeField] private bool _isFacingCompareCamera;

		[Tooltip("How far in front of the head the orb is placed")]
		[Min(0.1f)]
		[SerializeField] private float _orbDistanceMeters = 0.8f;

		#endregion

		#region Fields

		// Below this the head looks almost straight up or down and its yaw is unreliable
		private const float MinFlatForwardLength = 0.2f;

		private bool _isFacingCompareCameraNow;
		private bool _isPlaced;
		private float _compareDistanceMeters;

		#endregion

		#region Setup

		private void Awake()
		{
			// Nobody sees the compare camera in a build, so it shouldn't cost a render there
			if (!Application.isEditor && _compareCamera != null)
			{
				_compareCamera.enabled = false;
			}
		}

		private void Start()
		{
			SetFacingCompareCamera(_isFacingCompareCamera);
			if (_compareCamera != null)
			{
				_compareDistanceMeters = Vector3.Distance(_compareCamera.transform.position, _orb.transform.position);
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			// The camera sits exactly on its parent's origin until the headset reports its first pose
			Transform head = _orb.Head;
			if (!_isPlaced && head != null && head.localPosition != Vector3.zero)
			{
				_isPlaced = true;
				PlaceInFrontOfHead(head);
			}

			Keyboard keyboard = Keyboard.current;
			if (keyboard == null)
			{
				return;
			}

			for (int step = 0; step < 5; step++)
			{
				if (WasStepPressed(keyboard, step))
				{
					_orb.SetStep(step);
				}
			}

			if (keyboard.spaceKey.wasPressedThisFrame)
			{
				TogglePlayback();
			}

			if (keyboard.fKey.wasPressedThisFrame)
			{
				SetFacingCompareCamera(!_isFacingCompareCameraNow);
			}

			if (keyboard.rKey.wasPressedThisFrame && head != null)
			{
				PlaceInFrontOfHead(head);
			}
		}

		#endregion

		#region Internal

		internal void Configure(NarratorOrbController orb, AudioSource voice, Camera compareCamera)
		{
			_orb = orb;
			_voice = voice;
			_compareCamera = compareCamera;
		}

		#endregion

		#region Private

		// In front of the head at eye height, turned with the head's yaw only; the compare camera stays on the line
		// between them
		private void PlaceInFrontOfHead(Transform head)
		{
			Vector3 forward = head.forward;
			forward.y = 0f;
			if (forward.magnitude < MinFlatForwardLength)
			{
				forward = head.up;
				forward.y = 0f;
			}

			forward.Normalize();
			Vector3 orbPosition = head.position + forward * _orbDistanceMeters;
			if (_compareCamera != null)
			{
				Vector3 comparePosition = orbPosition - forward * _compareDistanceMeters;
				_compareCamera.transform.SetPositionAndRotation(comparePosition, Quaternion.LookRotation(forward));
			}

			// After the compare camera, which the orb may be facing
			_orb.MoveTo(orbPosition);
			Debug.Log($"[Narrator Orb] Test scene: placed the orb {_orbDistanceMeters} m in front of the head, at "
				+ $"{orbPosition} (head at {head.position}).", this);
		}

		private void TogglePlayback()
		{
			if (_voice.isPlaying)
			{
				_voice.Pause();
			}
			else if (_voice.timeSamples > 0)
			{
				_voice.UnPause();
			}
			else
			{
				_voice.Play();
			}
		}

		private void SetFacingCompareCamera(bool isFacing)
		{
			_isFacingCompareCameraNow = isFacing && _compareCamera != null && _compareCamera.enabled;
			_orb.OverrideHead(_isFacingCompareCameraNow ? _compareCamera.transform : null);
		}

		private static bool WasStepPressed(Keyboard keyboard, int step)
		{
			switch (step)
			{
				case 0: return keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame;
				case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
				case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
				case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
				case 4: return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
				default: throw new ArgumentOutOfRangeException(nameof(step), step, null);
			}
		}

		#endregion
	}
}
