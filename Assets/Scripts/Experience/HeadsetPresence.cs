using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDDW.Experience
{
	// Notices the headset coming off, for the goodbye (the participant takes it off), and the admin's start hold. It
	// listens to the headset's proximity sensor through OVRManager (HMDUnmounted / HMDMounted) and, on the Quest, to the
	// app losing and getting back focus, which also happens when the headset comes off. Some lab headsets have the
	// sensor switched off: then the admin holds trigger or grip instead, and in the Editor Space.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Headset Presence")]
	internal sealed class HeadsetPresence : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Held to start, and to stand in for the sensor: trigger or grip, and Space in the Editor")]
		[SerializeField] private InputAction _fallbackAction;

		#endregion

		#region Fields

		private bool _isOn = true;
		private int _offCount;

		#endregion

		#region Setup

		private void OnEnable()
		{
			OVRManager.HMDUnmounted += HandleHeadsetOff;
			OVRManager.HMDMounted += HandleHeadsetOn;
			_fallbackAction.Enable();
		}

		private void OnDisable()
		{
			OVRManager.HMDUnmounted -= HandleHeadsetOff;
			OVRManager.HMDMounted -= HandleHeadsetOn;
			_fallbackAction.Disable();
		}

		// The Editor loses focus whenever another window is clicked, so only the headset's own app counts it
		private void OnApplicationPause(bool isPaused)
		{
			if (Application.isEditor)
			{
				return;
			}

			if (isPaused)
			{
				HandleHeadsetOff();
			}
			else
			{
				HandleHeadsetOn();
			}
		}

		#endregion

		#region Event handlers

		private void HandleHeadsetOff()
		{
			if (!_isOn)
			{
				return;
			}

			_isOn = false;
			_offCount++;
			Debug.Log("[Experience] Headset taken off.", this);
		}

		private void HandleHeadsetOn()
		{
			if (_isOn)
			{
				return;
			}

			_isOn = true;
			Debug.Log("[Experience] Headset put on.", this);
		}

		#endregion

		#region Internal

		internal void Configure(InputAction fallbackAction)
		{
			_fallbackAction = fallbackAction;
		}

		// The admin held the button (trigger or grip, Space in the Editor) for the given time: the participant is ready
		internal async UniTask WaitForStartHoldAsync(float holdSeconds, CancellationToken cancellationToken)
		{
			await WaitForHoldAsync(holdSeconds, () => false, cancellationToken);
			Debug.Log("[Experience] The admin started the session.", this);
		}

		// The headset came off, or the admin held the button for the given time. On the Quest the app sleeps while the
		// headset is off, so this completes when the next person puts it on.
		internal async UniTask WaitForTakenOffAsync(float holdSeconds, CancellationToken cancellationToken)
		{
			int offCount = _offCount;
			if (await WaitForHoldAsync(holdSeconds, () => _offCount > offCount, cancellationToken))
			{
				Debug.Log("[Experience] The admin's button stood in for the headset sensor.", this);
			}
		}

		#endregion

		#region Private

		// True once the button has been held for the given time, false once isDone says so first. A press that was
		// already down when this started (e.g. the click that started the session), or that was down while the admin
		// menu paused the session (its clicks use the same buttons), has to be let go first, and a shorter press
		// doesn't count, so a bump doesn't do it.
		private async UniTask<bool> WaitForHoldAsync(float holdSeconds, System.Func<bool> isDone,
			CancellationToken cancellationToken)
		{
			bool isReleaseNeeded = true;
			float pressedAt = -1f;
			while (!isDone())
			{
				bool isPressed = _fallbackAction.IsPressed();
				if (!isPressed || Time.timeScale == 0f)
				{
					isReleaseNeeded = isPressed;
					pressedAt = -1f;
				}
				else if (!isReleaseNeeded)
				{
					if (pressedAt < 0f)
					{
						pressedAt = Time.unscaledTime;
					}
					else if (Time.unscaledTime - pressedAt >= holdSeconds)
					{
						return true;
					}
				}

				await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
			}

			return false;
		}

		#endregion
	}
}
