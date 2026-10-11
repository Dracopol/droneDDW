using NUnit.Framework;

namespace DroneDDW.TicketScan.Tests
{
	internal sealed class TicketCodeUtilTests
	{
		#region Tests

		[TestCase("https://q.crowdtech.com/TestSurveyId0123456789?code=104", 104)]
		[TestCase("https://example.com/survey?a=1&code=7", 7)]
		[TestCase("https://example.com/survey?CODE=105", 105)]
		[TestCase("https://example.com/survey?code=106#start", 106)]
		[TestCase("104", 104)]
		[TestCase(" 104 \n", 104)]
		[TestCase("000123", 123)]
		public void TryGetNumber_Ticket_ReadsTheNumber(string payload, int expected)
		{
			bool isTicket = TicketCodeUtil.TryGetNumber(payload, out int number);

			Assert.IsTrue(isTicket);
			Assert.AreEqual(expected, number);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		[TestCase("https://q.crowdtech.com/TestSurveyId0123456789")]
		[TestCase("https://q.crowdtech.com/TestSurveyId0123456789?other=104")]
		[TestCase("https://example.com/?barcode=104")]
		[TestCase("https://q.crowdtech.com/TestSurveyId0123456789?code=abc")]
		[TestCase("https://q.crowdtech.com/TestSurveyId0123456789?code=")]
		[TestCase("https://q.crowdtech.com/TestSurveyId0123456789?code=-5")]
		[TestCase("https://q.crowdtech.com/TestSurveyId0123456789?code=1234567")]
		[TestCase("1234567")]
		[TestCase("10 4")]
		[TestCase("WIFI:S:Network;T:WPA;P:secret;;")]
		public void TryGetNumber_NotATicket_ReturnsFalse(string payload)
		{
			bool isTicket = TicketCodeUtil.TryGetNumber(payload, out int number);

			Assert.IsFalse(isTicket);
			Assert.AreEqual(-1, number);
		}

		[Test]
		public void TicketRead_TicketLink_HasItsNumber()
		{
			TicketRead read = new("https://q.crowdtech.com/TestSurveyId0123456789?code=101", float.NaN, float.NaN,
				float.NaN, 0f);

			Assert.IsTrue(read.IsTicket);
			Assert.AreEqual(101, read.Number);
		}

		[Test]
		public void TicketRead_OtherCode_IsNoTicket()
		{
			TicketRead read = new("https://example.com/", float.NaN, float.NaN, float.NaN, 0f);

			Assert.IsFalse(read.IsTicket);
			Assert.AreEqual(-1, read.Number);
		}

		#endregion
	}
}
