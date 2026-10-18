using System.Text;

namespace DroneDDW.Admin.Sessions
{
	// The session files' column headers the admin menu knows. Headers are lower case with underscores (form_choice),
	// which R, SPSS, Python and Excel all take as they are. Columns are found by name, ignoring case and treating
	// spaces like underscores, so files written before 2026-10-18 ("Form choice") still match; their header is
	// rewritten the new way the next time the app writes to them. Answer logging can add other columns; the menu keeps
	// them when it rewrites a file. Files written before 2026-10-11 also have a "first_time_participating" column, which
	// is kept but no longer filled.
	internal static class SessionColumns
	{
		#region Fields

		internal const string Id = "id";
		internal const string Group = "group";
		internal const string FormChoice = "form_choice";
		internal const string Date = "date";
		internal const string Time = "time";

		// Only in the deleted sessions file: when the row was deleted, as yyyy-MM-dd HH:mm:ss local time
		internal const string DeletedAt = "deleted_at";

		#endregion

		#region Internal

		// The header as the files write it: trimmed, lower case, each run of spaces, dashes or dots one underscore
		internal static string Normalize(string name)
		{
			StringBuilder result = new();
			bool isGap = false;
			foreach (char character in (name ?? string.Empty).Trim().ToLowerInvariant())
			{
				if (char.IsWhiteSpace(character) || character == '-' || character == '.' || character == '_')
				{
					isGap = true;
					continue;
				}

				if (isGap && result.Length > 0)
				{
					result.Append('_');
				}

				isGap = false;
				result.Append(character);
			}

			return result.ToString();
		}

		internal static bool IsSame(string header, string column)
		{
			return Normalize(header) == Normalize(column);
		}

		#endregion
	}
}
