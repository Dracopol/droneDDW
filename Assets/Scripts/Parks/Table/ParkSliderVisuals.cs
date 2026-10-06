using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Table
{
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Parks/Park Slider Visuals")]
    public sealed class ParkSliderVisuals : MonoBehaviour
    {
        #region Entities

        private sealed class GlowPart
        {
            public GlowPart(Renderer renderer)
            {
                Renderer = renderer;
            }

            public Renderer Renderer { get; }
            public float Glow { get; set; }
            public float AppliedGlow { get; set; } = -1f;
        }

        #endregion

        #region Serialized fields

        [SerializeField] private ParkLevelSlider _slider;

        [SerializeField, Tooltip("Level slider: the notch pulses while it changes level, and the pop plays when its "
            + "level changes.")]
        private ParkLevelController _controller;

        [SerializeField, Tooltip("Time slider: the notch pulses while it changes time step, and the pop plays when "
            + "its step changes.")]
        private ParkTimeController _timeController;

        [Header("Parts")]
        [SerializeField] private Renderer _knob;

        [SerializeField, Tooltip("One per detent, detent 0 first.")]
        private Renderer[] _notches = Array.Empty<Renderer>();

        [SerializeField] private Renderer _natureGlyph;
        [SerializeField] private Renderer _efficiencyGlyph;

        [SerializeField, Tooltip("The table model's root (Rig_ModelRoot). It pops with the knob. Leave empty on the "
            + "time slider.")]
        private Transform _model;

        [Header("Glow")]
        [SerializeField, ColorUsage(false, true)] private Color _glowColor = Color.white;

        [SerializeField, Min(0f), Tooltip("How quickly every glow follows its target. 0 = no smoothing.")]
        private float _glowSmoothing = 12f;

        [SerializeField, Min(0f)] private float _knobInReachGlow = 0.12f;
        [SerializeField, Min(0f)] private float _knobPushedGlow = 0.3f;
        [SerializeField, Min(0f)] private float _knobGrabbedGlow = 0.5f;

        [SerializeField, Min(0f), Tooltip("The knob's notch while a hand moves the knob.")]
        private float _notchActiveGlow = 0.45f;

        [SerializeField, Min(0f), Tooltip("The knob's notch at rest.")]
        private float _notchRestGlow = 0.12f;

        [SerializeField, Tooltip("The knob's notch pulses between these while the park changes.")]
        private Vector2 _notchPulseGlow = new Vector2(0.15f, 0.45f);

        [SerializeField, Min(0f)] private float _notchPulseHertz = 1.5f;

        [SerializeField, Min(0f), Tooltip("A glyph's glow with the knob at its end of the track.")]
        private float _glyphGlow = 0.4f;

        [SerializeField, Range(0f, 1f), Tooltip("Share of the glyph glow kept while no hand moves the knob.")]
        private float _glyphRestShare = 0.35f;

        [Header("Pop")]
        [SerializeField, Min(0f), Tooltip("How much bigger the knob gets at the peak of the pop. 0 = no pop.")]
        private float _knobPopScale = 0.15f;

        [SerializeField, Min(0f), Tooltip("How much bigger the table model gets at the peak of the pop. 0 = no pop.")]
        private float _modelPopScale = 0.05f;

        [SerializeField, Min(0.01f)] private float _popDurationSeconds = 0.3f;

        #endregion

        #region Fields

        private const string EmissionKeyword = "_EMISSION";
        private const float GlowEpsilon = 0.002f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private CancellationTokenSource _lifetime;
        private MaterialPropertyBlock _block;
        private GlowPart _knobPart;
        private GlowPart[] _notchParts = Array.Empty<GlowPart>();
        private GlowPart _natureGlyphPart;
        private GlowPart _efficiencyGlyphPart;
        private Transform _knobTransform;
        private Vector3 _knobBaseScale;
        private Vector3 _modelBaseScale;
        private int _popId;

        #endregion

        #region Properties

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

        private bool IsTransitioning =>
            (_controller != null && _controller.IsTransitioning)
            || (_timeController != null && _timeController.IsTransitioning);

        #endregion

        #region Setup

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _knobPart = CreatePart(_knob);
            _notchParts = new GlowPart[_notches.Length];
            for (int i = 0; i < _notches.Length; i++)
            {
                _notchParts[i] = CreatePart(_notches[i]);
            }

            _natureGlyphPart = CreatePart(_natureGlyph);
            _efficiencyGlyphPart = CreatePart(_efficiencyGlyph);
            if (_knob != null)
            {
                _knobTransform = _knob.transform;
                _knobBaseScale = _knobTransform.localScale;
            }

            if (_model != null)
            {
                _modelBaseScale = _model.localScale;
            }
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnLevelChange += HandleLevelChange;
            }

            if (_timeController != null)
            {
                _timeController.OnTimeChange += HandleTimeChange;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnLevelChange -= HandleLevelChange;
            }

            if (_timeController != null)
            {
                _timeController.OnTimeChange -= HandleTimeChange;
            }

            _popId++;
            SetPop(0f);
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

        #region Update

        private void LateUpdate()
        {
            float follow = _glowSmoothing > 0f ? 1f - Mathf.Exp(-_glowSmoothing * Time.deltaTime) : 1f;
            bool isMoving = _slider.IsDragging;
            UpdatePart(_knobPart, GetKnobGlow(_slider.Contact), follow);

            int detent = _slider.Detent;
            float notchGlow = GetNotchGlow(isMoving);
            for (int i = 0; i < _notchParts.Length; i++)
            {
                UpdatePart(_notchParts[i], i == detent ? notchGlow : 0f, follow);
            }

            float blend = _slider.HandleBlend;
            float glyphGlow = _glyphGlow * (isMoving ? 1f : _glyphRestShare);
            UpdatePart(_natureGlyphPart, (1f - blend) * glyphGlow, follow);
            UpdatePart(_efficiencyGlyphPart, blend * glyphGlow, follow);
        }

        #endregion

        #region Event handlers

        private void HandleLevelChange(ParkLevelChange change)
        {
            if (!change.IsImmediate && change.Previous != change.Next)
            {
                PopAsync(++_popId, LifetimeToken).Forget();
            }
        }

        private void HandleTimeChange(ParkTimeChange change)
        {
            if (!change.IsImmediate && change.Previous != change.Next)
            {
                PopAsync(++_popId, LifetimeToken).Forget();
            }
        }

        #endregion

        #region Internal

        internal void Configure(ParkLevelSlider slider, ParkLevelController controller, Renderer knob,
            Renderer[] notches, Renderer natureGlyph, Renderer efficiencyGlyph, Transform model)
        {
            _slider = slider;
            _controller = controller;
            _knob = knob;
            _notches = notches ?? Array.Empty<Renderer>();
            _natureGlyph = natureGlyph;
            _efficiencyGlyph = efficiencyGlyph;
            _model = model;
        }

        internal void ConfigureTime(ParkTimeController timeController)
        {
            _timeController = timeController;
        }

        #endregion

        #region Private

        private GlowPart CreatePart(Renderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            Material material = renderer.sharedMaterial;
            if (material != null && !material.IsKeywordEnabled(EmissionKeyword))
            {
                Debug.LogWarning($"[Parks] {renderer.name} can't glow: its material has no emission "
                    + "(the park builder gives it Park_Mat_Props_Glow).", this);
            }

            return new GlowPart(renderer);
        }

        private float GetKnobGlow(ParkSliderContact contact)
        {
            switch (contact)
            {
                case ParkSliderContact.InReach:
                    return _knobInReachGlow;
                case ParkSliderContact.Pushed:
                    return _knobPushedGlow;
                case ParkSliderContact.Grabbed:
                    return _knobGrabbedGlow;
                default:
                    return 0f;
            }
        }

        private float GetNotchGlow(bool isMoving)
        {
            if (isMoving)
            {
                return _notchActiveGlow;
            }

            if (IsTransitioning)
            {
                float wave = 0.5f - 0.5f * Mathf.Cos(Time.time * _notchPulseHertz * 2f * Mathf.PI);
                return Mathf.Lerp(_notchPulseGlow.x, _notchPulseGlow.y, wave);
            }

            return _notchRestGlow;
        }
        private void UpdatePart(GlowPart part, float target, float follow)
        {
            if (part == null)
            {
                return;
            }

            float glow = Mathf.Abs(part.Glow - target) < GlowEpsilon ? target : Mathf.Lerp(part.Glow, target, follow);
            part.Glow = glow;
            bool isSettled = glow == target;
            if (glow == part.AppliedGlow || (!isSettled && Mathf.Abs(glow - part.AppliedGlow) < GlowEpsilon))
            {
                return;
            }

            part.AppliedGlow = glow;
            _block.SetColor(EmissionColorId, _glowColor * glow);
            part.Renderer.SetPropertyBlock(_block);
        }

        private async UniTask PopAsync(int popId, CancellationToken cancellationToken)
        {
            try
            {
                float elapsed = 0f;
                while (elapsed < _popDurationSeconds)
                {
                    await UniTask.NextFrame(cancellationToken);
                    if (popId != _popId)
                    {
                        return;
                    }

                    elapsed += Time.deltaTime;
                    SetPop(Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / _popDurationSeconds)));
                }

                SetPop(0f);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void SetPop(float amount)
        {
            if (_knobTransform != null)
            {
                _knobTransform.localScale = _knobBaseScale * (1f + _knobPopScale * amount);
            }

            if (_model != null)
            {
                _model.localScale = _modelBaseScale * (1f + _modelPopScale * amount);
            }
        }

        #endregion
    }
}