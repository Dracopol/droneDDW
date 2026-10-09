using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// Dusk lighting: sun, ambient gradient, fog, sky and emission strengths (colours in sRGB).
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutDusk
    {
        #region Serialized fields

        [SerializeField] private Vector3 _sunTravel = Vector3.down;
        [SerializeField] private Color _sunColor = Color.white;
        [SerializeField] private float _sunIntensity = 1f;
        [SerializeField] private Color _ambientSky = Color.gray;
        [SerializeField] private Color _ambientEquator = Color.gray;
        [SerializeField] private Color _ambientGround = Color.black;
        [SerializeField] private Color _fogColor = Color.gray;
        [SerializeField] private float _fogStart = 70f;
        [SerializeField] private float _fogEnd = 1200f;
        [SerializeField] private string _skyTexture = string.Empty;
        [SerializeField] private float _emissive = 1.6f;
        [SerializeField] private float _glassGlow = 0.45f;
        [SerializeField] private float _nightGlow = 1f;

        #endregion

        #region Properties

        public Vector3 SunTravel => _sunTravel;
        public Color SunColor => _sunColor;
        public float SunIntensity => _sunIntensity;
        public Color AmbientSky => _ambientSky;
        public Color AmbientEquator => _ambientEquator;
        public Color AmbientGround => _ambientGround;
        public Color FogColor => _fogColor;
        public float FogStart => _fogStart;
        public float FogEnd => _fogEnd;
        public string SkyTexture => _skyTexture;
        public float Emissive => _emissive;
        public float GlassGlow => _glassGlow;
        public float NightGlow => _nightGlow;

        #endregion
    }
}
