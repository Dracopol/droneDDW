using UnityEngine;

namespace DroneDDW.TicketScan
{
	// What QrImageDecoder found in one image. Points are image pixels from the top-left corner, y down.
	internal readonly struct QrDecode
	{
		#region Fields

		// A QR code's finder squares are 7 modules wide, and its side is 21 modules plus 4 per version after the first
		private const int FinderSizeModules = 7;
		private const int MinSizeModules = 21;
		private const int ModulesPerVersion = 4;

		#endregion

		#region Properties

		internal bool IsFound => Text != null;

		internal string Text { get; }

		// The centres of the code's three finder squares
		internal Vector2 BottomLeft { get; }
		internal Vector2 TopLeft { get; }
		internal Vector2 TopRight { get; }

		// The module size ZXing measured on the finder squares, in pixels, or NaN when it didn't say
		internal float ModulePixels { get; }

		// Light code on a dark background, like the tickets' white on purple
		internal bool IsInverted { get; }

		// Read as in a mirror, as when the image's rows run the other way than the decoder was told: ZXing still reads
		// such codes, and the finder squares then go round the other way
		internal bool IsMirrored => IsFound && Cross(TopRight - TopLeft, BottomLeft - TopLeft) < 0f;

		// How long the decode took, found or not
		internal float DecodeMilliseconds { get; }

		// The modules from one finder square's centre to the next along a side (26 for the tickets' 33-module codes):
		// measured, then snapped to a real QR size. NaN without a module size.
		internal float FinderSpacingModules
		{
			get
			{
				if (!IsFound || float.IsNaN(ModulePixels) || ModulePixels <= 0f)
				{
					return float.NaN;
				}

				float sides = Vector2.Distance(TopLeft, TopRight) + Vector2.Distance(TopLeft, BottomLeft);
				float measured = sides * 0.5f / ModulePixels;
				int minSpacing = MinSizeModules - FinderSizeModules;
				float versionsAbove = Mathf.Max(0f, Mathf.Round((measured - minSpacing) / ModulesPerVersion));
				return minSpacing + versionsAbove * ModulesPerVersion;
			}
		}

		#endregion

		#region Setup

		internal QrDecode(string text, Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, float modulePixels,
			bool isInverted, float decodeMilliseconds)
		{
			Text = text;
			BottomLeft = bottomLeft;
			TopLeft = topLeft;
			TopRight = topRight;
			ModulePixels = modulePixels;
			IsInverted = isInverted;
			DecodeMilliseconds = decodeMilliseconds;
		}

		#endregion

		#region Internal

		// No code in the image; the time still counts towards the scan rate
		internal static QrDecode NotFound(float decodeMilliseconds)
		{
			return new QrDecode(null, Vector2.zero, Vector2.zero, Vector2.zero, float.NaN, false, decodeMilliseconds);
		}

		#endregion

		#region Private

		private static float Cross(Vector2 first, Vector2 second)
		{
			return first.x * second.y - first.y * second.x;
		}

		#endregion
	}
}
