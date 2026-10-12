using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// How the slider shows what it is doing, for people new to VR. When its hand input is switched on and nobody has
	/// touched it yet, the handle breathes a soft glow; a hand coming close brightens it, and pushing or grabbing it
	/// brightens it more. The three notch markers glow, the one the slider is on (or will land on, while it moves)
	/// brightest, and moving it past a notch ticks. Shared by the Neighborhood's table and the experience's practice table.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Slider Look")]
	public sealed class NeighborhoodSliderLook : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private NeighborhoodSlider _slider;

		[SerializeField, Tooltip("The slider's hand input: the handle waits for a touch each time this is switched on.")]
		private Behaviour _handInput;

		[SerializeField] private Renderer _handleRenderer;

		[SerializeField, Tooltip("The notch markers, in the slider's notch order.")]
		private Renderer[] _notchRenderers = Array.Empty<Renderer>();

		[SerializeField] private AudioSource _audioSource;

		[SerializeField, Tooltip("Played when a moving handle reaches another notch.")]
		private AudioClip _notchClip;

		[Header("Colours")]
		[SerializeField] private Color _glowColor = new Color(0.95f, 0.76f, 0.19f);

		[SerializeField, Min(0.05f), Tooltip("How fast the waiting handle breathes, in breaths per second.")]
		private float _readyPulseHertz = 0.6f;

		[SerializeField, Min(0f)] private float _readyPulseGlow = 0.5f;

		[SerializeField, Min(0f), Tooltip("How brightly the handle glows while a hand is close.")]
		private float _nearGlow = 0.3f;

		[SerializeField, Min(0f), Tooltip("How brightly the handle glows while it is pushed or grabbed.")]
		private float _heldGlow = 0.7f;

		[SerializeField, Min(0f), Tooltip("How brightly the notch the slider is on glows.")]
		private float _currentNotchGlow = 1.2f;

		[SerializeField, Min(0f), Tooltip("How brightly the other notches glow.")]
		private float _otherNotchGlow = 0.12f;

		#endregion

		#region Fields

		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

		private MaterialPropertyBlock _propertyBlock;
		private bool _wasInputEnabled;
		private bool _isWaitingForTouch;

		#endregion

		#region Setup

		private void Awake()
		{
			_propertyBlock = new MaterialPropertyBlock();
		}

		private void OnEnable()
		{
			_slider.OnDetentChange += HandleDetentChange;
		}

		private void OnDisable()
		{
			_slider.OnDetentChange -= HandleDetentChange;
		}

		#endregion

		#region Update

		private void Update()
		{
			bool isInputEnabled = _handInput == null || _handInput.enabled;
			if (isInputEnabled && !_wasInputEnabled)
			{
				_isWaitingForTouch = true;
			}

			_wasInputEnabled = isInputEnabled;
			NeighborhoodSliderContact contact = _slider.Contact;
			bool isHeld = contact == NeighborhoodSliderContact.Pushed || contact == NeighborhoodSliderContact.Grabbed;
			if (isHeld)
			{
				_isWaitingForTouch = false;
			}

			float glow = 0f;
			if (isInputEnabled)
			{
				float breath = _isWaitingForTouch
					? _readyPulseGlow * (0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI * _readyPulseHertz))
					: 0f;
				float touch = isHeld ? _heldGlow : contact == NeighborhoodSliderContact.InReach ? _nearGlow : 0f;
				glow = Mathf.Max(breath, touch);
			}

			SetGlow(_handleRenderer, _glowColor * glow);
			for (int notch = 0; notch < _notchRenderers.Length; notch++)
			{
				float notchGlow = notch == _slider.Detent ? _currentNotchGlow : _otherNotchGlow;
				SetGlow(_notchRenderers[notch], _glowColor * (isInputEnabled ? notchGlow : _otherNotchGlow));
			}
		}

		#endregion

		#region Event handlers

		private void HandleDetentChange(int detent)
		{
			if (_slider.IsDragging && _audioSource != null && _notchClip != null)
			{
				_audioSource.PlayOneShot(_notchClip);
			}
		}

		#endregion

		#region Internal

		internal void Configure(NeighborhoodSlider slider, Behaviour handInput, Renderer handleRenderer,
			Renderer[] notchRenderers, AudioSource audioSource, AudioClip notchClip)
		{
			_slider = slider;
			_handInput = handInput;
			_handleRenderer = handleRenderer;
			_notchRenderers = notchRenderers;
			_audioSource = audioSource;
			_notchClip = notchClip;
		}

		#endregion

		#region Private

		private void SetGlow(Renderer target, Color glow)
		{
			if (target == null)
			{
				return;
			}

			_propertyBlock.SetColor(EmissionColorId, glow);
			target.SetPropertyBlock(_propertyBlock);
		}

		#endregion
	}
}
