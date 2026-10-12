using System.Collections.Generic;
using System.IO;
using Unity.VectorGraphics;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Admin.Editor
{
	// The menu's icons, drawn as SVG in the style of the Lucide set (24 x 24, 2-unit round strokes) like the mockups,
	// then baked here into white PNG sprites with mipmaps, so they stay smooth at a distance in the headset. The SVG is
	// tessellated by Unity's vector graphics module and rasterised on the CPU with 4 x 4 samples per pixel.
	internal static class AdminMenuIconBaker
	{
		#region Fields

		internal const string IconFolder = "Assets/Content/Textures/Admin/Icons";

		private const int IconPixels = 96;
		private const int SamplesPerAxis = 4;
		private const float ViewBoxUnits = 24f;

		private const string Header = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" "
			+ "viewBox=\"0 0 24 24\">";

		private const string Stroke = "fill=\"none\" stroke=\"#FFFFFF\" stroke-width=\"2\" stroke-linecap=\"round\" "
			+ "stroke-linejoin=\"round\"";

		internal static readonly string PlaySvg = Header
			+ $"<path {Stroke} d=\"M6 3 L20 12 L6 21 Z\"/></svg>";

		internal static readonly string ResetSvg = Header
			+ $"<path {Stroke} d=\"M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8\"/>"
			+ $"<path {Stroke} d=\"M3 3v5h5\"/></svg>";

		internal static readonly string HistorySvg = Header
			+ $"<path {Stroke} d=\"M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8\"/>"
			+ $"<path {Stroke} d=\"M3 3v5h5\"/>"
			+ $"<path {Stroke} d=\"M12 7v5l4 2\"/></svg>";

		internal static readonly string LocationSvg = Header
			+ $"<path {Stroke} d=\"M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10"
			+ "a8 8 0 0 1 16 0\"/>"
			+ $"<circle {Stroke} cx=\"12\" cy=\"10\" r=\"3\"/></svg>";

		internal static readonly string LanguageSvg = Header
			+ $"<circle {Stroke} cx=\"12\" cy=\"12\" r=\"10\"/>"
			+ $"<path {Stroke} d=\"M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20\"/>"
			+ $"<path {Stroke} d=\"M2 12h20\"/></svg>";

		internal static readonly string SaveSvg = Header
			+ $"<path {Stroke} d=\"M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5"
			+ "a2 2 0 0 1 2-2z\"/>"
			+ $"<path {Stroke} d=\"M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7\"/>"
			+ $"<path {Stroke} d=\"M7 3v4a1 1 0 0 0 1 1h7\"/></svg>";

		internal static readonly string BackSvg = Header
			+ $"<path {Stroke} d=\"M12 19l-7-7 7-7\"/>"
			+ $"<path {Stroke} d=\"M19 12H5\"/></svg>";

		// Lucide's log-out: a door with an arrow leaving it
		internal static readonly string EndSvg = Header
			+ $"<path {Stroke} d=\"M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4\"/>"
			+ $"<path {Stroke} d=\"M16 17l5-5-5-5\"/>"
			+ $"<path {Stroke} d=\"M21 12H9\"/></svg>";

		#endregion

		#region Internal

		// Writes the icon's PNG (white, the shape in its alpha) and imports it as a sprite with mipmaps
		internal static Sprite Bake(string name, string svg)
		{
			string path = $"{IconFolder}/{name}.png";
			AdminMenuAssetBuilder.EnsureFolder(IconFolder);
			Color32[] pixels = RasterizeSvg(svg, IconPixels);
			Texture2D texture = new(IconPixels, IconPixels, TextureFormat.RGBA32, false);
			try
			{
				texture.SetPixels32(pixels);
				File.WriteAllBytes(path, texture.EncodeToPNG());
			}
			finally
			{
				Object.DestroyImmediate(texture);
			}

			AssetDatabase.ImportAsset(path);
			TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Single;
			importer.alphaIsTransparency = true;
			importer.mipmapEnabled = true;
			importer.filterMode = FilterMode.Trilinear;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();
			return AssetDatabase.LoadAssetAtPath<Sprite>(path);
		}

		// White pixels whose alpha is the share of the pixel the SVG's shapes cover; row 0 is the bottom
		internal static Color32[] RasterizeSvg(string svg, int pixelSize)
		{
			SVGParser.SceneInfo sceneInfo = SVGParser.ImportSVG(new StringReader(svg), 0f, 1f, 0, 0, false);
			VectorUtils.TessellationOptions options = new()
			{
				StepDistance = 0.25f,
				MaxCordDeviation = 0.01f,
				MaxTanAngleDeviation = 0.02f,
				SamplingStepSize = 0.01f,
			};

			List<VectorUtils.Geometry> geometries =
				VectorUtils.TessellateScene(sceneInfo.Scene, options, sceneInfo.NodeOpacity);
			List<Vector2> vertices = new();
			List<int> indices = new();
			foreach (VectorUtils.Geometry geometry in geometries)
			{
				int offset = vertices.Count;
				foreach (Vector2 vertex in geometry.Vertices)
				{
					vertices.Add(geometry.WorldTransform.MultiplyPoint(vertex));
				}

				foreach (ushort index in geometry.Indices)
				{
					indices.Add(offset + index);
				}
			}

			return Rasterize(vertices, indices, ViewBoxUnits, pixelSize);
		}

		// Triangles in view box units (y down, as in SVG) to white pixels with coverage in alpha, row 0 at the bottom
		internal static Color32[] Rasterize(IReadOnlyList<Vector2> vertices, IReadOnlyList<int> indices,
			float viewBoxUnits, int pixelSize)
		{
			int samples = pixelSize * SamplesPerAxis;
			float samplesPerUnit = samples / viewBoxUnits;
			bool[] covered = new bool[samples * samples];
			for (int triangle = 0; triangle + 2 < indices.Count; triangle += 3)
			{
				CoverTriangle(covered, samples, vertices[indices[triangle]] * samplesPerUnit,
					vertices[indices[triangle + 1]] * samplesPerUnit, vertices[indices[triangle + 2]] * samplesPerUnit);
			}

			Color32[] pixels = new Color32[pixelSize * pixelSize];
			for (int y = 0; y < pixelSize; y++)
			{
				for (int x = 0; x < pixelSize; x++)
				{
					int count = 0;
					for (int sampleY = 0; sampleY < SamplesPerAxis; sampleY++)
					{
						for (int sampleX = 0; sampleX < SamplesPerAxis; sampleX++)
						{
							int index = (y * SamplesPerAxis + sampleY) * samples + x * SamplesPerAxis + sampleX;
							count += covered[index] ? 1 : 0;
						}
					}

					byte alpha = (byte)Mathf.RoundToInt(255f * count / (SamplesPerAxis * SamplesPerAxis));

					// SVG rows go down, texture rows go up
					pixels[(pixelSize - 1 - y) * pixelSize + x] = new Color32(255, 255, 255, alpha);
				}
			}

			return pixels;
		}

		#endregion

		#region Private

		// Marks the samples whose centres lie inside the triangle, whichever way it winds
		private static void CoverTriangle(bool[] covered, int samples, Vector2 a, Vector2 b, Vector2 c)
		{
			float area = Cross(b - a, c - a);
			if (Mathf.Abs(area) < 1e-6f)
			{
				return;
			}

			int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
			int maxX = Mathf.Min(samples - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
			int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
			int maxY = Mathf.Min(samples - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
			float sign = Mathf.Sign(area);
			for (int y = minY; y <= maxY; y++)
			{
				for (int x = minX; x <= maxX; x++)
				{
					Vector2 point = new(x + 0.5f, y + 0.5f);
					bool isInside = sign * Cross(b - a, point - a) >= 0f
						&& sign * Cross(c - b, point - b) >= 0f
						&& sign * Cross(a - c, point - c) >= 0f;
					if (isInside)
					{
						covered[y * samples + x] = true;
					}
				}
			}
		}

		private static float Cross(Vector2 first, Vector2 second)
		{
			return first.x * second.y - first.y * second.x;
		}

		#endregion
	}
}
