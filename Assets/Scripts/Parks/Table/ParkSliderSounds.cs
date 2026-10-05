using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// The slider's sounds, standing in for the haptics hands don't have: a click on every detent, a touch when a
	/// hand starts pushing, a lower click when the fingers close into a grab, a release when the hand lets go, Lock In
	/// when the park's level really changes (a nudge that settles back on the same detent only plays the release),
	/// and a scrape that follows the handle's speed. The same click on every detent, so no sound hints at a side.
	/// Leave a clip empty to switch that sound off.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Slider Sounds")]
	public sealed class ParkSliderSounds : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkLevelSlider _slider;

		[SerializeField, Tooltip("Lock In plays when its level changes.")]
		private ParkLevelController _controller;

		[Header("Sources")]
		[SerializeField] private AudioSource _notchSource;
		[SerializeField] private AudioSource _handSource;
		[SerializeField] private AudioSource _commitSource;

		[SerializeField, Tooltip("Loops the scrape clip.")]
		private AudioSource _scrapeSource;

		[Header("Clips")]
		[SerializeField] private AudioClip _notchClip;
		[SerializeField] private AudioClip _touchClip;
		[SerializeField] private AudioClip _grabClip;
		[SerializeField] private AudioClip _releaseClip;
		[SerializeField] private AudioClip _commitClip;
		[SerializeField] private AudioClip _scrapeClip;

		[Header("Volumes")]
		[SerializeField, Range(0f, 1f)] private float _notchVolume = 0.6f;
		[SerializeField, Range(0f, 1f)] private float _touchVolume = 0.5f;
		[SerializeField, Range(0f, 1f)] private float _grabVolume = 0.7f;
		[SerializeField, Range(0f, 1f)] private float _releaseVolume = 0.5f;
		[SerializeField, Range(0f, 1f)] private float _commitVolume = 0.8f;

		[Header("Hand")]
		[SerializeField, Range(0.1f, 3f), Tooltip("The grab clip plays at this pitch, a lower click than a detent's.")]
		private float _grabPitch = 0.7f;

		[SerializeField, Min(0f), Tooltip("How long the hand must be off the handle before the release plays, "
			+ "so tracking flicker at the edge of a touch doesn't chatter.")]
		private float _releaseDebounceSeconds = 0.08f;

		[Header("Scrape")]
		[SerializeField, Range(0f, 1f)] private float _scrapeVolume = 0.35f;

		[SerializeField, Min(0.01f), Tooltip("Handle speed at which the scrape is at full volume and top pitch.")]
		private float _scrapeFullSpeedMetersPerSecond = 0.4f;

		[SerializeField, Tooltip("Scrape pitch at rest and at full speed.")]
		private Vector2 _scrapePitchRange = new Vector2(0.85f, 1.15f);

		[SerializeField, Min(0f), Tooltip("How quickly the scrape follows the speed. 0 = no smoothing.")]
		private float _scrapeSmoothing = 12f;

		#endregion

		#region Fields

		private bool _isHeld;
		private bool _isGrabbed;
		private float _lastTouchTime;
		private float _lastHandleX;
		private float _scrapeLevel;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_slider.OnDetentChange += HandleDetentChange;
			if (_controller != null)
			{
				_controller.OnLevelChange += HandleLevelChange;
			}
		}

		private void OnDisable()
		{
			_slider.OnDetentChange -= HandleDetentChange;
			if (_controller != null)
			{
				_controller.OnLevelChange -= HandleLevelChange;
			}
		}

		private void Start()
		{
			_lastHandleX = _slider.Handle.localPosition.x;
			if (_scrapeSource == null || _scrapeClip == null)
			{
				return;
			}

			_scrapeSource.clip = _scrapeClip;
			_scrapeSource.loop = true;
			_scrapeSource.volume = 0f;
			_scrapeSource.Play();
		}

		#endregion

		#region Update

		private void Update()
		{
			UpdateHand();
			UpdateScrape();
		}

		#endregion

		#region Event handlers

		private void HandleDetentChange(int detent)
		{
			Play(_notchSource, _notchClip, _notchVolume, 1f);
		}

		private void HandleLevelChange(ParkLevelChange change)
		{
			if (!change.IsImmediate && change.Previous != change.Next)
			{
				Play(_commitSource, _commitClip, _commitVolume, 1f);
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLevelSlider slider, ParkLevelController controller, AudioSource notchSource,
			AudioSource handSource, AudioSource commitSource, AudioSource scrapeSource)
		{
			_slider = slider;
			_controller = controller;
			_notchSource = notchSource;
			_handSource = handSource;
			_commitSource = commitSource;
			_scrapeSource = scrapeSource;
		}

		internal void ConfigureClips(AudioClip notchClip, AudioClip touchClip, AudioClip grabClip,
			AudioClip releaseClip, AudioClip commitClip, AudioClip scrapeClip)
		{
			_notchClip = notchClip;
			_touchClip = touchClip;
			_grabClip = grabClip;
			_releaseClip = releaseClip;
			_commitClip = commitClip;
			_scrapeClip = scrapeClip;
		}

		#endregion

		#region Private

		/// <summary>
		/// Touch when a hand starts pushing, the grab click when the fingers close (after a push or straight away),
		/// and the release once no hand has touched the handle for the debounce time.
		/// </summary>
		private void UpdateHand()
		{
			ParkSliderContact contact = _slider.Contact;
			bool isGrabbed = contact == ParkSliderContact.Grabbed;
			if (isGrabbed || contact == ParkSliderContact.Pushed)
			{
				if (isGrabbed && !_isGrabbed)
				{
					Play(_handSource, _grabClip, _grabVolume, _grabPitch);
				}
				else if (!_isHeld && !isGrabbed)
				{
					Play(_handSource, _touchClip, _touchVolume, 1f);
				}

				_isHeld = true;
				_isGrabbed = isGrabbed;
				_lastTouchTime = Time.time;
				return;
			}

			if (!_isHeld || Time.time - _lastTouchTime < _releaseDebounceSeconds)
			{
				return;
			}

			_isHeld = false;
			_isGrabbed = false;
			Play(_handSource, _releaseClip, _releaseVolume, 1f);
		}

		/// <summary>
		/// Follows the handle's speed along the track, in the slider's space, so the board gliding after the head (which
		/// carries the handle through the room) makes no scrape.
		/// </summary>
		private void UpdateScrape()
		{
			float handleX = _slider.Handle.localPosition.x;
			float deltaTime = Time.deltaTime;
			float speedMetersPerSecond = deltaTime > 0f ? Mathf.Abs(handleX - _lastHandleX) / deltaTime : 0f;
			_lastHandleX = handleX;
			if (_scrapeSource == null || _scrapeClip == null)
			{
				return;
			}

			float target = Mathf.Clamp01(speedMetersPerSecond / _scrapeFullSpeedMetersPerSecond);
			_scrapeLevel = _scrapeSmoothing > 0f
				? Mathf.Lerp(_scrapeLevel, target, 1f - Mathf.Exp(-_scrapeSmoothing * deltaTime))
				: target;
			_scrapeSource.volume = _scrapeLevel * _scrapeVolume;
			_scrapeSource.pitch = Mathf.Lerp(_scrapePitchRange.x, _scrapePitchRange.y, _scrapeLevel);
		}

		private static void Play(AudioSource source, AudioClip clip, float volume, float pitch)
		{
			if (source == null || clip == null || volume <= 0f)
			{
				return;
			}

			source.pitch = pitch;
			source.PlayOneShot(clip, volume);
		}

		#endregion
	}
}
