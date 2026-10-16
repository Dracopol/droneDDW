using System;
using TMPro;
using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// The table's lock-in button, beside the slider: a push button like the Scripted Button Stepper Test's. Any part of a
	/// hand pushes its cap down; the cap follows the hand and springs back up once it lets go
	/// (<see cref="NeighborhoodPushCap"/>). Holding it pressed counts down above the button with a tick each second and
	/// lights the ring of segments around the cap one by one; once the ring is full the slider's choice is locked in. A
	/// press only counts while the button is armed (available, and the slider resting on a notch without a hand on it), so
	/// reaching for the slider can't lock in by accident; letting go early empties the ring again. The cap only moves while
	/// the button is available, and once locked it stays down and green until <see cref="ResetLock"/>.
	/// The cap is grey while it can't be pressed and a steady green while it can (and once locked); a flash runs around
	/// the ring when it locks in. Pushed by the slider's hand bones
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

		[SerializeField, Tooltip("The seconds left, shown above the button while it is held; it turns to face the viewer.")]
		private TMP_Text _countdown;

		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _touchClip;
		[SerializeField] private AudioClip _pressClip;
		[SerializeField] private AudioClip _releaseClip;
		[SerializeField] private AudioClip _lockInClip;

		[SerializeField, Tooltip("The countdown's ticks, for 1, 2, 3 ... seconds left (the last one also for more).")]
		private AudioClip[] _countdownClips = Array.Empty<AudioClip>();

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

		[SerializeField, Tooltip("Usable, but the slider is still moving or a hand is on it: it can't be pressed yet.")]
		private Color _waitingColor = new Color(0.29f, 0.31f, 0.34f);

		[SerializeField, Tooltip("Ready to be pressed: the slider rests on a notch.")]
		private Color _armedColor = new Color(0.27f, 0.94f, 0.54f);

		[SerializeField] private Color _lockedColor = new Color(0.27f, 0.94f, 0.54f);

		[SerializeField, Tooltip("The ring's segments that aren't lit.")]
		private Color _ringIdleColor = new Color(0.24f, 0.25f, 0.28f);

		[Header("Glow")]
		[SerializeField, Min(0f), Tooltip("How brightly the lit ring segments glow.")]
		private float _ringGlow = 1.6f;

		[SerializeField, Min(0.05f), Tooltip("How fast the armed button breathes, in breaths per second.")]
		private float _readyPulseHertz = 0.6f;

		[SerializeField, Min(0f), Tooltip("How brightly the armed button glows at the top of a breath. 0: a steady colour.")]
		private float _readyPulseGlow;

		[SerializeField, Min(0f), Tooltip("How brightly the cap glows while a finger is close above it. 0: no change.")]
		private float _hoverGlow;

		[SerializeField, Tooltip("The highlight while the button is being explained: the slider handle's glow colour.")]
		private Color _hintColor = new Color(0.95f, 0.76f, 0.19f);

		[SerializeField, Min(0f), Tooltip("How brightly the cap and ring glow at the top of a highlight pulse.")]
		private float _hintGlow = 1.2f;

		[SerializeField, Min(0.05f), Tooltip("Highlight pulses per second.")]
		private float _hintPulseHertz = 1f;

		[SerializeField, Min(0f), Tooltip("How far above the cap's top a finger counts as close.")]
		private float _hoverRangeMeters = 0.06f;

		[SerializeField, Min(0f), Tooltip("How far a ring segment rises as it lights up.")]
		private float _segmentLiftMeters = 0.0015f;

		[SerializeField, Min(0.05f), Tooltip("How long the flash around the ring takes once the button locks in.")]
		private float _lockFlashSeconds = 0.6f;

		[SerializeField, Min(0.05f), Tooltip("How long each countdown number pops in.")]
		private float _countdownPopSeconds = 0.25f;

		#endregion

		#region Fields

		// Below this the samples along a bone would be too many to test every frame
		private const float MinSampleSpacingMeters = 0.002f;

		// How much bigger a countdown number starts, and how much the cap swells when it locks in
		private const float CountdownPopScale = 0.35f;
		private const float LockBounceScale = 0.12f;
		private const float LockBounceSeconds = 0.3f;

		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

		private MaterialPropertyBlock _propertyBlock;
		private NeighborhoodPushCap _pushCap;
		private Vector3 _capScale = Vector3.one;
		private Vector3 _countdownScale = Vector3.one;
		private bool _isAvailable = true;
		private bool _isHovered;
		private float _progress;
		private int _shownCount;
		private float _countdownShownAtSeconds;
		private float _lockedAtSeconds = float.NegativeInfinity;

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

		/// <summary>
		/// Pulses the cap and ring orange, like the slider handle, until the button is pressed: the practice turns it on
		/// while it explains the button.
		/// </summary>
		public bool IsHinted { get; set; }

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

		/// <summary>The whole seconds left on the countdown while the button is held; 0 when it isn't counting down.</summary>
		public int CountdownSeconds => _shownCount;

		#endregion

		#region Setup

		private void Awake()
		{
			_propertyBlock = new MaterialPropertyBlock();
			_pushCap = new NeighborhoodPushCap(_travelMeters, _push);
			if (_cap != null)
			{
				_capScale = _cap.localScale;
			}

			if (_countdown != null)
			{
				_countdownScale = _countdown.transform.localScale;
				_countdown.enabled = false;
			}

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
				_isHovered = false;
				_pushCap.ApplyHand(isPushable
					? FindLowestHandUnderside(_pushCap.LowestCountedMeters, out _isHovered)
					: float.PositiveInfinity);
				PlayChanges(wasTouched, wasPressed);
				UpdateProgress(deltaSeconds);
				UpdateCountdown();
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
			_lockedAtSeconds = float.NegativeInfinity;
			_pushCap?.Unlatch();
		}

		#endregion

		#region Internal

		internal void Configure(NeighborhoodSlider slider, NeighborhoodHandBones handBones,
			NeighborhoodPushSettings settings, Transform cap, Renderer capRenderer, Renderer[] ringSegments,
			TMP_Text countdown, AudioSource audioSource)
		{
			_slider = slider;
			_handBones = handBones;
			_settings = settings;
			_cap = cap;
			_capRenderer = capRenderer;
			_ringSegments = ringSegments;
			_countdown = countdown;
			_audioSource = audioSource;
		}

		internal void ConfigureSounds(AudioClip touchClip, AudioClip pressClip, AudioClip releaseClip,
			AudioClip lockInClip, AudioClip[] countdownClips)
		{
			_touchClip = touchClip;
			_pressClip = pressClip;
			_releaseClip = releaseClip;
			_lockInClip = lockInClip;
			_countdownClips = countdownClips;
		}

		internal void ConfigureShape(float capRadiusMeters, float pressedTopMeters, float travelMeters)
		{
			_capRadiusMeters = capRadiusMeters;
			_pressedTopMeters = pressedTopMeters;
			_travelMeters = travelMeters;
		}

		/// <summary>The whole seconds left to hold, from 1 up: 3, 2, 1 for a three second hold.</summary>
		internal static int GetCountdown(float progress, float holdSeconds)
		{
			const float roundingMargin = 0.0001f;
			return Mathf.Max(1, Mathf.CeilToInt((1f - Mathf.Clamp01(progress)) * holdSeconds - roundingMargin));
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

		// A tick with each new number; the number disappears when the hand lets go or the button locks in
		private void UpdateCountdown()
		{
			bool isHolding = !IsLocked && IsArmed && _pushCap.IsPressed;
			int count = isHolding
				? GetCountdown(_progress, NeighborhoodPushSettings.OrDefaults(_settings).LockHoldSeconds)
				: 0;
			if (count == _shownCount)
			{
				return;
			}

			_shownCount = count;
			if (count > 0)
			{
				_countdownShownAtSeconds = Time.time;
				Play(_countdownClips.Length > 0 ? _countdownClips[Mathf.Min(count, _countdownClips.Length) - 1] : null);
			}
		}

		private void Lock()
		{
			IsLocked = true;
			LockedDetent = _slider.Detent;
			_lockedAtSeconds = Time.time;
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
		// Also tells whether a hand is close above the cap.
		private float FindLowestHandUnderside(float lowestCountedMeters, out bool isHovered)
		{
			isHovered = false;
			if (_handBones == null)
			{
				return float.PositiveInfinity;
			}

			_handBones.Refresh();
			NeighborhoodPushSettings settings = NeighborhoodPushSettings.OrDefaults(_settings);
			float spacingMeters = Mathf.Max(MinSampleSpacingMeters, settings.SampleSpacingMeters);
			float radiusSquared = _capRadiusMeters * _capRadiusMeters;
			float capTopMeters = _pushCap.HeightMeters;
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
					if (point.x * point.x + point.z * point.z > radiusSquared)
					{
						continue;
					}

					float undersideMeters = point.y - bone.RadiusMeters - _pressedTopMeters;
					float aboveCapMeters = undersideMeters - capTopMeters;
					isHovered |= aboveCapMeters >= -_push.ContactToleranceMeters && aboveCapMeters <= _hoverRangeMeters;
					if (undersideMeters >= lowestCountedMeters)
					{
						lowestMeters = Mathf.Min(lowestMeters, undersideMeters);
					}
				}
			}

			return lowestMeters;
		}

		private void ApplyLook()
		{
			bool isArmed = IsArmed;
			float now = Time.time;

			// A slow breath while the armed button waits for a press
			bool isWaitingForPress = isArmed && _progress <= 0f && !_pushCap.IsPressed;
			float breath = isWaitingForPress
				? _readyPulseGlow * (0.5f + 0.5f * Mathf.Sin(now * 2f * Mathf.PI * _readyPulseHertz))
				: 0f;
			float lockAge = now - _lockedAtSeconds;
			float flash = lockAge >= 0f && lockAge < _lockFlashSeconds ? 1f - lockAge / _lockFlashSeconds : 0f;

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

			float hint = IsHinted && !IsLocked && _progress <= 0f
				? 0.5f + 0.5f * Mathf.Sin(now * 2f * Mathf.PI * _hintPulseHertz)
				: 0f;
			Color hintGlow = _hintColor * (_hintGlow * hint);
			float capGlow = Mathf.Max(breath, _isHovered && _isAvailable && !IsLocked ? _hoverGlow : 0f, flash);
			SetLook(_capRenderer, Color.Lerp(capColor, _hintColor, hint * 0.5f), capColor * capGlow + hintGlow);
			ApplyCapHeight();
			ApplyLockBounce(lockAge);

			// The segments light up one after another as the ring fills, the one being reached fading in; once locked a
			// flash runs once around the ring
			int segmentCount = _ringSegments.Length;
			float litSegments = Progress * segmentCount;
			float flashPosition = lockAge / _lockFlashSeconds * (segmentCount + 2f);
			Color litColor = IsLocked ? _lockedColor : _armedColor;
			for (int segment = 0; segment < segmentCount; segment++)
			{
				Renderer segmentRenderer = _ringSegments[segment];
				if (segmentRenderer == null)
				{
					continue;
				}

				float fill = isArmed || IsLocked ? Mathf.Clamp01(litSegments - segment) : 0f;
				float wave = flash > 0f ? Mathf.Clamp01(1f - Mathf.Abs(flashPosition - segment) * 0.5f) : 0f;
				Color color = Color.Lerp(Color.Lerp(_ringIdleColor, _hintColor, hint), litColor, fill);
				Color glow = litColor * (_ringGlow * fill + breath * 0.5f) + Color.white * wave + hintGlow;
				SetLook(segmentRenderer, color, glow);
				segmentRenderer.transform.localPosition = Vector3.up * (_segmentLiftMeters * fill);
			}

			ApplyCountdownLook(now);
		}

		// The cap swells for a moment as the button locks in
		private void ApplyLockBounce(float lockAge)
		{
			if (_cap == null)
			{
				return;
			}

			float swell = lockAge >= 0f && lockAge < LockBounceSeconds
				? LockBounceScale * Mathf.Sin(Mathf.PI * lockAge / LockBounceSeconds)
				: 0f;
			_cap.localScale = new Vector3(_capScale.x * (1f + swell), _capScale.y, _capScale.z * (1f + swell));
		}

		// Each number pops in and the text turns to face whoever looks at it
		private void ApplyCountdownLook(float now)
		{
			if (_countdown == null)
			{
				return;
			}

			_countdown.enabled = _shownCount > 0;
			if (_shownCount <= 0)
			{
				return;
			}

			_countdown.text = _shownCount.ToString();
			float popAge = (now - _countdownShownAtSeconds) / _countdownPopSeconds;
			float pop = popAge < 1f ? CountdownPopScale * (1f - popAge) * (1f - popAge) : 0f;
			Transform countdownTransform = _countdown.transform;
			countdownTransform.localScale = _countdownScale * (1f + pop);
			Camera viewer = Camera.main;
			if (viewer != null)
			{
				Vector3 away = countdownTransform.position - viewer.transform.position;
				away.y = 0f;
				if (away.sqrMagnitude > 0.0001f)
				{
					countdownTransform.rotation = Quaternion.LookRotation(away, Vector3.up);
				}
			}
		}

		private void ApplyCapHeight()
		{
			if (_cap != null && _pushCap != null)
			{
				_cap.localPosition = new Vector3(0f, _pressedTopMeters + _pushCap.HeightMeters, 0f);
			}
		}

		private void SetLook(Renderer target, Color color, Color glow)
		{
			if (target == null)
			{
				return;
			}

			_propertyBlock.SetColor(BaseColorId, color);
			_propertyBlock.SetColor(EmissionColorId, glow);
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
