using DroneDDW.Parks.Levels;
using UnityEngine;
#if PARKS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DroneDDW.Parks.Experience
{
	/// <summary>
	/// Keyboard shortcuts for testing without a headset: 1 to 5 pick a level, left and right arrows step.
	/// Works with the Input System package or the old Input Manager, whichever the project uses.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Level Debug Keys")]
	public sealed class ParkLevelDebugKeys : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkLevelController _controller;

		#endregion

		#region Update

		private void Update()
		{
			for (int level = 0; level < ParkLevels.Count; level++)
			{
				if (WasNumberPressed(level + 1))
				{
					_controller.RequestLevel(level);
					return;
				}
			}

			if (WasStepPressed(false))
			{
				_controller.StepLevel(-1);
			}
			else if (WasStepPressed(true))
			{
				_controller.StepLevel(1);
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLevelController controller)
		{
			_controller = controller;
		}

		#endregion

		#region Private

#if PARKS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
		private static bool WasNumberPressed(int number)
		{
			Keyboard keyboard = Keyboard.current;
			if (keyboard == null)
			{
				return false;
			}

			switch (number)
			{
				case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
				case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
				case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
				case 4: return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
				case 5: return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
				default: return false;
			}
		}

		private static bool WasStepPressed(bool isForward)
		{
			Keyboard keyboard = Keyboard.current;
			if (keyboard == null)
			{
				return false;
			}

			return isForward ? keyboard.rightArrowKey.wasPressedThisFrame : keyboard.leftArrowKey.wasPressedThisFrame;
		}
#elif ENABLE_LEGACY_INPUT_MANAGER
		private static bool WasNumberPressed(int number)
		{
			return Input.GetKeyDown(KeyCode.Alpha0 + number) || Input.GetKeyDown(KeyCode.Keypad0 + number);
		}

		private static bool WasStepPressed(bool isForward)
		{
			return Input.GetKeyDown(isForward ? KeyCode.RightArrow : KeyCode.LeftArrow);
		}
#else
		private static bool WasNumberPressed(int number)
		{
			return false;
		}

		private static bool WasStepPressed(bool isForward)
		{
			return false;
		}
#endif

		#endregion
	}
}
