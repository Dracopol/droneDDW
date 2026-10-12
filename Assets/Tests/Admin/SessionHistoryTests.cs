using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DroneDDW.Admin.Sessions;
using NUnit.Framework;

namespace DroneDDW.Admin.Tests
{
	internal sealed class SessionHistoryTests
	{
		#region Fields

		private const string Header = "Id;Group;First time participating;Form choice;Date;Time;Answer 1";

		private static readonly DateTime DeletedAt = new(2026, 10, 4, 15, 30, 5);

		private string _folder;

		#endregion

		#region Setup

		[SetUp]
		public void CreateFolder()
		{
			_folder = Path.Combine(Path.GetTempPath(), "DroneDDW Session History Tests " + Guid.NewGuid());
			Directory.CreateDirectory(_folder);
		}

		[TearDown]
		public void DeleteFolder()
		{
			Directory.Delete(_folder, true);
		}

		#endregion

		#region Tests

		[Test]
		public void Load_WithoutFile_IsEmpty()
		{
			Assert.AreEqual(0, CreateHistory().Load().Count);
		}

		[Test]
		public void Load_ReadsRowsByColumnName()
		{
			WriteSessions(Header, "1;A;yes;nature max;2026-03-12;09:14;x", "002;;no;neutral;2026-03-13;10:00;y");

			List<SessionRecord> records = CreateHistory().Load();

			Assert.AreEqual(2, records.Count);
			Assert.AreEqual("001", records[0].DisplayId);
			Assert.AreEqual("nature max", records[0].Get("form CHOICE"));
			Assert.AreEqual(SessionRecord.Missing, records[1].GetDisplay(SessionColumns.Group));
			Assert.AreEqual(SessionRecord.Missing, records[1].GetDisplay("Not a column"));
		}

		[Test]
		public void DisplayId_KeepsTextIds()
		{
			WriteSessions("Id;Group", "P-7;A");

			Assert.AreEqual("P-7", CreateHistory().Load()[0].DisplayId);
		}

		[Test]
		public void Delete_MovesTheRowWithItsDeleteTime()
		{
			WriteSessions(Header, "001;A;yes;nature max;2026-03-12;09:14;first",
				"002;B;no;neutral;2026-03-12;09:18;second");
			SessionHistory history = CreateHistory();

			Assert.IsTrue(history.Delete(history.Load()[0], DeletedAt));

			List<string[]> sessions = Read(history.SessionsPath);
			Assert.AreEqual(2, sessions.Count);
			Assert.AreEqual("Answer 1", sessions[0][6]);
			CollectionAssert.AreEqual(new[] { "002", "B", "no", "neutral", "2026-03-12", "09:18", "second" },
				sessions[1]);

			List<string[]> deleted = Read(history.DeletedPath);
			Assert.AreEqual(2, deleted.Count);
			Assert.AreEqual(SessionColumns.DeletedAt, deleted[0][7]);
			CollectionAssert.AreEqual(
				new[] { "001", "A", "yes", "nature max", "2026-03-12", "09:14", "first", "2026-10-04 15:30:05" },
				deleted[1]);
		}

		[Test]
		public void Delete_Twice_AppendsUnderOneHeader()
		{
			WriteSessions(Header, "001;A;yes;nature max;2026-03-12;09:14;a", "002;B;no;neutral;2026-03-12;09:18;b");
			SessionHistory history = CreateHistory();

			history.Delete(history.Load()[0], DeletedAt);
			history.Delete(history.Load()[0], DeletedAt);

			Assert.AreEqual(1, Read(history.SessionsPath).Count);
			List<string[]> deleted = Read(history.DeletedPath);
			Assert.AreEqual(3, deleted.Count);
			Assert.AreEqual("002", deleted[2][0]);
		}

		[Test]
		public void Delete_RowNoLongerThere_ReturnsFalse()
		{
			WriteSessions(Header, "001;A;yes;nature max;2026-03-12;09:14;a");
			SessionHistory history = CreateHistory();
			SessionRecord record = history.Load()[0];
			history.Delete(record, DeletedAt);

			Assert.IsFalse(history.Delete(record, DeletedAt));
		}

		[Test]
		public void Delete_KeepsACommaFileWithCommas()
		{
			WriteSessions("Id,Group", "001,A", "002,B");
			SessionHistory history = CreateHistory();

			history.Delete(history.Load()[0], DeletedAt);

			string text = File.ReadAllText(history.SessionsPath);
			StringAssert.StartsWith("Id,Group", text.TrimStart('﻿'));
		}

		[Test]
		public void Delete_WritesUtf8WithByteOrderMark()
		{
			WriteSessions(Header, "001;A;yes;nature max;2026-03-12;09:14;a", "002;B;no;neutral;2026-03-12;09:18;b");
			SessionHistory history = CreateHistory();

			history.Delete(history.Load()[0], DeletedAt);

			byte[] bytes = File.ReadAllBytes(history.SessionsPath);
			Assert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, new[] { bytes[0], bytes[1], bytes[2] });
			Assert.IsFalse(File.Exists(history.SessionsPath + ".tmp"));
		}

		[Test]
		public void Delete_PadsAShortRowSoTheTimeLandsUnderItsHeader()
		{
			WriteSessions("Id;Group;Date", "001;A");
			SessionHistory history = CreateHistory();

			history.Delete(history.Load()[0], DeletedAt);

			List<string[]> deleted = Read(history.DeletedPath);
			Assert.AreEqual(4, deleted[1].Length);
			Assert.AreEqual("2026-10-04 15:30:05", deleted[1][3]);
		}

		[Test]
		public void Append_WithoutFile_WritesHeaderAndRow()
		{
			SessionHistory history = CreateHistory();

			List<string> missing = history.Append(TestCells());

			Assert.AreEqual(0, missing.Count);
			byte[] bytes = File.ReadAllBytes(history.SessionsPath);
			Assert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, new[] { bytes[0], bytes[1], bytes[2] });
			List<string[]> rows = Read(history.SessionsPath);
			CollectionAssert.AreEqual(new[] { "Id", "Group", "Form choice", "Date", "Time" }, rows[0]);
			CollectionAssert.AreEqual(new[] { "0", "A", "neutral", "2026-10-05", "14:32" }, rows[1]);
		}

		[Test]
		public void Append_ToExistingFile_FollowsItsColumns()
		{
			WriteSessions("Time;Id;Answer 1;Group", "09:14;001;x;B");
			SessionHistory history = CreateHistory();

			List<string> missing = history.Append(TestCells());

			List<string[]> rows = Read(history.SessionsPath);
			Assert.AreEqual(3, rows.Count);
			CollectionAssert.AreEqual(new[] { "14:32", "0", "", "A" }, rows[2]);
			CollectionAssert.AreEquivalent(new[] { "Form choice", "Date" }, missing);
		}

		[Test]
		public void Append_KeepsACommaFileWithCommas()
		{
			WriteSessions("Id,Group", "001,B");
			SessionHistory history = CreateHistory();

			history.Append(TestCells());

			StringAssert.EndsWith("001,B\r\n0,A\r\n", File.ReadAllText(history.SessionsPath));
		}

		[Test]
		public void Append_AfterALastLineWithoutBreak_StartsANewLine()
		{
			File.WriteAllText(Path.Combine(_folder, "Sessions.csv"), "Id;Group\r\n001;B", new UTF8Encoding(true));
			SessionHistory history = CreateHistory();

			history.Append(TestCells());

			List<string[]> rows = Read(history.SessionsPath);
			Assert.AreEqual(3, rows.Count);
			CollectionAssert.AreEqual(new[] { "0", "A" }, rows[2]);
		}

		[Test]
		public void Append_ThenDelete_MovesTheRow()
		{
			SessionHistory history = CreateHistory();
			history.Append(TestCells());
			history.Append(TestCells());

			Assert.IsTrue(history.Delete(history.Load()[1], DeletedAt));

			Assert.AreEqual(2, Read(history.SessionsPath).Count);
			List<string[]> deleted = Read(history.DeletedPath);
			Assert.AreEqual(2, deleted.Count);
			Assert.AreEqual("0", deleted[1][0]);
		}

		#endregion

		#region Private

		// What the admin menu's test save button writes, at a fixed time
		private static (string Column, string Value)[] TestCells()
		{
			return new[]
			{
				(SessionColumns.Id, "0"),
				(SessionColumns.Group, "A"),
				(SessionColumns.FormChoice, "neutral"),
				(SessionColumns.Date, "2026-10-05"),
				(SessionColumns.Time, "14:32"),
			};
		}

		private SessionHistory CreateHistory()
		{
			return new SessionHistory(_folder, "Sessions.csv", "Deleted Sessions.csv", ';');
		}

		private void WriteSessions(params string[] lines)
		{
			File.WriteAllText(Path.Combine(_folder, "Sessions.csv"), string.Join("\r\n", lines) + "\r\n",
				new UTF8Encoding(true));
		}

		private static List<string[]> Read(string path)
		{
			string text = File.ReadAllText(path);
			return CsvUtil.Parse(text, CsvUtil.DetectSeparator(text, ';'));
		}

		#endregion
	}
}
