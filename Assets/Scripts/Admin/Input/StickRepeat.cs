using UnityEngine;

namespace DroneDDW.Admin
{
	// Turns a thumbstick into hover moves like a keyboard's arrow keys: one move when the stick is pushed past the press
	// threshold along its stronger axis, then repeats while it is held, until it comes back under the release threshold.
	// Pushing it to another direction moves that way at once.
	internal sealed class StickRepeat
	{
		#region Fields

		private bool _isHeld;
		private MenuDirection _heldDirection;
		private float _nextRepeatSeconds;

		#endregion

		#region Internal

		// True when the hover should move this frame, with the direction
		internal bool TryRead(Vector2 stick, float nowSeconds, AdminMenuSettings settings, out MenuDirection direction)
		{
			if (_isHeld)
			{
				float heldAmount = Vector2.Dot(stick, ToVector(_heldDirection));
				bool isPushedElsewhere = TryGetPushed(stick, settings.StickPressThreshold, out MenuDirection pushed)
					&& pushed != _heldDirection;
				if (heldAmount >= settings.StickReleaseThreshold && !isPushedElsewhere)
				{
					direction = _heldDirection;
					if (nowSeconds < _nextRepeatSeconds)
					{
						return false;
					}

					_nextRepeatSeconds = nowSeconds + settings.RepeatIntervalSeconds;
					return true;
				}

				_isHeld = false;
			}

			if (!TryGetPushed(stick, settings.StickPressThreshold, out direction))
			{
				return false;
			}

			_isHeld = true;
			_heldDirection = direction;
			_nextRepeatSeconds = nowSeconds + settings.RepeatDelaySeconds;
			return true;
		}

		// Forgets a held stick, so it has to come back before it moves the hover again
		internal void Clear()
		{
			_isHeld = false;
		}

		internal static Vector2 ToVector(MenuDirection direction)
		{
			switch (direction)
			{
				case MenuDirection.Up: return Vector2.up;
				case MenuDirection.Down: return Vector2.down;
				case MenuDirection.Left: return Vector2.left;
				case MenuDirection.Right: return Vector2.right;
				default: throw new System.ArgumentOutOfRangeException(nameof(direction), direction, null);
			}
		}

		#endregion

		#region Private

		// The stick's stronger axis, when it is pushed past the threshold
		private static bool TryGetPushed(Vector2 stick, float threshold, out MenuDirection direction)
		{
			bool isHorizontal = Mathf.Abs(stick.x) > Mathf.Abs(stick.y);
			float amount = isHorizontal ? stick.x : stick.y;
			if (isHorizontal)
			{
				direction = amount > 0f ? MenuDirection.Right : MenuDirection.Left;
			}
			else
			{
				direction = amount > 0f ? MenuDirection.Up : MenuDirection.Down;
			}

			return Mathf.Abs(amount) >= threshold;
		}

		#endregion
	}
}
