using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DroneDDW.Admin.Sessions
{
	// The session history files: Sessions.csv (one row per session, written by Session Control when the participant
	// locks in their choice, and by the admin menu's test save button) and Deleted Sessions.csv, where the admin menu
	// moves deleted rows with the time they were deleted. Both are UTF-8 with a byte order mark, so Excel reads them
	// right. Every call reads the files fresh, so rows logged meanwhile aren't lost.
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

		// The files named in the admin menu's settings, in their folder under the app's persistent data, or in
		// folderOverride when it isn't null (the Play mode checks use a temporary one)
		internal static SessionHistory FromSettings(AdminMenuSettings settings, string folderOverride)
		{
			string folder = folderOverride ?? Path.Combine(Application.persistentDataPath, settings.SessionsFolder);
			return new SessionHistory(folder, settings.SessionsFileName, settings.DeletedSessionsFileName,
				settings.Separator);
		}

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

		// Adds a row to the sessions file. A new file takes these columns, in this order, as its header. An existing one
		// keeps its columns and separator: each value goes under its column (by name, ignoring case) and the file's
		// other columns stay empty. Returns the columns the existing file doesn't have, whose values were left out.
		internal List<string> Append(IReadOnlyList<(string Column, string Value)> cells)
		{
			List<string> missingColumns = new();
			Directory.CreateDirectory(Folder);
			if (!TryReadTable(SessionsPath, out string[] header, out _, out char separator))
			{
				string[] newHeader = new string[cells.Count];
				string[] newRow = new string[cells.Count];
				for (int index = 0; index < cells.Count; index++)
				{
					newHeader[index] = cells[index].Column;
					newRow[index] = cells[index].Value;
				}

				string text = CsvUtil.FormatRow(newHeader, _preferredSeparator) + LineBreak
					+ CsvUtil.FormatRow(newRow, _preferredSeparator) + LineBreak;
				File.WriteAllText(SessionsPath, text, FileEncoding);
				return missingColumns;
			}

			string[] row = new string[header.Length];
			for (int index = 0; index < row.Length; index++)
			{
				row[index] = string.Empty;
			}

			foreach ((string column, string value) in cells)
			{
				int index = Array.FindIndex(header,
					name => string.Equals(name?.Trim(), column, StringComparison.OrdinalIgnoreCase));
				if (index >= 0)
				{
					row[index] = value;
				}
				else
				{
					missingColumns.Add(column);
				}
			}

			string line = CsvUtil.FormatRow(row, separator) + LineBreak;
			File.AppendAllText(SessionsPath, EnsureLeadingLineBreak(SessionsPath, line), FileEncoding);
			return missingColumns;
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
				string line = CsvUtil.FormatRow(WithCell(row, deletedAtCell), separator) + LineBreak;
				File.AppendAllText(DeletedPath, EnsureLeadingLineBreak(DeletedPath, line), FileEncoding);
				return;
			}

			// A new file takes the sessions file's columns plus the delete time
			StringBuilder text = new();
			text.Append(CsvUtil.FormatRow(WithCell(header, SessionColumns.DeletedAt), sessionsSeparator));
			text.Append(LineBreak);
			text.Append(CsvUtil.FormatRow(WithCell(row, deletedAtCell), sessionsSeparator));
			text.Append(LineBreak);
			File.WriteAllText(DeletedPath, text.ToString(), FileEncoding);
		}

		// When the file doesn't end with a line break (edited by hand), the new row still starts on its own line
		private static string EnsureLeadingLineBreak(string path, string line)
		{
			using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
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

		private static string[] WithCell(string[] cells, string cell)
		{
			string[] result = new string[cells.Length + 1];
			cells.CopyTo(result, 0);
			result[cells.Length] = cell;
			return result;
		}

		#endregion
	}
}
