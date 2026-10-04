using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace DroneDDW.Admin
{
	// Reads the admin menu's actions (Content/Input/Admin Menu.inputactions) into one command per frame. The actions
	// stay on while the menu is closed, so the menu button can open it.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Admin Menu Input")]
	internal sealed class AdminMenuInput : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("The left controller's menu button: opens the menu, or closes it and resumes")]
		[SerializeField] private InputActionReference _toggleAction;

		[Tooltip("Either thumbstick: moves the hover")]
		[SerializeField] private InputActionReference _navigateAction;

		[Tooltip("Trigger, grip or A / X on either controller: clicks the hovered button")]
		[SerializeField] private InputActionReference _clickAction;

		[Tooltip("B or Y: one screen back, or cancels a dialog")]
		[SerializeField] private InputActionReference _backAction;

		#endregion

		#region Fields

		private readonly StickRepeat _stick = new();

		#endregion

		#region Setup

		private void OnEnable()
		{
			_toggleAction.action.Enable();
			_navigateAction.action.Enable();
			_clickAction.action.Enable();
			_backAction.action.Enable();
		}

		private void OnDisable()
		{
			_toggleAction.action.Disable();
			_navigateAction.action.Disable();
			_clickAction.action.Disable();
			_backAction.action.Disable();
		}

		#endregion

		#region Internal

		internal void Configure(InputActionReference toggleAction, InputActionReference navigateAction,
			InputActionReference clickAction, InputActionReference backAction)
		{
			_toggleAction = toggleAction;
			_navigateAction = navigateAction;
			_clickAction = clickAction;
			_backAction = backAction;
		}

		// This frame's command. When several happen in one frame, the menu button wins, then back, click and move.
		internal MenuCommand Read(float nowSeconds, AdminMenuSettings settings)
		{
			// Read every frame, so a held stick is followed even when another command wins
			InputAction navigate = _navigateAction.action;
			bool isMoved = _stick.TryRead(navigate.ReadValue<Vector2>(), nowSeconds, settings,
				out MenuDirection direction);

			if (WasPressed(_toggleAction, out MenuHand hand))
			{
				return new MenuCommand(MenuCommandKind.Toggle, hand);
			}

			if (WasPressed(_backAction, out hand))
			{
				return new MenuCommand(MenuCommandKind.Back, hand);
			}

			if (WasPressed(_clickAction, out hand))
			{
				return new MenuCommand(MenuCommandKind.Click, hand);
			}

			if (isMoved)
			{
				return new MenuCommand(MenuCommandKind.Move, GetHand(navigate.activeControl), direction);
			}

			return MenuCommand.None;
		}

		// A stick still held when the menu opens has to come back before it moves the hover
		internal void ClearStick()
		{
			_stick.Clear();
		}

		#endregion

		#region Private

		private static bool WasPressed(InputActionReference reference, out MenuHand hand)
		{
			InputAction action = reference.action;
			if (!action.WasPressedThisFrame())
			{
				hand = MenuHand.None;
				return false;
			}

			hand = GetHand(action.activeControl);
			return true;
		}

		// From the device's usages rather than XRController.leftHand, which picks one of several left-hand devices
		// (the tracked hands are left-hand devices too)
		private static MenuHand GetHand(InputControl control)
		{
			if (control == null)
			{
				return MenuHand.None;
			}

			foreach (InternedString usage in control.device.usages)
			{
				if (usage == CommonUsages.LeftHand)
				{
					return MenuHand.Left;
				}

				if (usage == CommonUsages.RightHand)
				{
					return MenuHand.Right;
				}
			}

			return MenuHand.None;
		}

		#endregion
	}
}
