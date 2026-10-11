using System;
using System.Globalization;

namespace DroneDDW.TicketScan
{
	// Reads the participant number from a ticket's QR code. The tickets' codes are a survey link carrying the number
	// as its code parameter (https://q.crowdtech.com/<survey>?code=104); a code that is just the number reads too. The
	// survey part isn't checked, so tickets printed for another survey still work.
	internal static class TicketCodeUtil
	{
		#region Fields

		// Longer numbers aren't participant numbers (and would overflow an int)
		internal const int MaxDigits = 6;

		private const string NumberParameter = "code";

		#endregion

		#region Internal

		// False for anything that isn't a ticket, e.g. another QR code or a link without a number
		internal static bool TryGetNumber(string payload, out int number)
		{
			number = -1;
			if (string.IsNullOrWhiteSpace(payload))
			{
				return false;
			}

			string text = payload.Trim();
			if (TryParseDigits(text, out number))
			{
				return true;
			}

			int queryStart = text.IndexOf('?');
			if (queryStart < 0)
			{
				return false;
			}

			int queryEnd = text.IndexOf('#', queryStart);
			string query = queryEnd < 0
				? text.Substring(queryStart + 1)
				: text.Substring(queryStart + 1, queryEnd - queryStart - 1);
			foreach (string parameter in query.Split('&'))
			{
				int equalsIndex = parameter.IndexOf('=');
				if (equalsIndex > 0 && string.Equals(parameter.Substring(0, equalsIndex), NumberParameter,
					StringComparison.OrdinalIgnoreCase))
				{
					return TryParseDigits(parameter.Substring(equalsIndex + 1), out number);
				}
			}

			return false;
		}

		#endregion

		#region Private

		// Only plain digits: no sign, spaces or separators
		private static bool TryParseDigits(string text, out int number)
		{
			number = -1;
			if (text.Length == 0 || text.Length > MaxDigits)
			{
				return false;
			}

			foreach (char character in text)
			{
				if (character < '0' || character > '9')
				{
					return false;
				}
			}

			number = int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
			return true;
		}

		#endregion
	}
}
