using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDDW.Experience
{
	// Notices the headset coming off and going back on: for the handover (the admin takes it off, the participant puts
	// it on) and the goodbye (the participant takes it off). It listens to the headset's proximity sensor through
	// OVRManager (HMDUnmounted / HMDMounted) and, on the Quest, to the app losing and getting back focus, which also
	// happens when the headset comes off. Some lab headsets have the sensor switched off: then the admin presses A or X
	// instead, and in the Editor Space does.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Headset Presence")]
	internal sealed class HeadsetPresence : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Stands in for the sensor: A / X, and Space in the Editor")]
		[SerializeField] private InputAction _fallbackAction;

		#endregion

		#region Fields

		private bool _isOn = true;
		private int _offCount;
		private int _fallbackCount;

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

		#region Update

		private void Update()
		{
			if (_fallbackAction.WasPressedThisFrame())
			{
				_fallbackCount++;
				Debug.Log("[Experience] The admin's button stood in for the headset sensor.", this);
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

		// The headset came off and is back on (someone else is wearing it now), or the admin pressed the button
		internal async UniTask WaitForHandoverAsync(CancellationToken cancellationToken)
		{
			int offCount = _offCount;
			int fallbackCount = _fallbackCount;
			await UniTask.WaitUntil(() => _fallbackCount > fallbackCount || (_offCount > offCount && _isOn),
				cancellationToken: cancellationToken);
		}

		// The headset came off, or the admin pressed the button. On the Quest the app sleeps while the headset is off,
		// so this completes when the next person puts it on.
		internal async UniTask WaitForTakenOffAsync(CancellationToken cancellationToken)
		{
			int offCount = _offCount;
			int fallbackCount = _fallbackCount;
			await UniTask.WaitUntil(() => _fallbackCount > fallbackCount || _offCount > offCount,
				cancellationToken: cancellationToken);
		}

		#endregion
	}
}
