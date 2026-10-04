namespace DroneDDW.Admin.Sessions
{
	// The session files' column headers the admin menu knows. Columns are found by these names (ignoring case), so
	// answer logging can add others; the menu keeps them when it rewrites a file.
	internal static class SessionColumns
	{
		#region Fields

		internal const string Id = "Id";
		internal const string Group = "Group";
		internal const string FirstTime = "First time participating";
		internal const string FormChoice = "Form choice";
		internal const string Date = "Date";
		internal const string Time = "Time";

		// Only in the deleted sessions file: when the row was deleted, as yyyy-MM-dd HH:mm:ss local time
		internal const string DeletedAt = "Deleted at";

		#endregion
	}
}
