using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DroneDDW.Admin.Sessions
{
	// The session history files: Sessions.csv (one row per session, written by answer logging) and Deleted
	// Sessions.csv, where the admin menu moves deleted rows with the time they were deleted. Both are UTF-8 with a byte
	// order mark, so Excel reads them right. Every call reads the files fresh, so rows logged meanwhile aren't lost.
	internal sealed class SessionHistory
	{
		#region Fields

		private const string DeletedAtFormat = "yyyy-MM-dd HH:mm:ss";
		private const string LineBreak = "\r\n";
		private const string TemporarySuffix = ".tmp";

		private static readonly Encoding FileEncoding = new UTF8Encoding(true);

		private readonly char _preferredSeparator;

		#endregion

		#region Setup

		internal SessionHistory(string folder, string sessionsFileName, string deletedFileName, char preferredSeparator)
		{
			Folder = folder;
			SessionsPath = Path.Combine(folder, sessionsFileName);
			DeletedPath = Path.Combine(folder, deletedFileName);
			_preferredSeparator = preferredSeparator;
		}

		#endregion

		#region Properties

		internal string Folder { get; }
		internal string SessionsPath { get; }
		internal string DeletedPath { get; }

		#endregion

		#region Internal

		// The sessions in file order; none when the file doesn't exist yet. Throws IOException when it can't be read.
		internal List<SessionRecord> Load()
		{
			List<SessionRecord> records = new();
			if (!TryReadTable(SessionsPath, out string[] header, out List<string[]> rows, out _))
			{
				return records;
			}

			foreach (string[] row in rows)
			{
				records.Add(new SessionRecord(header, row));
			}

			return records;
		}

		// Moves the session's row from the sessions file to the deleted sessions file. False when the row isn't in the
		// file any more. The deleted file is written first, so a failure halfway leaves the row in both, never in neither.
		internal bool Delete(SessionRecord record, DateTime deletedAt)
		{
			if (!TryReadTable(SessionsPath, out string[] header, out List<string[]> rows, out char separator))
			{
				return false;
			}

			int index = rows.FindIndex(record.HasCells);
			if (index < 0)
			{
				return false;
			}

			string[] row = rows[index];
			AppendDeleted(header, row, deletedAt, separator);
			rows.RemoveAt(index);
			WriteTable(SessionsPath, header, rows, separator);
			return true;
		}

		#endregion

		#region Private

		// False when the file is missing or empty
		private bool TryReadTable(string path, out string[] header, out List<string[]> rows, out char separator)
		{
			header = Array.Empty<string>();
			rows = new List<string[]>();
			separator = _preferredSeparator;
			if (!File.Exists(path))
			{
				return false;
			}

			string text = File.ReadAllText(path, FileEncoding);
			separator = CsvUtil.DetectSeparator(text, separator);
			List<string[]> table = CsvUtil.Parse(text, separator);
			if (table.Count == 0)
			{
				return false;
			}

			header = table[0];
			table.RemoveAt(0);
			rows = table;
			return true;
		}

		// A new deleted sessions file takes the sessions file's separator
		private void AppendDeleted(string[] header, string[] row, DateTime deletedAt, char sessionsSeparator)
		{
			string deletedAtCell = deletedAt.ToString(DeletedAtFormat, CultureInfo.InvariantCulture);

			// A short row (edited by hand) is padded, so the delete time lands under its header
			if (row.Length < header.Length)
			{
				string[] padded = new string[header.Length];
				row.CopyTo(padded, 0);
				for (int index = row.Length; index < padded.Length; index++)
				{
					padded[index] = string.Empty;
				}

				row = padded;
			}

			Directory.CreateDirectory(Folder);
			if (TryReadTable(DeletedPath, out _, out _, out char separator))
			{
				string line = CsvUtil.FormatRow(Append(row, deletedAtCell), separator) + LineBreak;
				File.AppendAllText(DeletedPath, EnsureLeadingLineBreak(line), FileEncoding);
				return;
			}

			// A new file takes the sessions file's columns plus the delete time
			StringBuilder text = new();
			text.Append(CsvUtil.FormatRow(Append(header, SessionColumns.DeletedAt), sessionsSeparator));
			text.Append(LineBreak);
			text.Append(CsvUtil.FormatRow(Append(row, deletedAtCell), sessionsSeparator));
			text.Append(LineBreak);
			File.WriteAllText(DeletedPath, text.ToString(), FileEncoding);
		}

		// When the file doesn't end with a line break (edited by hand), the new row still starts on its own line
		private string EnsureLeadingLineBreak(string line)
		{
			using FileStream stream = new(DeletedPath, FileMode.Open, FileAccess.Read);
			if (stream.Length == 0)
			{
				return line;
			}

			stream.Seek(-1, SeekOrigin.End);
			int last = stream.ReadByte();
			return last == '\n' ? line : LineBreak + line;
		}

		// Writes a temporary file, then swaps it in, so a failure never leaves a half-written sessions file
		private static void WriteTable(string path, string[] header, List<string[]> rows, char separator)
		{
			StringBuilder text = new();
			text.Append(CsvUtil.FormatRow(header, separator));
			text.Append(LineBreak);
			foreach (string[] row in rows)
			{
				text.Append(CsvUtil.FormatRow(row, separator));
				text.Append(LineBreak);
			}

			string temporaryPath = path + TemporarySuffix;
			File.WriteAllText(temporaryPath, text.ToString(), FileEncoding);
			try
			{
				File.Replace(temporaryPath, path, null);
			}
			catch (PlatformNotSupportedException)
			{
				File.Copy(temporaryPath, path, true);
				File.Delete(temporaryPath);
			}
		}

		private static string[] Append(string[] cells, string cell)
		{
			string[] result = new string[cells.Length + 1];
			cells.CopyTo(result, 0);
			result[cells.Length] = cell;
			return result;
		}

		#endregion
	}
}
