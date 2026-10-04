using System.Collections.Generic;
using DroneDDW.Admin.Sessions;
using NUnit.Framework;

namespace DroneDDW.Admin.Tests
{
	internal sealed class CsvUtilTests
	{
		#region Tests

		[Test]
		public void Parse_SplitsRowsAndCells()
		{
			List<string[]> rows = CsvUtil.Parse("Id;Group\r\n001;A\r\n002;B\r\n", ';');

			Assert.AreEqual(3, rows.Count);
			CollectionAssert.AreEqual(new[] { "Id", "Group" }, rows[0]);
			CollectionAssert.AreEqual(new[] { "002", "B" }, rows[2]);
		}

		[Test]
		public void Parse_SkipsByteOrderMarkAndBlankLines()
		{
			List<string[]> rows = CsvUtil.Parse("﻿Id;Group\n\n001;A\n\n", ';');

			Assert.AreEqual(2, rows.Count);
			Assert.AreEqual("Id", rows[0][0]);
		}

		[Test]
		public void Parse_ReadsQuotedSeparatorsQuotesAndLineBreaks()
		{
			List<string[]> rows = CsvUtil.Parse("\"a;b\";\"say \"\"hi\"\"\";\"two\nlines\"\n", ';');

			CollectionAssert.AreEqual(new[] { "a;b", "say \"hi\"", "two\nlines" }, rows[0]);
		}

		[Test]
		public void Parse_KeepsEmptyCells()
		{
			List<string[]> rows = CsvUtil.Parse("001;;yes;\n", ';');

			CollectionAssert.AreEqual(new[] { "001", "", "yes", "" }, rows[0]);
		}

		[Test]
		public void FormatRow_QuotesOnlyWhatNeedsIt()
		{
			string line = CsvUtil.FormatRow(new[] { "001", "a;b", "say \"hi\"", "plain" }, ';');

			Assert.AreEqual("001;\"a;b\";\"say \"\"hi\"\"\";plain", line);
		}

		[Test]
		public void FormatRow_ThenParse_RoundTrips()
		{
			string[] cells = { "x", "comma, here", "semi;colon", "quote\"", "line\r\nbreak", "" };

			List<string[]> rows = CsvUtil.Parse(CsvUtil.FormatRow(cells, ';') + "\r\n", ';');

			CollectionAssert.AreEqual(cells, rows[0]);
		}

		[TestCase("Id;Form (nature, efficiency);Date\r\n1;A;x", ',', ';')]
		[TestCase("Id;Group,x", ',', ',')]
		[TestCase("Id,Group\r\n1,A", ';', ',')]
		[TestCase("Id\tGroup", ';', '\t')]
		[TestCase("Id", ';', ';')]
		[TestCase("\"a;b\",c", ';', ',')]
		public void DetectSeparator_ReadsTheHeaderLine(string text, char preferred, char expected)
		{
			Assert.AreEqual(expected, CsvUtil.DetectSeparator(text, preferred));
		}

		#endregion
	}
}
