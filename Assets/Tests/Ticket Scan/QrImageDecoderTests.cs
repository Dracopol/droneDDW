using NUnit.Framework;
using ZXing.Common;

namespace DroneDDW.TicketScan.Tests
{
	internal sealed class QrImageDecoderTests
	{
		#region Fields

		// As long as the tickets' survey links, so the code comes out the tickets' size: version 4, 33 x 33 modules,
		// with 26 modules between finder square centres
		private const string TicketPayload = "https://q.crowdtech.com/TestSurveyId0123456789?code=104";
		private const int TicketCodeModules = 33;
		private const float TicketFinderSpacingModules = 26f;

		#endregion

		#region Tests

		[Test]
		public void Encode_TicketLink_IsTheTicketsSize()
		{
			BitMatrix code = TicketImageUtil.Encode(TicketPayload);

			Assert.AreEqual(TicketCodeModules, code.Width);
		}

		[Test]
		public void Decode_TicketStyle_ReadsTheLinkInverted()
		{
			byte[] image = TicketImageUtil.DrawTicket(TicketImageUtil.Encode(TicketPayload), 10, false, out int size);

			QrDecode decode = new QrImageDecoder().Decode(image, size, size, false);

			Assert.IsTrue(decode.IsFound);
			Assert.AreEqual(TicketPayload, decode.Text);
			Assert.IsTrue(decode.IsInverted);
			Assert.IsFalse(decode.IsMirrored);
			Assert.AreEqual(10f, decode.ModulePixels, 1f);
			Assert.AreEqual(TicketFinderSpacingModules, decode.FinderSpacingModules);
		}

		[Test]
		public void Decode_TicketStyleAtThreePixelsAModule_ReadsTheLink()
		{
			byte[] image = TicketImageUtil.DrawTicket(TicketImageUtil.Encode(TicketPayload), 3, false, out int size);

			QrDecode decode = new QrImageDecoder().Decode(image, size, size, false);

			Assert.IsTrue(decode.IsFound);
			Assert.AreEqual(TicketPayload, decode.Text);
			Assert.AreEqual(TicketFinderSpacingModules, decode.FinderSpacingModules);
		}

		[Test]
		public void Decode_BottomUpRows_ReadsTheLinkUnmirrored()
		{
			byte[] image = TicketImageUtil.DrawTicket(TicketImageUtil.Encode(TicketPayload), 6, true, out int size);

			QrDecode decode = new QrImageDecoder().Decode(image, size, size, true);

			Assert.IsTrue(decode.IsFound);
			Assert.AreEqual(TicketPayload, decode.Text);
			Assert.IsFalse(decode.IsMirrored);
		}

		[Test]
		public void Decode_RowsTheWrongWayRound_ReadsTheLinkMirrored()
		{
			byte[] image = TicketImageUtil.DrawTicket(TicketImageUtil.Encode(TicketPayload), 6, false, out int size);

			QrDecode decode = new QrImageDecoder().Decode(image, size, size, true);

			Assert.IsTrue(decode.IsFound);
			Assert.AreEqual(TicketPayload, decode.Text);
			Assert.IsTrue(decode.IsMirrored);
		}

		[Test]
		public void Decode_PlainCode_ReadsTheLinkNotInverted()
		{
			byte[] image = TicketImageUtil.DrawPlain(TicketImageUtil.Encode(TicketPayload), 6, out int size);

			QrDecode decode = new QrImageDecoder().Decode(image, size, size, false);

			Assert.IsTrue(decode.IsFound);
			Assert.AreEqual(TicketPayload, decode.Text);
			Assert.IsFalse(decode.IsInverted);
		}

		[Test]
		public void Decode_BlankImage_FindsNothing()
		{
			const int size = 240;

			QrDecode decode = new QrImageDecoder().Decode(TicketImageUtil.DrawBlank(size), size, size, false);

			Assert.IsFalse(decode.IsFound);
			Assert.IsNull(decode.Text);
			Assert.IsTrue(float.IsNaN(decode.FinderSpacingModules));
		}

		[Test]
		public void Decode_AfterAnImageWithoutCode_StillReads()
		{
			QrImageDecoder decoder = new();
			byte[] ticket = TicketImageUtil.DrawTicket(TicketImageUtil.Encode(TicketPayload), 6, false, out int size);

			QrDecode first = decoder.Decode(ticket, size, size, false);
			QrDecode blank = decoder.Decode(TicketImageUtil.DrawBlank(size), size, size, false);
			QrDecode again = decoder.Decode(ticket, size, size, false);

			Assert.IsTrue(first.IsFound);
			Assert.IsFalse(blank.IsFound);
			Assert.IsTrue(again.IsFound);
			Assert.AreEqual(TicketPayload, again.Text);
		}

		#endregion
	}
}
