using DroneDDW.NarratorOrb.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Tests
{
	// The colours against three.js r184's colour maths (NEUTRAL, derivePalette and applyPalette's weights), all linear
	public sealed class PaletteTests
	{
		#region Fields

		private const double Tolerance = 1e-6;

		#endregion

		#region Private

		private static void AssertColor(NarratorOrbRgb color, double red, double green, double blue, string name)
		{
			Assert.That(color.R, Is.EqualTo(red).Within(Tolerance), $"{name} red");
			Assert.That(color.G, Is.EqualTo(green).Within(Tolerance), $"{name} green");
			Assert.That(color.B, Is.EqualTo(blue).Within(Tolerance), $"{name} blue");
		}

		private static void AssertWeights(float morph, float wispsLead, float body, float lead)
		{
			NarratorOrbPalette.GetWeights(morph, wispsLead, out float bodyWeight, out float leadWeight);
			Assert.That(bodyWeight, Is.EqualTo(body).Within(1e-6f), $"body weight at morph {morph}");
			Assert.That(leadWeight, Is.EqualTo(lead).Within(1e-6f), $"lead weight at morph {morph}");
		}

		#endregion

		#region Tests

		[Test]
		public void NeutralMatchesTheReference()
		{
			NarratorOrbPalette neutral = NarratorOrbPalette.Neutral;
			AssertColor(neutral.Deep, 0.029556834, 0.080219820, 1.0, "deep");
			AssertColor(neutral.Mid, 0.102241733, 0.238397574, 1.0, "mid");
			AssertColor(neutral.Ice, 0.456411023, 0.745404210, 1.0, "ice");
			AssertColor(neutral.Accent, 1.0, 0.434153636, 0.822785754, "accent");
			AssertColor(neutral.Faint, 0.023153366, 0.045186204, 0.434153636, "faint");
			AssertColor(neutral.Rim, 0.068478170, 0.147027266, 1.0, "rim");
			AssertColor(neutral.Inner, 0.017641954, 0.051269458, 0.630757136, "inner");
		}

		[Test]
		public void LookAPaletteMatchesTheReference()
		{
			NarratorOrbPalette palette = NarratorOrbPalette.Derive(new Color32(0x45, 0xd4, 0x83, 0xff));
			AssertColor(palette.Deep, 0.014987382, 0.268452983, 0.055843144, "deep");
			AssertColor(palette.Mid, 0.059511238, 0.658374817, 0.226965873, "mid");
			AssertColor(palette.Ice, 0.623804495, 0.863349927, 0.690786349, "ice");
			AssertColor(palette.Accent, 0.487686385, 0.794639058, 0.388333974, "accent");
			AssertColor(palette.Faint, 0.011869497, 0.076973354, 0.031977604, "faint");
			AssertColor(palette.Rim, 0.023363665, 0.471171624, 0.135842831, "rim");
			AssertColor(palette.Inner, 0.008987139, 0.134819838, 0.042283002, "inner");
		}

		[Test]
		public void LookBPaletteMatchesTheReference()
		{
			NarratorOrbPalette palette = NarratorOrbPalette.Derive(new Color32(0xff, 0x9f, 0x43, 0xff));
			AssertColor(palette.Deep, 0.575257777, 0.067994074, 0.0, "deep");
			AssertColor(palette.Mid, 1.0, 0.346704056, 0.056128490, "mid");
			AssertColor(palette.Ice, 1.0, 0.738681623, 0.622451396, "ice");
			AssertColor(palette.Accent, 0.950681119, 0.297939946, 0.410945703, "accent");
			AssertColor(palette.Faint, 0.147218364, 0.045485970, 0.004894336, "faint");
			AssertColor(palette.Rim, 1.0, 0.209052295, 0.000789548, "rim");
			AssertColor(palette.Inner, 0.282724590, 0.062848612, 0.0, "inner");
		}

		// With wispsLead 1 the body is 40 % there at a semi step while dust and wisps are fully there
		[Test]
		public void WeightsMatchTheReference()
		{
			AssertWeights(0.5f, 1f, 0.4f, 1f);
			AssertWeights(-0.5f, 1f, 0.4f, 1f);
			AssertWeights(0.25f, 1f, 0.2f, 0.5f);
			AssertWeights(0.75f, 1f, 0.7f, 1f);
			AssertWeights(1f, 1f, 1f, 1f);
			AssertWeights(0.5f, 0f, 0.5f, 0.5f);
			AssertWeights(0f, 1f, 0f, 0f);
		}

		#endregion
	}
}
