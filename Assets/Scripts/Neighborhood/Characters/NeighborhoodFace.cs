using UnityEngine;

namespace DroneDDW.Neighborhood.Characters
{
    /// <summary>
    /// Swaps the eye and mouth meshes of a character: Natural (usual eyes, smile), Happy (happy eyes, big
    /// smile), Angry (angry eyes, frown), and the open mouth while a line is spoken.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Face")]
    public sealed class NeighborhoodFace : MonoBehaviour
    {
        #region Serialized fields

        [Header("Eyes")]
        [SerializeField] private Renderer _eyesUsual;
        [SerializeField] private Renderer _eyesHappy;
        [SerializeField] private Renderer _eyesAngry;

        [Header("Mouths")]
        [SerializeField] private Renderer _mouthSmile;
        [SerializeField] private Renderer _mouthBigSmile;
        [SerializeField] private Renderer _mouthFrown;
        [SerializeField] private Renderer _mouthOpen;

        #endregion

        #region Fields

        private NeighborhoodEmotion _emotion;
        private bool _isMouthOpen;

        #endregion

        #region Properties

        public NeighborhoodEmotion Emotion => _emotion;
        public bool IsMouthOpen => _isMouthOpen;

        #endregion

        #region Public

        public void SetEmotion(NeighborhoodEmotion emotion)
        {
            _emotion = emotion;
            Apply();
        }

        public void SetMouthOpen(bool isOpen)
        {
            if (isOpen == _isMouthOpen)
            {
                return;
            }

            _isMouthOpen = isOpen;
            Apply();
        }

        #endregion

        #region Internal

        internal void Configure(Renderer eyesUsual, Renderer eyesHappy, Renderer eyesAngry, Renderer mouthSmile,
            Renderer mouthBigSmile, Renderer mouthFrown, Renderer mouthOpen)
        {
            _eyesUsual = eyesUsual;
            _eyesHappy = eyesHappy;
            _eyesAngry = eyesAngry;
            _mouthSmile = mouthSmile;
            _mouthBigSmile = mouthBigSmile;
            _mouthFrown = mouthFrown;
            _mouthOpen = mouthOpen;
        }

        #endregion

        #region Private

        private void Apply()
        {
            SetShown(_eyesUsual, _emotion == NeighborhoodEmotion.Natural);
            SetShown(_eyesHappy, _emotion == NeighborhoodEmotion.Happy);
            SetShown(_eyesAngry, _emotion == NeighborhoodEmotion.Angry);
            SetShown(_mouthOpen, _isMouthOpen);
            SetShown(_mouthSmile, !_isMouthOpen && _emotion == NeighborhoodEmotion.Natural);
            SetShown(_mouthBigSmile, !_isMouthOpen && _emotion == NeighborhoodEmotion.Happy);
            SetShown(_mouthFrown, !_isMouthOpen && _emotion == NeighborhoodEmotion.Angry);
        }

        private static void SetShown(Renderer part, bool isShown)
        {
            if (part != null && part.enabled != isShown)
            {
                part.enabled = isShown;
            }
        }

        #endregion
    }
}
