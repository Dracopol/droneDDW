using System;
using System.Collections.Generic;
using System.Text;

namespace DroneDDW.Admin.Sessions
{
	// Reads and writes CSV the way Excel does (RFC 4180): cells holding the separator, quotes or line breaks are quoted,
	// with quotes doubled. The separator is ';' or ',' depending on the file.
	internal static class CsvUtil
	{
		#region Fields

		private const char Quote = '"';
		private const char ByteOrderMark = '﻿';

		private static readonly char[] KnownSeparators = { ';', ',', '\t' };

		#endregion

		#region Internal

		// The rows' cells. A leading byte order mark is skipped and blank lines are dropped.
		internal static List<string[]> Parse(string text, char separator)
		{
			List<string[]> rows = new();
			List<string> cells = new();
			StringBuilder cell = new();
			bool isQuoted = false;
			bool isRowStarted = false;
			int start = text.Length > 0 && text[0] == ByteOrderMark ? 1 : 0;
			for (int index = start; index < text.Length; index++)
			{
				char character = text[index];
				if (isQuoted)
				{
					if (character != Quote)
					{
						cell.Append(character);
					}
					else if (index + 1 < text.Length && text[index + 1] == Quote)
					{
						cell.Append(Quote);
						index++;
					}
					else
					{
						isQuoted = false;
					}

					continue;
				}

				if (character == Quote)
				{
					isQuoted = true;
					isRowStarted = true;
				}
				else if (character == separator)
				{
					cells.Add(cell.ToString());
					cell.Clear();
					isRowStarted = true;
				}
				else if (character == '\r' || character == '\n')
				{
					if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
					{
						index++;
					}

					EndRow();
				}
				else
				{
					cell.Append(character);
					isRowStarted = true;
				}
			}

			EndRow();
			return rows;

			void EndRow()
			{
				if (isRowStarted)
				{
					cells.Add(cell.ToString());
					rows.Add(cells.ToArray());
				}

				cells.Clear();
				cell.Clear();
				isRowStarted = false;
			}
		}

		// One line, without the line break
		internal static string FormatRow(IReadOnlyList<string> cells, char separator)
		{
			StringBuilder line = new();
			for (int index = 0; index < cells.Count; index++)
			{
				if (index > 0)
				{
					line.Append(separator);
				}

				AppendCell(line, cells[index] ?? string.Empty, separator);
			}

			return line.ToString();
		}

		// The separator a file uses: the known separator found most often outside quotes in its first line (so a comma
		// inside a column name doesn't fool it), the preferred one on a tie, and the preferred one for a single column
		internal static char DetectSeparator(string text, char preferred)
		{
			bool isQuoted = false;
			Dictionary<char, int> counts = new();
			foreach (char character in text)
			{
				if (character == Quote)
				{
					isQuoted = !isQuoted;
				}
				else if (!isQuoted && (character == '\r' || character == '\n'))
				{
					break;
				}
				else if (!isQuoted && (character == preferred || Array.IndexOf(KnownSeparators, character) >= 0))
				{
					counts.TryGetValue(character, out int count);
					counts[character] = count + 1;
				}
			}

			counts.TryGetValue(preferred, out int best);
			char separator = preferred;
			foreach (char candidate in KnownSeparators)
			{
				if (counts.TryGetValue(candidate, out int count) && count > best)
				{
					best = count;
					separator = candidate;
				}
			}

			return separator;
		}

		#endregion

		#region Private

		private static void AppendCell(StringBuilder line, string cell, char separator)
		{
			bool isQuoted = cell.IndexOf(separator) >= 0 || cell.IndexOf(Quote) >= 0 || cell.IndexOf('\r') >= 0
				|| cell.IndexOf('\n') >= 0;
			if (!isQuoted)
			{
				line.Append(cell);
				return;
			}

			line.Append(Quote);
			line.Append(cell.Replace("\"", "\"\""));
			line.Append(Quote);
		}

		#endregion
	}
}
