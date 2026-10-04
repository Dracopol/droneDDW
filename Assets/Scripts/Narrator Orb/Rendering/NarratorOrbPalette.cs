using System;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Rendering
{
	// The seven colours of one look, all linear: NEUTRAL in the web preview, or derived from a base colour
	// (derivePalette). Deep, mid, ice, accent and faint colour the particles; rim and inner the glow shells.
	internal readonly struct NarratorOrbPalette
	{
		#region Properties

		internal static NarratorOrbPalette Neutral => new(
			NarratorOrbColorUtil.FromHex(0x3050ff),
			NarratorOrbColorUtil.FromHex(0x5a86ff),
			NarratorOrbColorUtil.FromHex(0xb4e0ff),
			NarratorOrbColorUtil.FromHex(0xffb0ea),
			NarratorOrbColorUtil.FromHex(0x2a3cb0),
			NarratorOrbColorUtil.FromHex(0x4a6bff),
			NarratorOrbColorUtil.FromHex(0x2440d0));

		internal NarratorOrbRgb Deep { get; }
		internal NarratorOrbRgb Mid { get; }
		internal NarratorOrbRgb Ice { get; }
		internal NarratorOrbRgb Accent { get; }
		internal NarratorOrbRgb Faint { get; }
		internal NarratorOrbRgb Rim { get; }
		internal NarratorOrbRgb Inner { get; }

		#endregion

		#region Setup

		private NarratorOrbPalette(NarratorOrbRgb deep, NarratorOrbRgb mid, NarratorOrbRgb ice, NarratorOrbRgb accent,
			NarratorOrbRgb faint, NarratorOrbRgb rim, NarratorOrbRgb inner)
		{
			Deep = deep;
			Mid = mid;
			Ice = ice;
			Accent = accent;
			Faint = faint;
			Rim = rim;
			Inner = inner;
		}

		#endregion

		#region Internal

		// derivePalette: every colour of a look from its base colour (sRGB, as picked)
		internal static NarratorOrbPalette Derive(Color32 baseColor)
		{
			NarratorOrbRgb mid = NarratorOrbColorUtil.FromSrgb(baseColor.r / 255.0, baseColor.g / 255.0,
				baseColor.b / 255.0);
			NarratorOrbColorUtil.ToHsl(mid, out double hue, out double saturation, out double lightness);
			return new NarratorOrbPalette(
				Make(hue - 0.02, saturation, lightness * 0.62),
				mid,
				mid.Lerp(NarratorOrbRgb.White, 0.6),
				Make(hue - 0.12, saturation * 0.9, Math.Max(0.78, lightness)),
				Make(hue, saturation * 0.75, lightness * 0.38),
				Make(hue, saturation, lightness * 0.8),
				Make(hue, saturation, lightness * 0.45));
		}

		// How far the colours have turned to the look being approached (applyPalette): the body (shell, haze, glow) and
		// the leaders (dust and wisps), which get there first so semi steps read as two-tone. With wispsLead 1 the body
		// is 40 % there at a semi step while the leaders are fully there.
		internal static void GetWeights(float morph, float wispsLead, out float bodyWeight, out float leadWeight)
		{
			float weight = Mathf.Min(1f, Mathf.Abs(morph));
			float body = weight < 0.5f ? weight * 0.8f : 0.4f + (weight - 0.5f) * 1.2f;
			bodyWeight = weight + wispsLead * (body - weight);
			leadWeight = weight + wispsLead * (Mathf.Min(1f, weight * 2f) - weight);
		}

		#endregion

		#region Private

		// mk: wraps the hue, clamps saturation and lightness, then setHSL in sRGB
		private static NarratorOrbRgb Make(double hue, double saturation, double lightness)
		{
			return NarratorOrbColorUtil.FromHsl(NarratorOrbColorUtil.EuclideanModulo(hue, 1),
				Math.Min(1, Math.Max(0, saturation)), Math.Min(1, Math.Max(0, lightness)));
		}

		#endregion
	}
}
