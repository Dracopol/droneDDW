using DroneDDW.Admin.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.Admin.Tests
{
	internal sealed class IconBakerTests
	{
		#region Tests

		// The lower-left half of a 2 x 2 view box, in SVG coordinates (y down): full pixels below the diagonal, empty
		// ones above, and the texture's row 0 at the bottom
		[Test]
		public void Rasterize_CoversTheTriangle()
		{
			Vector2[] vertices = { new(0f, 0f), new(0f, 2f), new(2f, 2f) };
			int[] indices = { 0, 1, 2 };

			Color32[] pixels = AdminMenuIconBaker.Rasterize(vertices, indices, 2f, 8);

			Assert.AreEqual(255, pixels[0 * 8 + 1].a, "bottom left");
			Assert.AreEqual(0, pixels[7 * 8 + 6].a, "top right");
			Assert.That(pixels[3 * 8 + 4].a, Is.InRange(1, 254), "on the diagonal");
		}

		[Test]
		public void RasterizeSvg_DrawsEveryIcon()
		{
			string[] icons =
			{
				AdminMenuIconBaker.PlaySvg, AdminMenuIconBaker.ResetSvg, AdminMenuIconBaker.HistorySvg,
				AdminMenuIconBaker.LocationSvg, AdminMenuIconBaker.LanguageSvg, AdminMenuIconBaker.SaveSvg,
				AdminMenuIconBaker.BackSvg,
			};

			foreach (string icon in icons)
			{
				Color32[] pixels = AdminMenuIconBaker.RasterizeSvg(icon, 48);
				int covered = 0;
				foreach (Color32 pixel in pixels)
				{
					covered += pixel.a > 127 ? 1 : 0;
				}

				// Thin strokes: some of the square, nowhere near all of it
				Assert.That(covered, Is.InRange(48 * 48 / 40, 48 * 48 * 3 / 5), icon);
			}
		}

		#endregion
	}
}
