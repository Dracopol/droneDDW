using DroneDDW.NarratorOrb.Settings;
using DroneDDW.Voice;
using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Tests
{
	// The settings asset's voice defaults are the web preview's VOICE_DEFAULTS, exactly
	public sealed class NarratorOrbVoiceSettingsTests
	{
		#region Tests

		[Test]
		public void SettingsAssetDefaultsAreTheReferenceValues()
		{
			NarratorOrbSettings settings = ScriptableObject.CreateInstance<NarratorOrbSettings>();
			try
			{
				Assert.That(settings.Voice.ToAnalysisSettings(), Is.EqualTo(VoiceAnalysisSettings.Defaults));
			}
			finally
			{
				Object.DestroyImmediate(settings);
			}
		}

		#endregion
	}
}
