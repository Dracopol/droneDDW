using TMPro;
using UnityEngine;

namespace DroneDDW.Parks.Table
{
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Parks/Park Time Label")]
    public sealed class ParkTimeLabel : MonoBehaviour
    {
        [SerializeField] private ParkLevelSlider _slider;
        [SerializeField] private TMP_Text _text;

        private int _shown = -1;

        private void LateUpdate()
        {
            int detent = ParkTimes.Clamp(Mathf.Max(0, _slider.Detent));
            if (detent == _shown)
            {
                return;
            }

            _shown = detent;
            _text.text = ParkTimes.GetYear(detent).ToString();
        }
    }
}