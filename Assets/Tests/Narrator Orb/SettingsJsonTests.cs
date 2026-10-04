using System;
using System.Collections.Generic;
using DroneDDW.NarratorOrb.Settings;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DroneDDW.NarratorOrb.Tests
{
	// Paste and Copy JSON against the web preview's "Copy settings" output for its default settings
	public sealed class SettingsJsonTests
	{
		#region Fields

		// The web page's export of its defaults, line for line
		private static readonly string WebExport = string.Join("\n",
			"{",
			"  \"voice\": {",
			"    \"hopSeconds\": 0.01,",
			"    \"gateDb\": 30,",
			"    \"vowelRangeDb\": 24,",
			"    \"hissLowDb\": -16,",
			"    \"hissHighDb\": -4,",
			"    \"onsetOn\": 0.1,",
			"    \"onsetOff\": 0.04,",
			"    \"onsetMinGap\": 0.11,",
			"    \"leadSeconds\": 0.04",
			"  },",
			"  \"reaction\": {",
			"    \"swell\": 0.06,",
			"    \"glow\": 1.4,",
			"    \"ripples\": 1,",
			"    \"wisps\": 1,",
			"    \"sparkle\": 1.2,",
			"    \"smoothing\": 1",
			"  },",
			"  \"timeConstants\": {",
			"    \"levelAttack\": 0.03,",
			"    \"levelRelease\": 0.22,",
			"    \"hissAttack\": 0.015,",
			"    \"hissRelease\": 0.14,",
			"    \"wispAttack\": [",
			"      0.05,",
			"      0.07,",
			"      0.09,",
			"      0.06",
			"    ],",
			"    \"wispRelease\": [",
			"      0.3,",
			"      0.38,",
			"      0.46,",
			"      0.34",
			"    ],",
			"    \"pulseDecayPerSecond\": 7",
			"  },",
			"  \"ripple\": {",
			"    \"speed\": 2.4,",
			"    \"width\": 0.32,",
			"    \"life\": 1.1,",
			"    \"spreadFromFront\": 0.55",
			"  },",
			"  \"morph\": {",
			"    \"step\": 2,",
			"    \"smoothTime\": 0.45,",
			"    \"wispsLead\": 1,",
			"    \"steps\": [",
			"      -1,",
			"      -0.5,",
			"      0,",
			"      0.5,",
			"      1",
			"    ]",
			"  },",
			"  \"look\": {",
			"    \"shapeA\": \"bloom\",",
			"    \"colorA\": \"#45d483\",",
			"    \"shapeB\": \"cube\",",
			"    \"colorB\": \"#ff9f43\"",
			"  }",
			"}");

		private readonly List<NarratorOrbSettings> _created = new();

		#endregion

		#region Setup

		[TearDown]
		public void DestroyCreated()
		{
			foreach (NarratorOrbSettings settings in _created)
			{
				Object.DestroyImmediate(settings);
			}

			_created.Clear();
		}

		#endregion

		#region Private

		private NarratorOrbSettings Create()
		{
			NarratorOrbSettings settings = ScriptableObject.CreateInstance<NarratorOrbSettings>();
			_created.Add(settings);
			return settings;
		}

		#endregion

		#region Tests

		[Test]
		public void WebExportPastesAsTheDefaults()
		{
			NarratorOrbSettings settings = Create();
			settings.Voice.GateDecibels = 20f;
			settings.Reaction.Glow = 2.5f;
			settings.TimeConstants.WispReleaseSeconds = new[] { 1f, 1f, 1f, 1f };
			settings.Morph.Step = 4;
			settings.Morph.Steps = new[] { -0.8f, -0.4f, 0f, 0.4f, 0.8f };
			settings.Look.ShapeA = NarratorOrbShape.Star;
			settings.Look.ColorB = new Color32(1, 2, 3, 255);

			List<string> problems = NarratorOrbSettingsJson.Apply(settings, WebExport);

			Assert.That(problems, Is.Empty);
			Assert.That(NarratorOrbSettingsJson.ToJson(settings), Is.EqualTo(NarratorOrbSettingsJson.ToJson(Create())));
		}

		[Test]
		public void CopyWritesTheWebLayout()
		{
			string json = NarratorOrbSettingsJson.ToJson(Create());
			int unity = json.IndexOf(",\n  \"unity\"", StringComparison.Ordinal);

			Assert.That(unity, Is.GreaterThan(0));
			Assert.That(json.Substring(0, unity) + "\n}", Is.EqualTo(WebExport));
		}

		[Test]
		public void CopyThenPasteKeepsEveryValue()
		{
			NarratorOrbSettings source = Create();
			source.Voice.LeadSeconds = -0.02f;
			source.Reaction.Sparkle = 2.25f;
			source.TimeConstants.WispAttackSeconds = new[] { 0.01f, 0.02f, 0.03f, 0.04f };
			source.Ripple.SpreadFromFront = 0.9f;
			source.Morph.WispsLead = 0.35f;
			source.Look.ShapeB = NarratorOrbShape.Crystal;
			source.Look.ColorA = new Color32(0x12, 0x34, 0x56, 0xff);
			source.Rendering.IsWebBlendOn = false;
			source.Rendering.AlphaMode = NarratorOrbAlphaMode.SameAsColor;
			source.Rendering.MinParticlePixels = 2f;
			string json = NarratorOrbSettingsJson.ToJson(source);

			NarratorOrbSettings target = Create();
			List<string> problems = NarratorOrbSettingsJson.Apply(target, json);

			Assert.That(problems, Is.Empty);
			Assert.That(NarratorOrbSettingsJson.ToJson(target), Is.EqualTo(json));
			Assert.That(target.Look.ShapeB, Is.EqualTo(NarratorOrbShape.Crystal));
			Assert.That(target.Rendering.AlphaMode, Is.EqualTo(NarratorOrbAlphaMode.SameAsColor));
		}

		[Test]
		public void BadValuesAreReportedAndKept()
		{
			NarratorOrbSettings settings = Create();
			string json = "{\"look\": {\"shapeA\": \"blob\", \"colorA\": \"green-ish\"}, "
				+ "\"morph\": {\"steps\": [1, 2]}}";

			List<string> problems = NarratorOrbSettingsJson.Apply(settings, json);

			Assert.That(problems, Has.Count.EqualTo(3));
			Assert.That(settings.Look.ShapeA, Is.EqualTo(NarratorOrbShape.Bloom));
			Assert.That(settings.Look.ColorA, Is.EqualTo(new Color32(0x45, 0xd4, 0x83, 0xff)));
			Assert.That(settings.Morph.Steps, Has.Length.EqualTo(5));
		}

		[Test]
		public void TextThatIsNotJsonThrows()
		{
			Assert.Throws<ArgumentException>(() => NarratorOrbSettingsJson.Apply(Create(), "not settings"));
		}

		#endregion
	}
}
