using DroneDDW.Parks.Utilities;
using UnityEngine;

namespace DroneDDW.Parks.Levels
{
	/// <summary>
	/// A park object that only exists in some of the five levels. It holds no logic of its own:
	/// <see cref="ParkLevelController"/> schedules it and moves it every frame of a transition.
	/// Presence runs from 0 (gone) to 1 (fully in place) and is shown by scaling or lifting the object
	/// from the pose it has in the scene, which is captured the first time the controller touches it.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Level Element")]
	public sealed class ParkLevelElement : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField, Tooltip("Bit i set = part of level i (0 = full nature, 4 = full efficiency). 31 = every level.")]
		private int _levelMask = 31;

		[SerializeField, Tooltip("Scale grows and shrinks from the pivot; Lift rises out of and sinks into the ground.")]
		private ParkTransitionStyle _style = ParkTransitionStyle.Scale;

		[SerializeField, Min(0f), Tooltip("How far below its place a Lift element starts, in metres.")]
		private float _liftDepth = 1f;

		#endregion

		#region Fields

		private const float MinimumScale = 0.001f;
		private const float MinimumDuration = 0.12f;
		private const float GrowOvershoot = 1.3f;

		private Vector3 _baseLocalPosition;
		private Vector3 _baseLocalScale = Vector3.one;
		private Vector3 _baseWorldPosition;
		private bool _hasBasePose;
		private bool _isAnimating;
		private float _presence = 1f;
		private float _from = 1f;
		private float _to = 1f;
		private float _startTime;
		private float _duration;

		#endregion

		#region Properties

		public int LevelMask => _levelMask;
		public ParkTransitionStyle Style => _style;
		public float LiftDepth => _liftDepth;

		/// <summary>0 = gone, 1 = fully in place. Briefly above 1 while a plant overshoots as it grows.</summary>
		public float Presence => _presence;
		public bool IsAnimating => _isAnimating;

		/// <summary>When the current motion ends (Time.time seconds).</summary>
		internal float EndTime => _startTime + _duration;

		#endregion

		#region Public

		public bool IsInLevel(int level)
		{
			return ParkLevels.Contains(_levelMask, level);
		}

		#endregion

		#region Internal

		internal void Configure(int levelMask, ParkTransitionStyle style, float liftDepth)
		{
			_levelMask = levelMask & ParkLevels.AllMask;
			_style = style;
			_liftDepth = Mathf.Max(0f, liftDepth);
		}

		/// <summary>Distance on the ground plane from the element's resting place to a point.</summary>
		internal float HorizontalDistanceTo(Vector3 point)
		{
			EnsureBasePose();
			float x = _baseWorldPosition.x - point.x;
			float z = _baseWorldPosition.z - point.z;
			return Mathf.Sqrt(x * x + z * z);
		}

		internal void ShowImmediate(bool isShown)
		{
			EnsureBasePose();
			_isAnimating = false;
			_presence = isShown ? 1f : 0f;
			_from = _presence;
			_to = _presence;
			ApplyPresence();
		}

		/// <summary>
		/// Points the element at a new state. Returns false when it already rests there.
		/// An element already moving towards the same state keeps its schedule, so a quick second
		/// request does not restart motion that is under way.
		/// </summary>
		internal bool Retarget(bool isShown, float startTime, float duration)
		{
			EnsureBasePose();
			float target = isShown ? 1f : 0f;
			if (_isAnimating && Mathf.Approximately(_to, target))
			{
				return true;
			}

			if (Mathf.Approximately(_presence, target))
			{
				_isAnimating = false;
				_presence = target;
				_from = target;
				_to = target;
				ApplyPresence();
				return false;
			}

			_from = _presence;
			_to = target;
			_startTime = startTime;
			_duration = _style == ParkTransitionStyle.None
				? 0f
				: Mathf.Max(MinimumDuration, duration * Mathf.Abs(target - _from));
			_isAnimating = true;
			return true;
		}

		/// <summary>Moves the element to where it should be at this time. Returns true once it has arrived.</summary>
		internal bool Evaluate(float time)
		{
			if (!_isAnimating)
			{
				return true;
			}

			if (time < _startTime)
			{
				return false;
			}

			float t = _duration > 0f ? Mathf.Clamp01((time - _startTime) / _duration) : 1f;
			_presence = _from + (_to - _from) * Ease(t, _to > _from);
			if (t >= 1f)
			{
				_presence = _to;
				_isAnimating = false;
			}

			ApplyPresence();
			return !_isAnimating;
		}

		#endregion

		#region Private

		private void EnsureBasePose()
		{
			if (_hasBasePose)
			{
				return;
			}

			Transform self = transform;
			_baseLocalPosition = self.localPosition;
			_baseLocalScale = self.localScale;
			_baseWorldPosition = self.position;
			_presence = gameObject.activeSelf ? 1f : 0f;
			_from = _presence;
			_to = _presence;
			_hasBasePose = true;
		}

		private float Ease(float t, bool isAppearing)
		{
			switch (_style)
			{
				case ParkTransitionStyle.Scale:
					return isAppearing ? ParkEasing.OutBack(t, GrowOvershoot) : ParkEasing.InCubic(t);
				case ParkTransitionStyle.Lift:
					return isAppearing ? ParkEasing.OutCubic(t) : ParkEasing.InCubic(t);
				default:
					return 1f;
			}
		}

		private void ApplyPresence()
		{
			Transform self = transform;
			if (_presence <= 0f)
			{
				// Park it at its resting pose while hidden so the scene never keeps a squashed object.
				self.localPosition = _baseLocalPosition;
				self.localScale = _baseLocalScale;
				if (gameObject.activeSelf)
				{
					gameObject.SetActive(false);
				}

				return;
			}

			switch (_style)
			{
				case ParkTransitionStyle.Scale:
					self.localPosition = _baseLocalPosition;
					self.localScale = _baseLocalScale * Mathf.Max(_presence, MinimumScale);
					break;
				case ParkTransitionStyle.Lift:
					float drop = _liftDepth * (1f - Mathf.Min(_presence, 1f));
					self.localPosition = new Vector3(_baseLocalPosition.x, _baseLocalPosition.y - drop, _baseLocalPosition.z);
					self.localScale = _baseLocalScale;
					break;
				default:
					self.localPosition = _baseLocalPosition;
					self.localScale = _baseLocalScale;
					break;
			}

			if (!gameObject.activeSelf)
			{
				gameObject.SetActive(true);
			}
		}

		#endregion
	}
}
