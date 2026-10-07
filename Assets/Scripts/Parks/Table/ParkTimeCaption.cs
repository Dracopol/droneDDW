using TMPro;
using UnityEngine;

namespace DroneDDW.Parks.Table
{
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Parks/Park Time Caption")]
    public sealed class ParkTimeCaption : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField, Tooltip("Found automatically when empty.")]
        private ParkTimeWorld _world;

        [SerializeField, Tooltip("The board the text hangs on. Found automatically when empty.")]
        private Transform _board;

        [SerializeField, Tooltip("Where the text sits, in the board's space (its origin is the middle of the board's top).")]
        private Vector3 _localPosition = new Vector3(0f, 0.36f, 0f);

        [SerializeField] private Vector2 _size = new Vector2(0.8f, 0.3f);

        [SerializeField, Min(0.01f), Tooltip("World-space text size. Raise or lower until it reads well.")]
        private float _fontSize = 0.3f;

        #endregion

        #region Fields

        private static readonly int[] Years = { 2026, 2050, 2100 };

        private static readonly string[,] Stories =
        {
            {
                "Close to the original ecosystem. The drones keep their distance.",
                "Decades of light monitoring: more species, more colour, wilder growth.",
                "Dense, layered and unpredictable. Little is measured, and much is unknown.",
            },
            {
                "Early days: watching without taking over.",
                "Some areas are watched closely, others are left to grow.",
                "A patchwork of managed ground and wild ground.",
            },
            {
                "More drones and scanning, yet it still looks healthy.",
                "More knowledge and control. Plants are trimmed and animals change where they move.",
                "Extremely well monitored and managed. Is this still the ecosystem we set out to protect?",
            },
        };

        private static readonly string[] Disturbances =
        {
            "Visible disturbance: animals flee drones",
            "Visible disturbance: animals watch and adapt",
            "Visible disturbance: animals barely react",
        };

        private TextMeshPro _text;
        private int _shownKey = -1;

        #endregion

        #region Setup

        private void Start()
        {
            if (_world == null)
            {
                _world = FindObjectOfType<ParkTimeWorld>();
            }

            if (_board == null)
            {
                ParkBoardFollow board = FindObjectOfType<ParkBoardFollow>();
                _board = board != null ? board.transform : null;
            }

            if (_world == null || _board == null)
            {
                Debug.LogWarning("[Parks] Park Time Caption needs a Park Time World and a Park Board Follow.", this);
                enabled = false;
                return;
            }

            var go = new GameObject("Time Caption");
            go.transform.SetParent(_board, false);
            go.transform.localPosition = _localPosition;
            go.transform.localRotation = Quaternion.identity;
            _text = go.AddComponent<TextMeshPro>();
            _text.rectTransform.sizeDelta = _size;
            _text.fontSize = _fontSize;
            _text.alignment = TextAlignmentOptions.Bottom;
            _text.color = Color.white;
        }

        #endregion

        #region Update

        private void LateUpdate()
        {
            float time = _world.TimePosition;
            int step = ParkTimes.Clamp(Mathf.RoundToInt(time));
            float efficiency = _world.Efficiency;
            int side = efficiency < 0.4f ? 0 : (efficiency > 0.6f ? 2 : 1);
            int alpha = Mathf.RoundToInt(Mathf.Clamp01(time - 0.5f) * 15f) * 17;
            int key = step * 10000 + side * 1000 + alpha;
            if (key == _shownKey)
            {
                return;
            }

            _shownKey = key;
            _text.text = $"<b>{Years[step]}</b>\n{Stories[side, step]}\n<size=75%>{Disturbances[step]}\n"
                + $"<alpha=#{alpha:X2}>Long-term ecological impact: uncertain";
        }

        #endregion
    }
}