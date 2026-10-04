using DroneDDW.Characters.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.Characters.Tests
{
	// The portrait measurements against the files. Grandma's boxes were measured separately from her PNGs (rows from
	// the bottom, as Unity counts them): her visible area spans x 438 to 1435 and rises 784 rows from the bottom edge,
	// and her natural open and closed mouths differ in x 890 to 980, y 362 to 402.
	public sealed class CharacterPortraitAnalysisTests
	{
		#region Fields

		private const string Grandma = "grandma";
		private const int PixelTolerance = 3;
		private const float AspectTolerance = 0.01f;

		#endregion

		#region Tests

		[Test]
		public void GrandmaMouthIsWhereTheOpenAndClosedPortraitsDiffer()
		{
			Color32[] closed = CharacterPortraitAnalysis.ReadPixels(
				CharacterPortraitBuilder.GetPortraitPath(Grandma, (int)CharacterEmotion.Natural, false), out int width,
				out int height);
			Color32[] open = CharacterPortraitAnalysis.ReadPixels(
				CharacterPortraitBuilder.GetPortraitPath(Grandma, (int)CharacterEmotion.Natural, true), out _, out _);
			RectInt mouth = CharacterPortraitAnalysis.FindDifferenceBox(closed, open, width, height);

			Assert.That(width, Is.EqualTo(1920));
			Assert.That(height, Is.EqualTo(1080));
			Assert.That(mouth.xMin, Is.EqualTo(890).Within(PixelTolerance));
			Assert.That(mouth.xMax - 1, Is.EqualTo(980).Within(PixelTolerance));
			Assert.That(mouth.yMin, Is.EqualTo(362).Within(PixelTolerance));
			Assert.That(mouth.yMax - 1, Is.EqualTo(402).Within(PixelTolerance));
		}

		[Test]
		public void GrandmaVisibleAreaIsHerBust()
		{
			Color32[] closed = CharacterPortraitAnalysis.ReadPixels(
				CharacterPortraitBuilder.GetPortraitPath(Grandma, (int)CharacterEmotion.Natural, false), out int width,
				out int height);
			RectInt visible = CharacterPortraitAnalysis.FindVisibleBox(closed, width, height);

			Assert.That(visible.xMin, Is.EqualTo(438).Within(PixelTolerance));
			Assert.That(visible.xMax - 1, Is.EqualTo(1435).Within(PixelTolerance));
			Assert.That(visible.yMin, Is.EqualTo(0));
			Assert.That(visible.yMax, Is.EqualTo(784).Within(PixelTolerance));
		}

		// The crop has the card's shape, holds the whole bust and the mouth, and keeps to the image's bottom edge
		[Test]
		public void EveryCropHoldsTheCharacterAndTheMouth()
		{
			foreach ((string file, _, _) in CharacterPortraitBuilder.Characters)
			{
				CharacterPortraitBuilder.MeasureCharacter(file, out Rect crop, out Rect mouth);
				Color32[] closed = CharacterPortraitAnalysis.ReadPixels(
					CharacterPortraitBuilder.GetPortraitPath(file, (int)CharacterEmotion.Natural, false),
					out int width, out int height);
				Rect visible = CharacterPortraitAnalysis.ToNormalized(
					CharacterPortraitAnalysis.FindVisibleBox(closed, width, height), width, height);

				float aspect = crop.width * width / (crop.height * height);
				Assert.That(aspect, Is.EqualTo(CharacterPortraitBuilder.CropAspect).Within(AspectTolerance), file);
				Assert.That(crop.yMin, Is.EqualTo(0f), file);
				Assert.That(crop.xMin, Is.LessThanOrEqualTo(visible.xMin), file);
				Assert.That(crop.xMax, Is.GreaterThanOrEqualTo(visible.xMax), file);
				Assert.That(crop.yMax, Is.GreaterThanOrEqualTo(visible.yMax), file);
				Assert.That(mouth.width, Is.GreaterThan(0f), file);
				Assert.That(crop.Contains(mouth.min) && crop.Contains(mouth.max), Is.True, file);
			}
		}

		#endregion
	}
}
