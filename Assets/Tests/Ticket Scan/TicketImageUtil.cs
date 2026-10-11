using System.Collections.Generic;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace DroneDDW.TicketScan.Tests
{
	// Draws QR codes as RGBA images for the decoder: like the tickets print them (white modules on purple #663366, the
	// purple running a little past the code, on white paper), or plainly (black on white).
	internal static class TicketImageUtil
	{
		#region Fields

		private const int BytesPerPixel = 4;

		// Purple past the code's edge, and paper past the purple, in modules
		private const int TicketPurpleBorderModules = 2;
		private const int PaperModules = 4;

		private static readonly byte[] White = { 255, 255, 255, 255 };
		private static readonly byte[] Black = { 0, 0, 0, 255 };
		private static readonly byte[] TicketPurple = { 0x66, 0x33, 0x66, 255 };

		#endregion

		#region Internal

		// The code's modules without a border, true for the modules QR calls dark, at the tickets' error correction (M)
		internal static BitMatrix Encode(string payload)
		{
			Dictionary<EncodeHintType, object> hints = new()
			{
				[EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.M,
				[EncodeHintType.MARGIN] = 0,
			};
			return new QRCodeWriter().encode(payload, BarcodeFormat.QR_CODE, 0, 0, hints);
		}

		// White modules on purple, the purple on paper. Rows top to bottom, or bottom to top when isBottomUp.
		internal static byte[] DrawTicket(BitMatrix code, int modulePixels, bool isBottomUp, out int size)
		{
			return Draw(code, modulePixels, White, TicketPurple, TicketPurpleBorderModules, isBottomUp, out size);
		}

		// Black modules on white paper, rows top to bottom
		internal static byte[] DrawPlain(BitMatrix code, int modulePixels, out int size)
		{
			return Draw(code, modulePixels, Black, White, 0, false, out size);
		}

		// One colour all over
		internal static byte[] DrawBlank(int size)
		{
			byte[] pixels = new byte[size * size * BytesPerPixel];
			for (int pixel = 0; pixel < size * size; pixel++)
			{
				White.CopyTo(pixels, pixel * BytesPerPixel);
			}

			return pixels;
		}

		#endregion

		#region Private

		// A square image: the code's dark modules in moduleColor and its light ones in backgroundColor, which also runs
		// borderModules past the code, then white paper all round
		private static byte[] Draw(BitMatrix code, int modulePixels, byte[] moduleColor, byte[] backgroundColor,
			int borderModules, bool isBottomUp, out int size)
		{
			int codeModules = code.Width;
			int imageModules = codeModules + 2 * (borderModules + PaperModules);
			size = imageModules * modulePixels;
			byte[] pixels = new byte[size * size * BytesPerPixel];
			for (int y = 0; y < size; y++)
			{
				int row = y / modulePixels - PaperModules - borderModules;
				int pixelRow = isBottomUp ? size - 1 - y : y;
				for (int x = 0; x < size; x++)
				{
					int column = x / modulePixels - PaperModules - borderModules;
					byte[] color = GetColor(code, row, column, borderModules, moduleColor, backgroundColor);
					color.CopyTo(pixels, (pixelRow * size + x) * BytesPerPixel);
				}
			}

			return pixels;
		}

		private static byte[] GetColor(BitMatrix code, int row, int column, int borderModules, byte[] moduleColor,
			byte[] backgroundColor)
		{
			int codeModules = code.Width;
			bool isInCode = row >= 0 && row < codeModules && column >= 0 && column < codeModules;
			if (isInCode)
			{
				return code[column, row] ? moduleColor : backgroundColor;
			}

			bool isInBorder = row >= -borderModules && row < codeModules + borderModules && column >= -borderModules
				&& column < codeModules + borderModules;
			return isInBorder ? backgroundColor : White;
		}

		#endregion
	}
}
