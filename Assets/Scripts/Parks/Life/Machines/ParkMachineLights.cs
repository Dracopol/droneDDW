using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// The lights of the smart infrastructure. Their props use Park_Mat_Props_Lights, whose emission map lights only
	/// the palette's lamp, LED and screen colours, and this sets each renderer's emission strength: beacons blink,
	/// sensor heads pulse, chargers breathe, drone pads flash as drones take off and land, irrigation controllers blink
	/// while their sprinklers run, and screens, lamps, bins and docks glow steadily. Steady glows are set once;
	/// animated ones are only written when they change.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Machine Lights")]
	public sealed class ParkMachineLights : MonoBehaviour
	{
		#region Events

		/// <summary>A beacon's flash starts (its index), for the tick sound.</summary>
		public event Action<int> OnBeaconBlink;

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLife _life;
		[SerializeField] private Renderer[] _beacons = Array.Empty<Renderer>();
		[SerializeField] private Renderer[] _sensors = Array.Empty<Renderer>();
		[SerializeField] private Renderer[] _chargers = Array.Empty<Renderer>();

		[SerializeField, Tooltip("Drone pads, in the same order as the drone fleet's pads.")]
		private Renderer[] _pads = Array.Empty<Renderer>();

		[SerializeField, Tooltip("Irrigation controllers, in the order the sprinklers refer to them.")]
		private Renderer[] _controllers = Array.Empty<Renderer>();

		[SerializeField, Tooltip("Steady glows: screens (totems, kiosk, lockers, robots).")]
		private Renderer[] _screens = Array.Empty<Renderer>();

		[SerializeField] private Renderer[] _lamps = Array.Empty<Renderer>();
		[SerializeField] private Renderer[] _bins = Array.Empty<Renderer>();
		[SerializeField] private Renderer[] _docks = Array.Empty<Renderer>();

		#endregion

		#region Fields

		private const float FlashShare = 0.12f;
		private const float DimGlow = 0.05f;
		private const float PadPulseSeconds = 1.2f;
		private const float ControllerBlinkHertz = 2f;
		private const float ChangeThreshold = 0.01f;

		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

		private MaterialPropertyBlock _block;
		private float[] _applied = Array.Empty<float>();
		private Renderer[] _animated = Array.Empty<Renderer>();
		private int[] _beaconFlashes = Array.Empty<int>();
		private float[] _padPulseTimes = Array.Empty<float>();
		private bool[] _isControllerActive = Array.Empty<bool>();

		#endregion

		#region Setup

		private void Awake()
		{
			_block = new MaterialPropertyBlock();
			_beaconFlashes = new int[_beacons.Length];
			_padPulseTimes = new float[_pads.Length];
			for (int i = 0; i < _padPulseTimes.Length; i++)
			{
				_padPulseTimes[i] = float.NegativeInfinity;
			}

			_isControllerActive = new bool[_controllers.Length];
			_animated = Concat(_beacons, _sensors, _chargers, _pads, _controllers);
			_applied = new float[_animated.Length];
			for (int i = 0; i < _applied.Length; i++)
			{
				_applied[i] = -1f;
			}
		}

		private void Start()
		{
			ParkMachineSettings settings = _life.Settings.Machines;
			ApplySteady(_screens, settings.ScreenGlow);
			ApplySteady(_docks, settings.ScreenGlow);
			ApplySteady(_lamps, settings.LampGlow);
			ApplySteady(_bins, settings.BinGlow);
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkMachineSettings settings = _life.Settings.Machines;
			float time = Time.time;
			int index = 0;
			for (int i = 0; i < _beacons.Length; i++, index++)
			{
				// Each beacon flashes on its own beat, offset by its index so they ripple rather than blink together
				float beat = time / settings.BeaconBlinkSeconds + i * 0.37f;
				int flash = Mathf.FloorToInt(beat);
				bool isOn = beat - flash < FlashShare;
				if (isOn && flash != _beaconFlashes[i] && IsShown(_beacons[i]))
				{
					_beaconFlashes[i] = flash;
					OnBeaconBlink?.Invoke(i);
				}

				Apply(index, isOn ? settings.BeaconGlow : DimGlow);
			}

			for (int i = 0; i < _sensors.Length; i++, index++)
			{
				float wave = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (time * 0.25f + i * 0.21f));
				Apply(index, Mathf.Lerp(settings.SensorGlow.x, settings.SensorGlow.y, wave));
			}

			for (int i = 0; i < _chargers.Length; i++, index++)
			{
				float breath = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (time * 0.5f + i * 0.13f));
				Apply(index, Mathf.Lerp(settings.ChargerGlow.x, settings.ChargerGlow.y, breath * breath));
			}

			for (int i = 0; i < _pads.Length; i++, index++)
			{
				float pulse = Mathf.Exp(-(time - _padPulseTimes[i]) / PadPulseSeconds * 3f);
				Apply(index, Mathf.Lerp(settings.PadGlow, settings.PadPulseGlow, pulse));
			}

			for (int i = 0; i < _controllers.Length; i++, index++)
			{
				bool isBlinkOn = _isControllerActive[i] && Mathf.Repeat(time * ControllerBlinkHertz, 1f) < 0.5f;
				Apply(index, isBlinkOn ? settings.ScreenGlow * 2f : settings.ScreenGlow * 0.4f);
			}
		}

		#endregion

		#region Internal

		internal void PulsePad(int pad)
		{
			if (pad >= 0 && pad < _padPulseTimes.Length)
			{
				_padPulseTimes[pad] = Time.time;
			}
		}

		internal void SetControllerActive(int controller, bool isActive)
		{
			if (controller >= 0 && controller < _isControllerActive.Length)
			{
				_isControllerActive[controller] = isActive;
			}
		}

		internal void Configure(ParkLife life, Renderer[] beacons, Renderer[] sensors, Renderer[] chargers, Renderer[] pads,
			Renderer[] controllers, Renderer[] screens, Renderer[] lamps, Renderer[] bins, Renderer[] docks)
		{
			_life = life;
			_beacons = beacons;
			_sensors = sensors;
			_chargers = chargers;
			_pads = pads;
			_controllers = controllers;
			_screens = screens;
			_lamps = lamps;
			_bins = bins;
			_docks = docks;
		}

		#endregion

		#region Private

		private void Apply(int index, float glow)
		{
			Renderer target = _animated[index];
			if (target == null || !IsShown(target) || Mathf.Abs(_applied[index] - glow) < ChangeThreshold)
			{
				return;
			}

			_applied[index] = glow;
			_block.SetColor(EmissionColorId, Color.white * glow);
			target.SetPropertyBlock(_block);
		}

		private void ApplySteady(Renderer[] renderers, float glow)
		{
			_block.SetColor(EmissionColorId, Color.white * glow);
			foreach (Renderer target in renderers)
			{
				if (target != null)
				{
					target.SetPropertyBlock(_block);
				}
			}
		}

		private static bool IsShown(Renderer target)
		{
			return target.gameObject.activeInHierarchy;
		}

		private static Renderer[] Concat(params Renderer[][] groups)
		{
			int count = 0;
			foreach (Renderer[] group in groups)
			{
				count += group.Length;
			}

			Renderer[] all = new Renderer[count];
			int index = 0;
			foreach (Renderer[] group in groups)
			{
				group.CopyTo(all, index);
				index += group.Length;
			}

			return all;
		}

		#endregion
	}
}
