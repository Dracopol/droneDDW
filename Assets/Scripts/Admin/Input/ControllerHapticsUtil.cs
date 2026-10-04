using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Input;

namespace DroneDDW.Admin
{
	// Short buzzes in a Quest controller, through OpenXR. Does nothing for the keyboard, without a headset, or when the
	// controller isn't connected.
	internal static class ControllerHapticsUtil
	{
		#region Internal

		internal static void Pulse(MenuHand hand, float amplitude, float durationSeconds)
		{
			if (hand == MenuHand.None || amplitude <= 0f || durationSeconds <= 0f)
			{
				return;
			}

			XRNode node = hand == MenuHand.Left ? XRNode.LeftHand : XRNode.RightHand;
			InputDevice device = InputDevices.GetDeviceAtXRNode(node);
			if (!device.isValid)
			{
				return;
			}

			// Frequency 0 lets the runtime pick its default
			OpenXRInput.SendHapticImpulse(device, amplitude, 0f, durationSeconds);
		}

		#endregion
	}
}
