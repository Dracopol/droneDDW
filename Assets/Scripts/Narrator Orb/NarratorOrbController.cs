using System;
using DroneDDW.NarratorOrb.Rendering;
using DroneDDW.NarratorOrb.Settings;
using DroneDDW.Voice;
using UnityEngine;

namespace DroneDDW.NarratorOrb
{
	// The narrator orb: a particle sphere that breathes, ripples and sparkles with the narrator's voice and morphs
	// between look A, neutral and look B, turned to face the player's head (the web preview's frame()). A type called
	// NarratorOrb would clash with its namespace in code elsewhere in DroneDDW, hence Controller.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Narrator Orb/Narrator Orb Controller")]
	public sealed class NarratorOrbController : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Every value of the web preview's settings. The orb reads it each frame, so edits apply live.")]
		[SerializeField] private NarratorOrbSettings _settings;

		[Tooltip("The narrator's voice, 3D at the orb. Clips need Load Type Decompress On Load to be read ahead.")]
		[SerializeField] private AudioSource _voice;

		[Tooltip("On the voice's object: follows the live output when a clip can't be read ahead")]
		[SerializeField] private VoiceLiveTap _liveTap;

		[Tooltip("The player's head: the orb turns to face it and starts its ripples on that side")]
		[SerializeField] private Transform _head;

		[Header("Parts")]
		[SerializeField] private MeshRenderer _particles;
		[SerializeField] private MeshRenderer _halos;
		[SerializeField] private MeshRenderer _innerGlow;
		[SerializeField] private MeshRenderer _rippleRings;

		#endregion

		#region Fields

		private const int WispCount = NarratorOrbTimeConstants.WispCount;
		private const float MinDeltaSeconds = 0.0001f;
		private const float MaxDeltaSeconds = 0.05f;
		private const float FacingRatePerSecond = 3f;
		private const float PokeEaseRatePerSecond = 6f;
		private const float PokeFollowRatePerSecond = 14f;
		private const float PokeSpeedPerMeter = 25f;
		private const float MaxPokeSpeed = 1.5f;

		private static readonly string[] StepNames = { "A", "semi A", "neutral", "semi B", "B" };

		private readonly NarratorOrbRipples _ripples = new();
		private readonly float[] _wispEnvelopes = new float[WispCount];
		private readonly Vector4[] _shellRows = new Vector4[3];
		private readonly Vector4[] _dustRows = new Vector4[3];
		private VoiceReader _voiceReader;
		private NarratorOrbMaterials _materials;
		private Transform _transform;
		private Transform _headOverride;
		private NarratorOrbPalette _neutral;
		private NarratorOrbPalette _paletteA;
		private NarratorOrbPalette _paletteB;
		private Color32 _paletteAColor;
		private Color32 _paletteBColor;
		private bool _hasPalettes;
		private double _elapsedSeconds;
		private float _loudness;
		private float _hiss;
		private float _pulse;
		private Vector4 _wispLevels = new(0.1f, 0.1f, 0.1f, 0.1f);
		private Vector4 _wispPhases;
		private float _morph;
		private float _morphVelocity;
		private int _step;
		private int _settingsStep;
		private int _syllableCount;
		private Vector3 _pokeTarget;
		private Vector3 _pokePosition;
		private Vector3 _lastPokePoint;
		private float _pokeAmountTarget;
		private float _pokeAmount;
		private float _pokeSpeed;

		#endregion

		#region Properties

		internal NarratorOrbSettings Settings => _settings;
		internal AudioSource Voice => _voice;
		internal Transform Head => _head;
		internal float Loudness => _loudness;

		// The four wisps' vowel envelopes on average, like the preview's vowels meter
		internal float Vowels => (_wispEnvelopes[0] + _wispEnvelopes[1] + _wispEnvelopes[2] + _wispEnvelopes[3]) / 4f;

		internal float Hiss => _hiss;
		internal int SyllableCount => _syllableCount;
		internal float Morph => _morph;
		internal int Step => _step;
		internal VoiceStatus VoiceStatus => _voiceReader?.Status ?? VoiceStatus.Idle;

		// The analysis of the clip being followed, or null
		internal VoiceAnalysis Analysis => _voiceReader?.Analysis;

		private Transform CurrentHead => _headOverride != null ? _headOverride : _head;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
			if (!HasParts())
			{
				enabled = false;
				return;
			}

			_settings.Validate();
			_materials = new NarratorOrbMaterials(_particles, _halos, _innerGlow, _rippleRings);
			_voiceReader = new VoiceReader(_voice, _liveTap);
			_voiceReader.OnSyllable += HandleSyllable;
			_neutral = NarratorOrbPalette.Neutral;
			_step = _settings.Morph.Step;
			_settingsStep = _step;
			_lastPokePoint = _transform.position;
		}

		private void OnDestroy()
		{
			if (_voiceReader != null)
			{
				_voiceReader.OnSyllable -= HandleSyllable;
				_voiceReader.Dispose();
			}

			_materials?.Dispose();
		}

		#endregion

		#region Update

		private void Update()
		{
			float deltaSeconds = Mathf.Clamp(Time.deltaTime, MinDeltaSeconds, MaxDeltaSeconds);
			_elapsedSeconds += deltaSeconds;
			double time = _elapsedSeconds;
			NarratorOrbReactionSettings reaction = _settings.Reaction;
			NarratorOrbTimeConstants timeConstants = _settings.TimeConstants;
			float smoothing = reaction.Smoothing;
			FollowSettingsStep();

			// The voice at the moment being heard; the syllables it passed fire ripples through HandleSyllable
			NarratorOrbVoiceSettings voice = _settings.Voice;
			VoiceFeatures features = _voiceReader.Read(voice.ToAnalysisSettings(), voice.LeadSeconds,
				Time.unscaledDeltaTime);
			_loudness = Follow(_loudness, (float)features.Level, timeConstants.LevelAttackSeconds,
				timeConstants.LevelReleaseSeconds * smoothing, deltaSeconds);
			_hiss = Follow(_hiss, (float)features.Hiss, timeConstants.HissAttackSeconds,
				timeConstants.HissReleaseSeconds * smoothing, deltaSeconds);
			_pulse *= Mathf.Exp(-timeConstants.PulseDecayPerSecond * deltaSeconds);
			for (int wisp = 0; wisp < WispCount; wisp++)
			{
				_wispEnvelopes[wisp] = Follow(_wispEnvelopes[wisp], (float)features.Vowel,
					timeConstants.WispAttackSeconds[wisp], timeConstants.WispReleaseSeconds[wisp] * smoothing,
					deltaSeconds);
				float level = 0.1f + 0.9f * Mathf.Min(1f, _wispEnvelopes[wisp] * reaction.Wisps);
				_wispLevels[wisp] = level;
				_wispPhases[wisp] = (_wispPhases[wisp] + deltaSeconds * (0.05f + 0.13f * level)) % 1f;
			}

			// Idle breathing plus the voice swell, and the glow shells' opacity
			float breathe = 1f + 0.02f * (float)Math.Sin(time * 1.3) + 0.006f * (float)Math.Sin(time * 3.7)
				+ reaction.Swell * _loudness + 0.015f * _pulse;
			float haloOpacityScale = 1f + 0.15f * (float)Math.Sin(time * 1.1) + reaction.Glow * _loudness
				+ 0.8f * _pulse;
			float innerOpacityScale = 1f + 0.5f * _loudness;
			SetEulerRows(Math.Sin(time * 0.3) * 0.12, time * 0.09, _shellRows);
			SetEulerRows(Math.Sin(time * 0.25) * 0.2, -time * 0.07, _dustRows);

			// The morph between A, neutral and B
			NarratorOrbMorphSettings morph = _settings.Morph;
			_morph = Mathf.SmoothDamp(_morph, morph.Steps[_step], ref _morphVelocity, morph.SmoothTimeSeconds,
				Mathf.Infinity, deltaSeconds);

			// The poke eases in and out and follows its point
			_pokeAmount += (_pokeAmountTarget - _pokeAmount) * Mathf.Min(1f, deltaSeconds * PokeEaseRatePerSecond);
			_pokeSpeed *= Mathf.Pow(0.94f, deltaSeconds * 60f);
			_pokePosition += (_pokeTarget - _pokePosition) * Mathf.Min(1f, deltaSeconds * PokeFollowRatePerSecond);

			Upload((float)time, breathe, haloOpacityScale, innerOpacityScale);
			FaceHead(deltaSeconds);
		}

		#endregion

		#region Event handlers

		private void HandleSyllable(float strength)
		{
			Transform head = CurrentHead;
			Vector3 listener = head != null ? _transform.InverseTransformPoint(head.position) : Vector3.forward;
			_ripples.Spawn(listener, strength, _settings.Ripple.SpreadFromFront, (float)_elapsedSeconds);
			_pulse = Mathf.Max(_pulse, strength);
			_syllableCount++;
		}

		#endregion

		#region Public

		// Morphs to a step: 0 A, 1 semi A, 2 neutral, 3 semi B, 4 B, at the values in the settings' steps
		public void SetStep(int step)
		{
			int lastStep = NarratorOrbMorphSettings.StepCount - 1;
			if (step < 0 || step > lastStep)
			{
				Debug.LogWarning($"[Narrator Orb] SetStep({step}) is out of range: the steps go from 0 (A) to "
					+ $"{lastStep} (B).", this);
				step = Mathf.Clamp(step, 0, lastStep);
			}

			_step = step;
		}

		// Pushes the particles near a point, for a fingertip later. The amount (0 to 1) eases in; call this every frame
		// while poking, and with 0 to let go.
		public void SetPoke(Vector3 worldPoint, float amount)
		{
			_pokeAmountTarget = Mathf.Clamp01(amount);
			if (amount <= 0f)
			{
				return;
			}

			float movedMeters = Vector3.Distance(worldPoint, _lastPokePoint);
			_pokeSpeed = Mathf.Min(MaxPokeSpeed, _pokeSpeed + movedMeters * PokeSpeedPerMeter);
			_lastPokePoint = worldPoint;
			_pokeTarget = _transform.InverseTransformPoint(worldPoint);
		}

		#endregion

		#region Internal

		internal static string GetStepName(int step)
		{
			return StepNames[step];
		}

		// Faces this instead of the head until called with null (the test scene's compare camera)
		internal void OverrideHead(Transform head)
		{
			_headOverride = head;
		}

		// Moves the orb and turns it straight to whoever it faces, without the usual eased turn
		internal void MoveTo(Vector3 position)
		{
			_transform.position = position;
			Transform head = CurrentHead;
			if (head != null && (head.position - position).sqrMagnitude > 1e-8f)
			{
				_transform.rotation = Quaternion.LookRotation(head.position - position, Vector3.up);
			}
		}

		internal void Configure(NarratorOrbSettings settings, AudioSource voice, VoiceLiveTap liveTap,
			MeshRenderer particles, MeshRenderer halos, MeshRenderer innerGlow, MeshRenderer rippleRings)
		{
			_settings = settings;
			_voice = voice;
			_liveTap = liveTap;
			_particles = particles;
			_halos = halos;
			_innerGlow = innerGlow;
			_rippleRings = rippleRings;
		}

		internal void ConfigureHead(Transform head)
		{
			_head = head;
		}

		// Whether every animated value is a number, for the Play mode check
		internal bool AreValuesFinite()
		{
			return float.IsFinite(_loudness) && float.IsFinite(_hiss) && float.IsFinite(_pulse)
				&& float.IsFinite(_morph) && float.IsFinite(_pokeAmount) && float.IsFinite(_pokeSpeed)
				&& float.IsFinite(Vowels) && float.IsFinite(_wispPhases.x + _wispPhases.y + _wispPhases.z
					+ _wispPhases.w) && float.IsFinite(_transform.rotation.w);
		}

		#endregion

		#region Private

		private bool HasParts()
		{
			if (_settings != null && _particles != null && _halos != null && _innerGlow != null && _rippleRings != null)
			{
				return true;
			}

			Debug.LogError("[Narrator Orb] Assign the settings asset and the four part renderers (the Narrator Orb "
				+ "prefab has them all), so the orb can run.", this);
			return false;
		}

		// Editing the settings' step in Play mode moves the orb there; SetStep leaves the asset alone
		private void FollowSettingsStep()
		{
			int settingsStep = _settings.Morph.Step;
			if (settingsStep == _settingsStep)
			{
				return;
			}

			_settingsStep = settingsStep;
			SetStep(settingsStep);
		}

		private void Upload(float time, float breathe, float haloOpacityScale, float innerOpacityScale)
		{
			NarratorOrbLookSettings look = _settings.Look;
			NarratorOrbReactionSettings reaction = _settings.Reaction;
			NarratorOrbRippleSettings ripple = _settings.Ripple;
			Vector4 rippleShape = new(ripple.SpeedRadiansPerSecond, ripple.WidthRadians, ripple.LifeSeconds, 0f);
			Vector4 poke = new(_pokePosition.x, _pokePosition.y, _pokePosition.z, _pokeAmount);
			_materials.SetShape(_morph, look.ShapeA, look.ShapeB, breathe);
			_materials.SetTime(time);
			_materials.SetRipples(_ripples.Origins, _ripples.Strengths, rippleShape, reaction.Ripples);
			_materials.SetVoice(_loudness, _hiss, reaction.Sparkle);
			_materials.SetRotations(_shellRows, _dustRows);
			_materials.SetWisps(_wispPhases, _wispLevels);
			_materials.SetPoke(poke, _pokeSpeed);
			_materials.SetGlow(haloOpacityScale, innerOpacityScale);
			_materials.SetRendering(_settings.Rendering);
			UploadPalette(look);
		}

		// applyPalette: the look being approached, how far the body and the leaders have turned to it, and the glow
		// shells' blended colours
		private void UploadPalette(NarratorOrbLookSettings look)
		{
			if (!_hasPalettes || !IsSameColor(look.ColorA, _paletteAColor))
			{
				_paletteA = NarratorOrbPalette.Derive(look.ColorA);
				_paletteAColor = look.ColorA;
			}

			if (!_hasPalettes || !IsSameColor(look.ColorB, _paletteBColor))
			{
				_paletteB = NarratorOrbPalette.Derive(look.ColorB);
				_paletteBColor = look.ColorB;
			}

			_hasPalettes = true;
			NarratorOrbPalette pole = _morph < 0f ? _paletteA : _paletteB;
			NarratorOrbPalette.GetWeights(_morph, _settings.Morph.WispsLead, out float bodyWeight,
				out float leadWeight);
			_materials.SetPalette(pole, bodyWeight, leadWeight, _neutral.Rim.Lerp(pole.Rim, bodyWeight),
				_neutral.Inner.Lerp(pole.Inner, bodyWeight), _neutral.Ice.Lerp(pole.Ice, bodyWeight));
		}

		private void FaceHead(float deltaSeconds)
		{
			Transform head = CurrentHead;
			if (head == null)
			{
				return;
			}

			Vector3 toHead = head.position - _transform.position;
			if (toHead.sqrMagnitude < 1e-8f)
			{
				return;
			}

			Quaternion facing = Quaternion.LookRotation(toHead, Vector3.up);
			float blend = 1f - Mathf.Exp(-deltaSeconds * FacingRatePerSecond);
			_transform.rotation = Quaternion.Slerp(_transform.rotation, facing, blend);
		}

		// follow(): eases towards the target, over the attack time when rising and the release time when falling
		private static float Follow(float current, float target, float attackSeconds, float releaseSeconds,
			float deltaSeconds)
		{
			float timeConstant = Mathf.Max(1e-4f, target > current ? attackSeconds : releaseSeconds);
			return current + (target - current) * (1f - Mathf.Exp(-deltaSeconds / timeConstant));
		}

		// three.js Matrix4.makeRotationFromEuler for order XYZ with no z turn, as the matrix's three rows
		private static void SetEulerRows(double x, double y, Vector4[] rows)
		{
			double a = Math.Cos(x);
			double b = Math.Sin(x);
			double c = Math.Cos(y);
			double d = Math.Sin(y);
			rows[0] = new Vector4((float)c, 0f, (float)d, 0f);
			rows[1] = new Vector4((float)(b * d), (float)a, (float)(-b * c), 0f);
			rows[2] = new Vector4((float)(-a * d), (float)b, (float)(a * c), 0f);
		}

		private static bool IsSameColor(Color32 first, Color32 second)
		{
			return first.r == second.r && first.g == second.g && first.b == second.b;
		}

		#endregion
	}
}
