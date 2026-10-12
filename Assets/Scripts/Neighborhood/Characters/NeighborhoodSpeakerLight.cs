using UnityEngine;

namespace DroneDDW.Neighborhood.Characters
{
    /// <summary>
    /// A soft warm pool of light on the ground under a person while they speak, so it is clear from the seat who is
    /// talking. It is a quad with the light pool shader, like the street lamps' pools (no realtime light): its colour
    /// fades in when a line starts and out after it ends, and the quad is switched off while dark.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Speaker Light")]
    public sealed class NeighborhoodSpeakerLight : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] private NeighborhoodCharacter _character;
        [SerializeField] private Renderer _pool;

        [SerializeField, Tooltip("Linear colour added to the ground at full strength (the lamp pools add about 0.22, 0.14, 0.07).")]
        private Color _color = new Color(0.7f, 0.5f, 0.24f, 1f);

        [SerializeField, Min(0.01f)] private float _fadeInSeconds = 0.3f;
        [SerializeField, Min(0.01f)] private float _fadeOutSeconds = 0.6f;

        #endregion

        #region Fields

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _block;
        private bool _isSpeaking;
        private float _level = -1f;

        #endregion

        #region Properties

        /// <summary>0 = off, 1 = full strength.</summary>
        public float Level => Mathf.Max(0f, _level);

        #endregion

        #region Setup

        private void OnEnable()
        {
            if (_character != null)
            {
                _character.OnSpeechStart += HandleSpeechStart;
                _character.OnSpeechEnd += HandleSpeechEnd;
                _isSpeaking = _character.IsSpeaking;
            }
        }

        private void OnDisable()
        {
            if (_character != null)
            {
                _character.OnSpeechStart -= HandleSpeechStart;
                _character.OnSpeechEnd -= HandleSpeechEnd;
            }

            _isSpeaking = false;
            SetLevel(0f);
        }

        #endregion

        #region Update

        private void Update()
        {
            float goal = _isSpeaking ? 1f : 0f;
            float seconds = _isSpeaking ? _fadeInSeconds : _fadeOutSeconds;
            float level = Mathf.MoveTowards(Mathf.Max(0f, _level), goal, Time.deltaTime / seconds);
            if (!Mathf.Approximately(level, _level))
            {
                SetLevel(level);
            }
        }

        #endregion

        #region Event handlers

        private void HandleSpeechStart()
        {
            _isSpeaking = true;
        }

        private void HandleSpeechEnd()
        {
            _isSpeaking = false;
        }

        #endregion

        #region Internal

        internal void Configure(NeighborhoodCharacter character, Renderer pool)
        {
            _character = character;
            _pool = pool;
        }

        #endregion

        #region Private

        private void SetLevel(float level)
        {
            _level = level;
            if (_pool == null)
            {
                return;
            }

            _pool.enabled = level > 0f;
            _block ??= new MaterialPropertyBlock();
            float strength = Mathf.SmoothStep(0f, 1f, level);

            // _Color is a Vector property in the shader, so this linear value reaches it unchanged
            _block.SetVector(ColorId, new Vector4(_color.r * strength, _color.g * strength, _color.b * strength, 1f));
            _pool.SetPropertyBlock(_block);
        }

        #endregion
    }
}
