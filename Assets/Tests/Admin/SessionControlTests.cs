using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DroneDDW.Admin.Sessions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DroneDDW.Admin.Tests
{
	internal sealed class SessionControlTests
	{
		#region Fields

		private string _folder;
		private AdminMenuSettings _settings;
		private GameObject _host;
		private SessionControl _session;

		#endregion

		#region Setup

		[SetUp]
		public void CreateSession()
		{
			_folder = Path.Combine(Path.GetTempPath(), "DroneDDW Session Control Tests " + Guid.NewGuid());
			Directory.CreateDirectory(_folder);
			_settings = ScriptableObject.CreateInstance<AdminMenuSettings>();
			_host = new GameObject("Session Control");
			_session = _host.AddComponent<SessionControl>();
			_session.ConfigureSessions(_settings, true);
			_session.UseSessionsFolder(_folder);
		}

		[TearDown]
		public void DestroySession()
		{
			Object.DestroyImmediate(_host);
			Object.DestroyImmediate(_settings);
			Directory.Delete(_folder, true);
		}

		#endregion

		#region Tests

		[Test]
		public void IsCheckingIn_BeforeStart_WaitsForAParticipant()
		{
			Assert.That(_session.IsCheckingIn, Is.True);
			Assert.That(_session.HasParticipant, Is.False);
		}

		[Test]
		public void StartSession_Number_ChecksTheParticipantIn()
		{
			bool isStarted = false;
			_session.OnSessionStart += () => isStarted = true;

			_session.StartSession(" 104 ");

			Assert.That(isStarted, Is.True);
			Assert.That(_session.ParticipantId, Is.EqualTo("104"));
			Assert.That(_session.Group, Is.EqualTo("A"));
			Assert.That(_session.IsCheckingIn, Is.False);
		}

		[Test]
		public void RecordChoice_NewFile_WritesTheHeaderAndTheRow()
		{
			_session.StartSession("104");
			DateTime before = DateTime.Now;

			bool isRecorded = _session.RecordChoice("Balanced");

			List<string[]> rows = CsvUtil.Parse(File.ReadAllText(Path.Combine(_folder, "Sessions.csv")), ';');
			Assert.That(isRecorded, Is.True);
			Assert.That(rows, Has.Count.EqualTo(2));
			Assert.That(rows[0], Is.EqualTo(new[] { "Id", "Group", "Form choice", "Date", "Time" }));
			Assert.That(rows[1][0], Is.EqualTo("104"));
			Assert.That(rows[1][1], Is.EqualTo("A"));
			Assert.That(rows[1][2], Is.EqualTo("Balanced"));
			Assert.That(rows[1][3], Is.EqualTo(before.ToString("yyyy-MM-dd")).Or.EqualTo(DateTime.Now.ToString("yyyy-MM-dd")));
			Assert.That(rows[1][4], Does.Match(@"^\d\d:\d\d$"));
		}

		[Test]
		public void RecordChoice_TwoParticipants_AppendsRows()
		{
			_session.StartSession("104");
			_session.RecordChoice("Nature");
			_session.StartSession("105");

			_session.RecordChoice("Efficiency");

			List<string[]> rows = CsvUtil.Parse(File.ReadAllText(Path.Combine(_folder, "Sessions.csv")), ';');
			Assert.That(rows, Has.Count.EqualTo(3));
			Assert.That(rows[2][0], Is.EqualTo("105"));
			Assert.That(rows[2][2], Is.EqualTo("Efficiency"));
		}

		[Test]
		public void RecordChoice_NoParticipant_WritesNothing()
		{
			LogAssert.Expect(LogType.Error, new Regex("no participant is checked in"));

			bool isRecorded = _session.RecordChoice("Balanced");

			Assert.That(isRecorded, Is.False);
			Assert.That(File.Exists(Path.Combine(_folder, "Sessions.csv")), Is.False);
		}

		#endregion
	}
}
