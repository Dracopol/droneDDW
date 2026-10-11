using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace DroneDDW.TicketScan
{
	// Finds and decodes a QR code in an RGBA image with ZXing: the image inverted first, since the tickets print their
	// code light on dark, then as it is. Safe on a worker thread, but one image at a time: it reuses its buffers.
	internal sealed class QrImageDecoder
	{
		#region Fields

		private const int BytesPerPixel = 4;

		private readonly QRCodeReader _reader = new();
		private readonly Dictionary<DecodeHintType, object> _hints = new() { [DecodeHintType.TRY_HARDER] = true };
		private readonly Stopwatch _stopwatch = new();
		private byte[] _luminance = Array.Empty<byte>();

		#endregion

		#region Internal

		// rgba holds width x height pixels of 4 bytes, its rows bottom to top when isBottomUp (how Unity stores
		// textures and reads them back from the GPU), else top to bottom
		internal QrDecode Decode(byte[] rgba, int width, int height, bool isBottomUp)
		{
			int pixelCount = width * height;
			if (rgba == null || width <= 0 || height <= 0 || rgba.Length < pixelCount * BytesPerPixel)
			{
				throw new ArgumentException($"Expected {width} x {height} RGBA pixels.", nameof(rgba));
			}

			_stopwatch.Restart();
			if (_luminance.Length != pixelCount)
			{
				_luminance = new byte[pixelCount];
			}

			FillLuminance(rgba, width, height, isBottomUp, _luminance);
			LuminanceSource image = new PlanarYUVLuminanceSource(_luminance, width, height, 0, 0, width, height, false);
			bool isInverted = true;
			Result result = TryDecode(new InvertedLuminanceSource(image));
			if (result == null)
			{
				isInverted = false;
				result = TryDecode(image);
			}

			_stopwatch.Stop();
			float milliseconds = (float)_stopwatch.Elapsed.TotalMilliseconds;
			ResultPoint[] points = result?.ResultPoints;
			if (points == null || points.Length < 3 || result.Text == null)
			{
				return QrDecode.NotFound(milliseconds);
			}

			return new QrDecode(result.Text, ToVector(points[0]), ToVector(points[1]), ToVector(points[2]),
				GetModulePixels(points), isInverted, milliseconds);
		}

		#endregion

		#region Private

		private Result TryDecode(LuminanceSource image)
		{
			try
			{
				return _reader.decode(new BinaryBitmap(new HybridBinarizer(image)), _hints);
			}
			catch (ReaderException)
			{
				// No code, or one too damaged to read
				return null;
			}
			finally
			{
				_reader.reset();
			}
		}

		// Rec. 601 luma in integers (the weights add up to 256), the image's top row first
		private static void FillLuminance(byte[] rgba, int width, int height, bool isBottomUp, byte[] luminance)
		{
			for (int row = 0; row < height; row++)
			{
				int source = (isBottomUp ? height - 1 - row : row) * width * BytesPerPixel;
				int target = row * width;
				for (int column = 0; column < width; column++)
				{
					luminance[target + column] = (byte)((rgba[source] * 77 + rgba[source + 1] * 150
						+ rgba[source + 2] * 29) >> 8);
					source += BytesPerPixel;
				}
			}
		}

		// The three finder squares' average module size, or NaN when ZXing gave plain points
		private static float GetModulePixels(ResultPoint[] points)
		{
			float sum = 0f;
			for (int index = 0; index < 3; index++)
			{
				if (points[index] is not FinderPattern finder)
				{
					return float.NaN;
				}

				sum += finder.EstimatedModuleSize;
			}

			return sum / 3f;
		}

		private static Vector2 ToVector(ResultPoint point)
		{
			return new Vector2(point.X, point.Y);
		}

		#endregion
	}
}
