using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// The table's lock-in button, beside the slider: a push button like the Scripted Button Stepper Test's. Any part of a
	/// hand pushes its cap down; the cap follows the hand and springs back up once it lets go
	/// (<see cref="NeighborhoodPushCap"/>). Holding it pressed lights the ring of segments around the cap one by one, and
	/// once the ring is full the slider's choice is locked in. A press only counts while the button is armed (available,
	/// and the slider resting on a notch without a hand on it), so reaching for the slider can't lock in by accident;
	/// letting go early empties the ring again. The cap only moves while the button is available, and once locked it stays
	/// down and green until <see cref="ResetLock"/>. Pushed by the slider's hand bones
	/// (<see cref="NeighborhoodHandBones"/>), timed by the shared push settings, on scaled time, so a paused session holds
	/// it still.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Lock Button")]
	public sealed class NeighborhoodLockButton : MonoBehaviour
	{
		#region Events

		/// <summary>The choice was locked in: the slider's notch at that moment.</summary>
		public event Action<int> OnLockIn;

		#endregion

		#region Serialized fields

		[SerializeField, Tooltip("The slider whose choice the button locks in.")]
		private NeighborhoodSlider _slider;

		[SerializeField] private NeighborhoodHandBones _handBones;

		[SerializeField, Tooltip("The shared push settings: the hand's thickness and sampling, how long to hold, how fast "
			+ "the ring empties. Uses the defaults when empty.")]
		private NeighborhoodPushSettings _settings;

		[SerializeField, Tooltip("The part a press pushes down, along this object's up axis. Its pivot is the middle of "
			+ "its top face.")]
		private Transform _cap;

		[SerializeField] private Renderer _capRenderer;

		[SerializeField, Tooltip("The ring around the cap, in the order its segments light up while the button is held.")]
		private Renderer[] _ringSegments = Array.Empty<Renderer>();

		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _touchClip;
		[SerializeField] private AudioClip _pressClip;
		[SerializeField] private AudioClip _releaseClip;
		[SerializeField] private AudioClip _lockInClip;

		[Header("Shape")]
		[SerializeField, Min(0.005f), Tooltip("The cap's radius: only a hand over its face pushes it.")]
		private float _capRadiusMeters = 0.034f;

		[SerializeField, Min(0f), Tooltip("How high the cap's top stands above this object when it is pushed all the way "
			+ "down: the collar's height.")]
		private float _pressedTopMeters = 0.012f;

		[SerializeField, Min(0.002f), Tooltip("How far the cap travels, from resting to all the way down.")]
		private float _travelMeters = 0.015f;

		[SerializeField] private NeighborhoodPushCapTuning _push = new NeighborhoodPushCapTuning();

		[Header("Colours")]
		[SerializeField, Tooltip("While the button can't be used.")]
		private Color _unavailableColor = new Color(0.29f, 0.31f, 0.34f);

		[SerializeField, Tooltip("Usable, but the slider is still moving.")]
		private Color _waitingColor = new Color(0.64f, 0.62f, 0.58f);

		[SerializeField, Tooltip("Ready to be pressed: the slider rests on a notch.")]
		private Color _armedColor = new Color(0.95f, 0.76f, 0.19f);

		[SerializeField] private Color _lockedColor = new Color(0.27f, 0.94f, 0.54f);

		[SerializeField, Tooltip("The ring's segments that aren't lit.")]
		private Color _ringIdleColor = new Color(0.24f, 0.25f, 0.28f);

		#endregion

		#region Fields

		// Below this the samples along a bone would be too many to test every frame
		private const float MinSampleSpacingMeters = 0.002f;

		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

		private MaterialPropertyBlock _propertyBlock;
		private NeighborhoodPushCap _pushCap;
		private bool _isAvailable = true;
		private float _progress;

		#endregion

		#region Properties

		/// <summary>
		/// Whether the button can be used. Off greys it, keeps its cap up and empties its ring; a lock already made stays.
		/// The experience switches it off until its turn, and while the session is paused.
		/// </summary>
		public bool IsAvailable
		{
			get => _isAvailable;
			set => _isAvailable = value;
		}

		public bool IsLocked { get; private set; }

		/// <summary>The notch locked in, or -1 before a lock.</summary>
		public int LockedDetent { get; private set; } = -1;

		/// <summary>How full the ring is, from 0 to 1.</summary>
		public float Progress => IsLocked ? 1f : _progress;

		/// <summary>How far the cap is down, from 0 (up) to 1 (all the way).</summary>
		public float PressDepth01 => _pushCap?.Depth01 ?? 0f;

		/// <summary>The cap is pushed far enough down to count as pressed.</summary>
		public bool IsPressed => _pushCap != null && _pushCap.IsPressed;

		/// <summary>Usable, not locked yet, and the slider rests on a notch without a hand on it.</summary>
		public bool IsArmed => _isAvailable && !IsLocked && _slider != null && _slider.Detent >= 0
			&& !_slider.IsDragging && _slider.Contact != NeighborhoodSliderContact.Pushed
			&& _slider.Contact != NeighborhoodSliderContact.Grabbed;

		#endregion

		#region Setup

		private void Awake()
		{
			_propertyBlock = new MaterialPropertyBlock();
			_pushCap = new NeighborhoodPushCap(_travelMeters, _push);
			ApplyCapHeight();
		}

		#endregion

		#region Update

		private void Update()
		{
			float deltaSeconds = Time.deltaTime;
			if (deltaSeconds > 0f)
			{
				bool wasTouched = _pushCap.IsTouched;
				bool wasPressed = _pushCap.IsPressed;
				_pushCap.Spring(deltaSeconds);
				bool isPushable = _isAvailable && !IsLocked;
				_pushCap.ApplyHand(isPushable
					? FindLowestHandUnderside(_pushCap.LowestCountedMeters)
					: float.PositiveInfinity);
				PlayChanges(wasTouched, wasPressed);
				UpdateProgress(deltaSeconds);
			}

			ApplyLook();
		}

		#endregion

		#region Public

		/// <summary>Unlocks, so the button can lock in a choice again; the cap springs back up.</summary>
		public void ResetLock()
		{
			IsLocked = false;
			LockedDetent = -1;
			_progress = 0f;
			_pushCap?.Unlatch();
		}

		#endregion

		#region Internal

		internal void Configure(NeighborhoodSlider slider, NeighborhoodHandBones handBones,
			NeighborhoodPushSettings settings, Transform cap, Renderer capRenderer, Renderer[] ringSegments,
			AudioSource audioSource, AudioClip touchClip, AudioClip pressClip, AudioClip releaseClip, AudioClip lockInClip)
		{
			_slider = slider;
			_handBones = handBones;
			_settings = settings;
			_cap = cap;
			_capRenderer = capRenderer;
			_ringSegments = ringSegments;
			_audioSource = audioSource;
			_touchClip = touchClip;
			_pressClip = pressClip;
			_releaseClip = releaseClip;
			_lockInClip = lockInClip;
		}

		internal void ConfigureShape(float capRadiusMeters, float pressedTopMeters, float travelMeters)
		{
			_capRadiusMeters = capRadiusMeters;
			_pressedTopMeters = pressedTopMeters;
			_travelMeters = travelMeters;
		}

		#endregion

		#region Private

		private void UpdateProgress(float deltaSeconds)
		{
			if (IsLocked)
			{
				return;
			}

			NeighborhoodPushSettings settings = NeighborhoodPushSettings.OrDefaults(_settings);
			bool isHolding = IsArmed && _pushCap.IsPressed;
			_progress = isHolding
				? _progress + deltaSeconds / settings.LockHoldSeconds
				: _progress - deltaSeconds / settings.LockDrainSeconds;
			_progress = Mathf.Clamp01(_progress);
			if (_progress >= 1f)
			{
				Lock();
			}
		}

		private void Lock()
		{
			IsLocked = true;
			LockedDetent = _slider.Detent;
			_pushCap.Latch();
			Play(_lockInClip);
			OnLockIn?.Invoke(LockedDetent);
		}

		private void PlayChanges(bool wasTouched, bool wasPressed)
		{
			if (!wasTouched && _pushCap.IsTouched)
			{
				Play(_touchClip);
			}

			if (!wasPressed && _pushCap.IsPressed)
			{
				Play(_pressClip);
			}
			else if (wasPressed && !_pushCap.IsPressed)
			{
				Play(_releaseClip);
			}
		}

		// The lowest underside of any tracked hand over the cap's face that may push it now, in the cap's heights (0 at the
		// bottom of its travel); positive infinity when none does. Spheres are sampled along every hand bone, as the
		// Scripted Button Stepper Test does, in this object's frame without its scale (the table model may be scaled).
		private float FindLowestHandUnderside(float lowestCountedMeters)
		{
			if (_handBones == null)
			{
				return float.PositiveInfinity;
			}

			_handBones.Refresh();
			NeighborhoodPushSettings settings = NeighborhoodPushSettings.OrDefaults(_settings);
			float spacingMeters = Mathf.Max(MinSampleSpacingMeters, settings.SampleSpacingMeters);
			float radiusSquared = _capRadiusMeters * _capRadiusMeters;
			Quaternion toLocal = Quaternion.Inverse(transform.rotation);
			Vector3 origin = transform.position;
			float lowestMeters = float.PositiveInfinity;
			foreach (NeighborhoodHandBone bone in _handBones.Bones)
			{
				Vector3 start = toLocal * (bone.Start - origin);
				Vector3 end = toLocal * (bone.End - origin);
				int segmentCount = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / spacingMeters));
				for (int sample = 0; sample <= segmentCount; sample++)
				{
					Vector3 point = Vector3.Lerp(start, end, (float)sample / segmentCount);
					float undersideMeters = point.y - bone.RadiusMeters - _pressedTopMeters;
					if (point.x * point.x + point.z * point.z > radiusSquared || undersideMeters < lowestCountedMeters)
					{
						continue;
					}

					lowestMeters = Mathf.Min(lowestMeters, undersideMeters);
				}
			}

			return lowestMeters;
		}

		private void ApplyLook()
		{
			bool isArmed = IsArmed;
			Color capColor;
			if (IsLocked)
			{
				capColor = _lockedColor;
			}
			else if (!_isAvailable)
			{
				capColor = _unavailableColor;
			}
			else
			{
				capColor = isArmed ? Color.Lerp(_armedColor, _lockedColor, _progress) : _waitingColor;
			}

			SetColor(_capRenderer, capColor);
			ApplyCapHeight();

			// The segments light up one after another as the ring fills, the one being reached fading in
			float litSegments = Progress * _ringSegments.Length;
			Color litColor = IsLocked ? _lockedColor : _armedColor;
			for (int segment = 0; segment < _ringSegments.Length; segment++)
			{
				float fill = isArmed || IsLocked ? Mathf.Clamp01(litSegments - segment) : 0f;
				SetColor(_ringSegments[segment], Color.Lerp(_ringIdleColor, litColor, fill));
			}
		}

		private void ApplyCapHeight()
		{
			if (_cap != null && _pushCap != null)
			{
				_cap.localPosition = new Vector3(0f, _pressedTopMeters + _pushCap.HeightMeters, 0f);
			}
		}

		private void SetColor(Renderer target, Color color)
		{
			if (target == null)
			{
				return;
			}

			_propertyBlock.SetColor(BaseColorId, color);
			target.SetPropertyBlock(_propertyBlock);
		}

		private void Play(AudioClip clip)
		{
			if (_audioSource != null && clip != null)
			{
				_audioSource.PlayOneShot(clip);
			}
		}

		#endregion
	}
}
