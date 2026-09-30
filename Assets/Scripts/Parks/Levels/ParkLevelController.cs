using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Parks.Levels
{
	/// <summary>
	/// Moves the park between its five levels. A change travels outwards from the wave origin (the seat)
	/// as a ring: each element waits until the ring reaches it, then leaves (shrinks or sinks) or arrives
	/// (grows or rises). Arrivals start a little after departures so old and new never pile up, and
	/// everything close to the seat moves more slowly so nothing pops up next to the viewer.
	/// A new request during a transition takes over from wherever each element is at that moment.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Level Controller")]
	public sealed class ParkLevelController : MonoBehaviour
	{
		#region Events

		/// <summary>A level was requested and the park starts moving (or snapped) to it.</summary>
		public event Action<ParkLevelChange> OnLevelChange;

		/// <summary>Progress of the running transition, 0 to 1, raised every frame while it runs.</summary>
		public event Action<float> OnTransitionProgress;

		/// <summary>Every element reached the given level.</summary>
		public event Action<int> OnTransitionComplete;

		#endregion

		#region Serialized fields

		[SerializeField, Tooltip("Every object that changes between levels.")]
		private ParkLevelElement[] _elements = Array.Empty<ParkLevelElement>();

		[SerializeField, Tooltip("Where the wave starts; normally the seat. Falls back to this object.")]
		private Transform _waveOrigin;

		[SerializeField, Range(0, 4), Tooltip("0 = full nature, 1 = nature skewed, 2 = balanced, 3 = efficiency skewed, 4 = full efficiency.")]
		private int _startLevel = 2;

		[Header("Wave")]
		[SerializeField, Min(1f), Tooltip("Speed of the ring, metres per second. 24 m/s crosses the park in about 3 s.")]
		private float _waveSpeed = 24f;

		[SerializeField, Min(0.05f), Tooltip("Seconds one element takes to leave or arrive.")]
		private float _elementDuration = 0.9f;

		[SerializeField, Min(0f), Tooltip("Extra wait before arrivals start, so departures clear the way first.")]
		private float _arriveDelay = 0.35f;

		[SerializeField, Min(0f), Tooltip("Elements closer to the wave origin than this move more slowly.")]
		private float _gentleRadius = 8f;

		[SerializeField, Min(1f), Tooltip("Duration multiplier inside the gentle radius.")]
		private float _gentleDurationScale = 1.8f;

		#endregion

		#region Fields

		private readonly List<ParkLevelElement> _moving = new List<ParkLevelElement>();
		private CancellationTokenSource _lifetime;
		private int _transitionId;
		private int _currentLevel = -1;
		private bool _isTransitioning;

		#endregion

		#region Properties

		/// <summary>The level the park is at, or moving to while a transition runs.</summary>
		public int CurrentLevel => _currentLevel;
		public bool IsTransitioning => _isTransitioning;
		public int StartLevel => _startLevel;
		public IReadOnlyList<ParkLevelElement> Elements => _elements;

		private CancellationToken LifetimeToken
		{
			get
			{
				if (_lifetime == null)
				{
					_lifetime = new CancellationTokenSource();
				}

				return _lifetime.Token;
			}
		}

		#endregion

		#region Setup

		private void Awake()
		{
			SetLevelImmediate(_startLevel);
		}

		private void OnDestroy()
		{
			if (_lifetime == null)
			{
				return;
			}

			_lifetime.Cancel();
			_lifetime.Dispose();
			_lifetime = null;
		}

		#endregion

		#region Public

		/// <summary>Starts a transition and forgets about it. Safe to call from input handlers.</summary>
		public void RequestLevel(int level)
		{
			RequestLevelAsync(level, LifetimeToken).Forget();
		}

		public void StepLevel(int delta)
		{
			RequestLevel(ParkLevels.Clamp(_currentLevel + delta));
		}

		/// <summary>
		/// Moves the park to a level with the wave transition.
		/// Returns true when the park arrived, false when a newer request took over first.
		/// </summary>
		public async UniTask<bool> SetLevelAsync(int level, CancellationToken cancellationToken)
		{
			level = ParkLevels.Clamp(level);
			if (level == _currentLevel && !_isTransitioning)
			{
				return true;
			}

			int transitionId = ++_transitionId;
			int previous = _currentLevel;
			float startTime = Time.time;
			float endTime = PlanTransition(level, startTime);
			_currentLevel = level;
			_isTransitioning = true;
			OnLevelChange?.Invoke(new ParkLevelChange(previous, level, false));

			CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeToken);
			try
			{
				bool isFinished = false;
				while (!isFinished)
				{
					await UniTask.NextFrame(linked.Token);
					if (transitionId != _transitionId)
					{
						// A newer request re-planned every element; it owns them now.
						return false;
					}

					float now = Time.time;
					isFinished = EvaluateMoving(now);
					float span = endTime - startTime;
					OnTransitionProgress?.Invoke(span > 0f ? Mathf.Clamp01((now - startTime) / span) : 1f);
				}
			}
			finally
			{
				linked.Dispose();
				if (transitionId == _transitionId)
				{
					_isTransitioning = false;
				}
			}

			OnTransitionComplete?.Invoke(level);
			return true;
		}

		/// <summary>Snaps the park to a level without a transition.</summary>
		public void SetLevelImmediate(int level)
		{
			level = ParkLevels.Clamp(level);
			_transitionId++;
			_isTransitioning = false;
			_moving.Clear();
			int previous = _currentLevel;
			foreach (ParkLevelElement element in _elements)
			{
				if (element != null)
				{
					element.ShowImmediate(element.IsInLevel(level));
				}
			}

			_currentLevel = level;
			OnLevelChange?.Invoke(new ParkLevelChange(previous, level, true));
			OnTransitionComplete?.Invoke(level);
		}

		#endregion

		#region Internal

		internal void Configure(ParkLevelElement[] elements, Transform waveOrigin, int startLevel)
		{
			_elements = elements ?? Array.Empty<ParkLevelElement>();
			_waveOrigin = waveOrigin;
			_startLevel = ParkLevels.Clamp(startLevel);
		}

		/// <summary>Edit mode only: switches objects on and off to show one level while editing.</summary>
		internal void ShowLevelInEditor(int level)
		{
			level = ParkLevels.Clamp(level);
			foreach (ParkLevelElement element in _elements)
			{
				if (element != null)
				{
					element.gameObject.SetActive(element.IsInLevel(level));
				}
			}
		}

		internal int CountElementsInLevel(int level)
		{
			int count = 0;
			foreach (ParkLevelElement element in _elements)
			{
				if (element != null && element.IsInLevel(level))
				{
					count++;
				}
			}

			return count;
		}

		#endregion

		#region Private

		private async UniTask RequestLevelAsync(int level, CancellationToken cancellationToken)
		{
			try
			{
				await SetLevelAsync(level, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				// The controller was destroyed mid-transition.
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
			}
		}

		/// <summary>Schedules every element that has to change. Returns the time the last one arrives.</summary>
		private float PlanTransition(int level, float startTime)
		{
			_moving.Clear();
			Vector3 origin = _waveOrigin != null ? _waveOrigin.position : transform.position;
			float endTime = startTime;
			foreach (ParkLevelElement element in _elements)
			{
				if (element == null)
				{
					continue;
				}

				bool isShown = element.IsInLevel(level);
				float distance = element.HorizontalDistanceTo(origin);
				float delay = distance / _waveSpeed + (isShown ? _arriveDelay : 0f);
				float duration = _elementDuration * (distance < _gentleRadius ? _gentleDurationScale : 1f);
				if (!element.Retarget(isShown, startTime + delay, duration))
				{
					continue;
				}

				_moving.Add(element);
				endTime = Mathf.Max(endTime, element.EndTime);
			}

			return endTime;
		}

		private bool EvaluateMoving(float time)
		{
			bool isFinished = true;
			foreach (ParkLevelElement element in _moving)
			{
				if (element != null && !element.Evaluate(time))
				{
					isFinished = false;
				}
			}

			return isFinished;
		}

		#endregion
	}
}
