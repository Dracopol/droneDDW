using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.Experience.Tests
{
	internal sealed class ExperienceSettingsTests
	{
		#region Fields

		private ExperienceSettings _settings;
		private AudioClip _clip;

		#endregion

		#region Setup

		[SetUp]
		public void CreateSettings()
		{
			_settings = ScriptableObject.CreateInstance<ExperienceSettings>();
			_clip = AudioClip.Create("Placeholder", 100, 1, 8000, false);
		}

		[TearDown]
		public void DestroySettings()
		{
			Object.DestroyImmediate(_settings);
			Object.DestroyImmediate(_clip);
		}

		#endregion

		#region Tests

		[TestCase(0, "Nature")]
		[TestCase(1, "Balanced")]
		[TestCase(2, "Efficiency")]
		public void GetFormChoiceName_Version_IsItsName(int version, string expected)
		{
			Assert.That(_settings.GetFormChoiceName(version), Is.EqualTo(expected));
		}

		[Test]
		public void GetFormChoiceName_UnnamedVersion_IsTheNumber()
		{
			Assert.That(_settings.GetFormChoiceName(5), Is.EqualTo("5"));
		}

		[Test]
		public void GetHandoverText_Participant_NamesThem()
		{
			Assert.That(_settings.GetHandoverText("104"), Does.Contain("104"));
		}

		[Test]
		public void UsePlaceholder_SetsEveryLine()
		{
			_settings.UsePlaceholder(_clip, 3);

			Assert.That(_settings.Welcome, Is.SameAs(_clip));
			Assert.That(_settings.Goodbye, Is.SameAs(_clip));
			Assert.That(_settings.HandCheck.Prompt, Is.SameAs(_clip));
			Assert.That(_settings.NeighborhoodLockIn.Stuck, Is.SameAs(_clip));
			Assert.That(_settings.PracticeLockIn.Success, Is.SameAs(_clip));
			Assert.That(_settings.GetCharacterLine(2), Is.SameAs(_clip));
			Assert.That(_settings.GetCharacterLine(3), Is.Null);
		}

		#endregion
	}
}
