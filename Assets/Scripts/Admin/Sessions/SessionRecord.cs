using System;
using System.Collections.Generic;
using System.Globalization;

namespace DroneDDW.Admin.Sessions
{
	// One row of the sessions file: its cells as written, read by column name
	internal sealed class SessionRecord
	{
		#region Fields

		// Shown for an empty or missing cell
		internal const string Missing = "–";

		private readonly IReadOnlyList<string> _header;
		private readonly string[] _cells;

		#endregion

		#region Setup

		internal SessionRecord(IReadOnlyList<string> header, string[] cells)
		{
			_header = header;
			_cells = cells;
		}

		#endregion

		#region Properties

		internal IReadOnlyList<string> Cells => _cells;

		// "001" for a numeric id, the id as written otherwise
		internal string DisplayId
		{
			get
			{
				string id = Get(SessionColumns.Id);
				if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number >= 0)
				{
					return number.ToString("000", CultureInfo.InvariantCulture);
				}

				return GetDisplay(SessionColumns.Id);
			}
		}

		#endregion

		#region Internal

		// The cell under this header (ignoring case and surrounding spaces), or "" when there is none
		internal string Get(string column)
		{
			for (int index = 0; index < _header.Count && index < _cells.Length; index++)
			{
				if (string.Equals(_header[index]?.Trim(), column, StringComparison.OrdinalIgnoreCase))
				{
					return _cells[index].Trim();
				}
			}

			return string.Empty;
		}

		// The cell for the table, or a dash when it is empty or missing
		internal string GetDisplay(string column)
		{
			string value = Get(column);
			return value.Length > 0 ? value : Missing;
		}

		internal bool HasCells(IReadOnlyList<string> cells)
		{
			if (cells.Count != _cells.Length)
			{
				return false;
			}

			for (int index = 0; index < _cells.Length; index++)
			{
				if (cells[index] != _cells[index])
				{
					return false;
				}
			}

			return true;
		}

		#endregion
	}
}
