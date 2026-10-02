using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Life
{
	/// <summary>
	/// The park's colour palette (Park_Palette.png): a 16 by 16 grid of flat colour swatches that every park model
	/// samples. Gives the texture coordinates of a swatch by its colour, so new models colour their faces the way the
	/// kit's models do, with no texture of their own. A colour not in the palette falls back to the nearest swatch.
	/// </summary>
	internal sealed class ParkPalette
	{
		#region Fields

		private const int SwatchPixels = 16;
		private static readonly Color32 Unused = new Color32(255, 0, 255, 255);

		private readonly Dictionary<string, Vector2> _uvs = new Dictionary<string, Vector2>();
		private readonly List<(Color32 Color, Vector2 Uv)> _swatches = new List<(Color32, Vector2)>();

		#endregion

		#region Setup

		private ParkPalette()
		{
		}

		#endregion

		#region Internal

		internal static ParkPalette Load(string path)
		{
			ParkPalette palette = new ParkPalette();
			Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
			texture.LoadImage(File.ReadAllBytes(path));
			int columns = texture.width / SwatchPixels, rows = texture.height / SwatchPixels;
			for (int row = 0; row < rows; row++)
			{
				for (int column = 0; column < columns; column++)
				{
					Color32 color = texture.GetPixel(column * SwatchPixels + SwatchPixels / 2, row * SwatchPixels + SwatchPixels / 2);
					if (color.r == Unused.r && color.g == Unused.g && color.b == Unused.b)
					{
						continue;
					}

					// Textures count rows from the bottom, as texture coordinates do
					Vector2 uv = new Vector2((column + 0.5f) / columns, (row + 0.5f) / rows);
					palette._uvs[Hex(color)] = uv;
					palette._swatches.Add((color, uv));
				}
			}

			Object.DestroyImmediate(texture);
			return palette;
		}

		/// <summary>Texture coordinates of the swatch with this colour (like "7a5c3e"), or of the nearest one.</summary>
		internal Vector2 Uv(string hex)
		{
			if (_uvs.TryGetValue(hex, out Vector2 uv))
			{
				return uv;
			}

			Color32 wanted = Parse(hex);
			float best = float.MaxValue;
			foreach ((Color32 color, Vector2 swatchUv) in _swatches)
			{
				float distance = Square(color.r - wanted.r) + Square(color.g - wanted.g) + Square(color.b - wanted.b);
				if (distance < best)
				{
					best = distance;
					uv = swatchUv;
				}
			}

			_uvs[hex] = uv;
			return uv;
		}

		#endregion

		#region Private

		private static string Hex(Color32 color)
		{
			return $"{color.r:x2}{color.g:x2}{color.b:x2}";
		}

		private static Color32 Parse(string hex)
		{
			int value = System.Convert.ToInt32(hex, 16);
			return new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
		}

		private static float Square(float value)
		{
			return value * value;
		}

		#endregion
	}
}
