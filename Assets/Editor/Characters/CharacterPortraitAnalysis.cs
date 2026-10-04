using System.IO;
using UnityEngine;

namespace DroneDDW.Characters.Editor
{
	// Measures portraits from their pixels: where the character is visible, and where the open and closed mouths
	// differ. Boxes are in pixels from the image's bottom left, like Unity's textures.
	internal static class CharacterPortraitAnalysis
	{
		#region Entities

		// Grows a box around the pixels added to it
		private sealed class BoxBuilder
		{
			private int _xMin = int.MaxValue;
			private int _yMin = int.MaxValue;
			private int _xMax = -1;
			private int _yMax = -1;

			internal void Add(int x, int y)
			{
				_xMin = Mathf.Min(_xMin, x);
				_yMin = Mathf.Min(_yMin, y);
				_xMax = Mathf.Max(_xMax, x);
				_yMax = Mathf.Max(_yMax, y);
			}

			// Max is exclusive, as in RectInt
			internal RectInt ToRect()
			{
				if (_xMax < 0)
				{
					return new RectInt(0, 0, 0, 0);
				}

				return new RectInt(_xMin, _yMin, _xMax - _xMin + 1, _yMax - _yMin + 1);
			}
		}

		#endregion

		#region Fields

		// Alpha above this counts as part of the character
		private const byte VisibleAlpha = 8;

		// A channel difference above this counts as a changed pixel (below it is compression noise)
		private const int DifferenceThreshold = 8;

		#endregion

		#region Internal

		// The file's pixels, read straight from the PNG so the import settings don't matter
		internal static Color32[] ReadPixels(string assetPath, out int width, out int height)
		{
			Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
			try
			{
				texture.LoadImage(File.ReadAllBytes(assetPath));
				width = texture.width;
				height = texture.height;
				return texture.GetPixels32();
			}
			finally
			{
				Object.DestroyImmediate(texture);
			}
		}

		// The smallest box holding every visible pixel; empty when nothing is visible
		internal static RectInt FindVisibleBox(Color32[] pixels, int width, int height)
		{
			BoxBuilder box = new();
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					if (pixels[y * width + x].a > VisibleAlpha)
					{
						box.Add(x, y);
					}
				}
			}

			return box.ToRect();
		}

		// The smallest box holding every pixel that differs between two images of the same size
		internal static RectInt FindDifferenceBox(Color32[] first, Color32[] second, int width, int height)
		{
			BoxBuilder box = new();
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					Color32 a = first[y * width + x];
					Color32 b = second[y * width + x];
					if (Mathf.Abs(a.r - b.r) > DifferenceThreshold || Mathf.Abs(a.g - b.g) > DifferenceThreshold
						|| Mathf.Abs(a.b - b.b) > DifferenceThreshold || Mathf.Abs(a.a - b.a) > DifferenceThreshold)
					{
						box.Add(x, y);
					}
				}
			}

			return box.ToRect();
		}

		// The part a card shows, as a fraction of the image: the visible box with a margin on the sides and top,
		// widened or heightened to the card's aspect (width / height) and centred on the character. It keeps to the
		// image's bottom edge when the character is cut off there, as these busts are.
		internal static Rect ToCropRect(RectInt visible, int width, int height, float aspect, int marginPixels)
		{
			float bottom = Mathf.Max(0, visible.yMin - marginPixels);
			float top = Mathf.Min(height, visible.yMax + marginPixels);
			float cropHeight = top - bottom;
			float cropWidth = cropHeight * aspect;
			float minWidth = visible.width + 2f * marginPixels;
			if (cropWidth < minWidth)
			{
				cropWidth = minWidth;
				cropHeight = Mathf.Min(height - bottom, cropWidth / aspect);
			}

			cropWidth = Mathf.Min(width, cropWidth);
			float left = Mathf.Clamp(visible.center.x - cropWidth * 0.5f, 0f, width - cropWidth);
			return new Rect(left / width, bottom / height, cropWidth / width, cropHeight / height);
		}

		internal static Rect ToNormalized(RectInt box, int width, int height)
		{
			return new Rect((float)box.xMin / width, (float)box.yMin / height, (float)box.width / width,
				(float)box.height / height);
		}

		internal static RectInt Union(RectInt first, RectInt second)
		{
			if (first.width == 0 || first.height == 0)
			{
				return second;
			}

			if (second.width == 0 || second.height == 0)
			{
				return first;
			}

			int xMin = Mathf.Min(first.xMin, second.xMin);
			int yMin = Mathf.Min(first.yMin, second.yMin);
			return new RectInt(xMin, yMin, Mathf.Max(first.xMax, second.xMax) - xMin,
				Mathf.Max(first.yMax, second.yMax) - yMin);
		}

		#endregion
	}
}
