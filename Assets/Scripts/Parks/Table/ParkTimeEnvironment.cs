using System;
using UnityEngine;

namespace DroneDDW.Parks.Table
{
    [CreateAssetMenu(fileName = "Park Time Environment", menuName = "DroneDDW/Parks/Time Environment")]
    public sealed class ParkTimeEnvironment : ScriptableObject
    {
        [Serializable]
        internal struct Step
        {
            public Color FogColor;
            [Min(0f)] public float FogDensity;
            public Color SunColor;
            [Min(0f)] public float SunIntensity;
            public Color AmbientColor;
        }

        [SerializeField, Tooltip("Index 0 = 2026, 1 = 2050, 2 = 2100.")]
        private Step[] _steps =
        {
			new Step
            {
                FogColor = new Color(0.78f, 0.87f, 0.92f), FogDensity = 0.0015f,
                SunColor = new Color(1f, 0.97f, 0.9f), SunIntensity = 1f,
                AmbientColor = new Color(0.55f, 0.62f, 0.62f),
            },

			new Step
            {
                FogColor = new Color(0.80f, 0.85f, 0.88f), FogDensity = 0.002f,
                SunColor = new Color(1f, 0.96f, 0.88f), SunIntensity = 1f,
                AmbientColor = new Color(0.55f, 0.60f, 0.62f),
            },

			new Step
            {
                FogColor = new Color(0.82f, 0.84f, 0.84f), FogDensity = 0.0025f,
                SunColor = new Color(1f, 0.95f, 0.86f), SunIntensity = 0.98f,
                AmbientColor = new Color(0.55f, 0.58f, 0.60f),
            },
        };

        private void OnValidate()
        {
            if (_steps == null || _steps.Length != ParkTimes.Count)
            {
                Array.Resize(ref _steps, ParkTimes.Count);
            }
        }

        internal Step GetStep(int index)
        {
            return _steps[ParkTimes.Clamp(index)];
        }
    }
}