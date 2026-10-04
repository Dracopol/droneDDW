using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Settings
{
	// The settings as JSON in the web preview's format: its "Copy settings" output pastes straight in. The web page's
	// names (gateDb, onsetOn, levelAttack, ...) map onto this project's names; Unity-only values go under "unity".
	// Numbers are written rounded to three decimals, as the web page writes them.
	internal static class NarratorOrbSettingsJson
	{
		#region Entities

		// The JSON's shape: public fields named like the web page's keys, because JsonUtility maps fields by name
		[Serializable]
		private sealed class SettingsData
		{
			public VoiceData voice = new();
			public ReactionData reaction = new();
			public TimeConstantsData timeConstants = new();
			public RippleData ripple = new();
			public MorphData morph = new();
			public LookData look = new();
			public UnityData unity = new();
		}

		[Serializable]
		private sealed class VoiceData
		{
			public float hopSeconds;
			public float gateDb;
			public float vowelRangeDb;
			public float hissLowDb;
			public float hissHighDb;
			public float onsetOn;
			public float onsetOff;
			public float onsetMinGap;
			public float leadSeconds;
		}

		[Serializable]
		private sealed class ReactionData
		{
			public float swell;
			public float glow;
			public float ripples;
			public float wisps;
			public float sparkle;
			public float smoothing;
		}

		[Serializable]
		private sealed class TimeConstantsData
		{
			public float levelAttack;
			public float levelRelease;
			public float hissAttack;
			public float hissRelease;
			public float[] wispAttack;
			public float[] wispRelease;
			public float pulseDecayPerSecond;
		}

		[Serializable]
		private sealed class RippleData
		{
			public float speed;
			public float width;
			public float life;
			public float spreadFromFront;
		}

		[Serializable]
		private sealed class MorphData
		{
			public int step;
			public float smoothTime;
			public float wispsLead;
			public float[] steps;
		}

		[Serializable]
		private sealed class LookData
		{
			public string shapeA;
			public string colorA;
			public string shapeB;
			public string colorB;
		}

		[Serializable]
		private sealed class UnityData
		{
			public bool webBlend;
			public float intensity;
			public float particleSize;
			public float minParticlePixels;
			public string alphaMode;
			public float alphaGain;
		}

		#endregion

		#region Fields

		private const string Indent = "  ";

		#endregion

		#region Internal

		internal static string ToJson(NarratorOrbSettings settings)
		{
			NarratorOrbVoiceSettings voice = settings.Voice;
			NarratorOrbReactionSettings reaction = settings.Reaction;
			NarratorOrbTimeConstants timeConstants = settings.TimeConstants;
			NarratorOrbRippleSettings ripple = settings.Ripple;
			NarratorOrbMorphSettings morph = settings.Morph;
			NarratorOrbLookSettings look = settings.Look;
			NarratorOrbRenderSettings rendering = settings.Rendering;

			StringBuilder json = new();
			json.Append("{\n");
			AppendGroup(json, "voice", false,
				("hopSeconds", Number(voice.HopSeconds)),
				("gateDb", Number(voice.GateDecibels)),
				("vowelRangeDb", Number(voice.VowelRangeDecibels)),
				("hissLowDb", Number(voice.HissLowDecibels)),
				("hissHighDb", Number(voice.HissHighDecibels)),
				("onsetOn", Number(voice.OnsetOnThreshold)),
				("onsetOff", Number(voice.OnsetOffThreshold)),
				("onsetMinGap", Number(voice.OnsetMinGapSeconds)),
				("leadSeconds", Number(voice.LeadSeconds)));
			AppendGroup(json, "reaction", false,
				("swell", Number(reaction.Swell)),
				("glow", Number(reaction.Glow)),
				("ripples", Number(reaction.Ripples)),
				("wisps", Number(reaction.Wisps)),
				("sparkle", Number(reaction.Sparkle)),
				("smoothing", Number(reaction.Smoothing)));
			AppendGroup(json, "timeConstants", false,
				("levelAttack", Number(timeConstants.LevelAttackSeconds)),
				("levelRelease", Number(timeConstants.LevelReleaseSeconds)),
				("hissAttack", Number(timeConstants.HissAttackSeconds)),
				("hissRelease", Number(timeConstants.HissReleaseSeconds)),
				("wispAttack", NumberList(timeConstants.WispAttackSeconds)),
				("wispRelease", NumberList(timeConstants.WispReleaseSeconds)),
				("pulseDecayPerSecond", Number(timeConstants.PulseDecayPerSecond)));
			AppendGroup(json, "ripple", false,
				("speed", Number(ripple.SpeedRadiansPerSecond)),
				("width", Number(ripple.WidthRadians)),
				("life", Number(ripple.LifeSeconds)),
				("spreadFromFront", Number(ripple.SpreadFromFront)));
			AppendGroup(json, "morph", false,
				("step", morph.Step.ToString(CultureInfo.InvariantCulture)),
				("smoothTime", Number(morph.SmoothTimeSeconds)),
				("wispsLead", Number(morph.WispsLead)),
				("steps", NumberList(morph.Steps)));
			AppendGroup(json, "look", false,
				("shapeA", Text(ShapeName(look.ShapeA))),
				("colorA", Text(ColorHex(look.ColorA))),
				("shapeB", Text(ShapeName(look.ShapeB))),
				("colorB", Text(ColorHex(look.ColorB))));
			AppendGroup(json, "unity", true,
				("webBlend", rendering.IsWebBlendOn ? "true" : "false"),
				("intensity", Number(rendering.Intensity)),
				("particleSize", Number(rendering.ParticleSizeMultiplier)),
				("minParticlePixels", Number(rendering.MinParticlePixels)),
				("alphaMode", Text(AlphaModeName(rendering.AlphaMode))),
				("alphaGain", Number(rendering.AlphaGain)));
			json.Append('}');
			return json.ToString();
		}

		// Overwrites the settings with every value the JSON holds; values it leaves out stay as they are. Returns the
		// values it couldn't use. Throws ArgumentException when the text isn't JSON.
		internal static List<string> Apply(NarratorOrbSettings settings, string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				throw new ArgumentException("There is no text to read.");
			}

			SettingsData data = Capture(settings);
			JsonUtility.FromJsonOverwrite(json, data);
			List<string> problems = new();
			Restore(settings, data, problems);
			settings.Validate();
			return problems;
		}

		#endregion

		#region Private

		private static SettingsData Capture(NarratorOrbSettings settings)
		{
			NarratorOrbVoiceSettings voice = settings.Voice;
			NarratorOrbReactionSettings reaction = settings.Reaction;
			NarratorOrbTimeConstants timeConstants = settings.TimeConstants;
			NarratorOrbRippleSettings ripple = settings.Ripple;
			NarratorOrbMorphSettings morph = settings.Morph;
			NarratorOrbLookSettings look = settings.Look;
			NarratorOrbRenderSettings rendering = settings.Rendering;

			SettingsData data = new();
			data.voice.hopSeconds = voice.HopSeconds;
			data.voice.gateDb = voice.GateDecibels;
			data.voice.vowelRangeDb = voice.VowelRangeDecibels;
			data.voice.hissLowDb = voice.HissLowDecibels;
			data.voice.hissHighDb = voice.HissHighDecibels;
			data.voice.onsetOn = voice.OnsetOnThreshold;
			data.voice.onsetOff = voice.OnsetOffThreshold;
			data.voice.onsetMinGap = voice.OnsetMinGapSeconds;
			data.voice.leadSeconds = voice.LeadSeconds;

			data.reaction.swell = reaction.Swell;
			data.reaction.glow = reaction.Glow;
			data.reaction.ripples = reaction.Ripples;
			data.reaction.wisps = reaction.Wisps;
			data.reaction.sparkle = reaction.Sparkle;
			data.reaction.smoothing = reaction.Smoothing;

			data.timeConstants.levelAttack = timeConstants.LevelAttackSeconds;
			data.timeConstants.levelRelease = timeConstants.LevelReleaseSeconds;
			data.timeConstants.hissAttack = timeConstants.HissAttackSeconds;
			data.timeConstants.hissRelease = timeConstants.HissReleaseSeconds;
			data.timeConstants.wispAttack = (float[])timeConstants.WispAttackSeconds.Clone();
			data.timeConstants.wispRelease = (float[])timeConstants.WispReleaseSeconds.Clone();
			data.timeConstants.pulseDecayPerSecond = timeConstants.PulseDecayPerSecond;

			data.ripple.speed = ripple.SpeedRadiansPerSecond;
			data.ripple.width = ripple.WidthRadians;
			data.ripple.life = ripple.LifeSeconds;
			data.ripple.spreadFromFront = ripple.SpreadFromFront;

			data.morph.step = morph.Step;
			data.morph.smoothTime = morph.SmoothTimeSeconds;
			data.morph.wispsLead = morph.WispsLead;
			data.morph.steps = (float[])morph.Steps.Clone();

			data.look.shapeA = ShapeName(look.ShapeA);
			data.look.colorA = ColorHex(look.ColorA);
			data.look.shapeB = ShapeName(look.ShapeB);
			data.look.colorB = ColorHex(look.ColorB);

			data.unity.webBlend = rendering.IsWebBlendOn;
			data.unity.intensity = rendering.Intensity;
			data.unity.particleSize = rendering.ParticleSizeMultiplier;
			data.unity.minParticlePixels = rendering.MinParticlePixels;
			data.unity.alphaMode = AlphaModeName(rendering.AlphaMode);
			data.unity.alphaGain = rendering.AlphaGain;
			return data;
		}

		private static void Restore(NarratorOrbSettings settings, SettingsData data, List<string> problems)
		{
			NarratorOrbVoiceSettings voice = settings.Voice;
			voice.HopSeconds = data.voice.hopSeconds;
			voice.GateDecibels = data.voice.gateDb;
			voice.VowelRangeDecibels = data.voice.vowelRangeDb;
			voice.HissLowDecibels = data.voice.hissLowDb;
			voice.HissHighDecibels = data.voice.hissHighDb;
			voice.OnsetOnThreshold = data.voice.onsetOn;
			voice.OnsetOffThreshold = data.voice.onsetOff;
			voice.OnsetMinGapSeconds = data.voice.onsetMinGap;
			voice.LeadSeconds = data.voice.leadSeconds;

			NarratorOrbReactionSettings reaction = settings.Reaction;
			reaction.Swell = data.reaction.swell;
			reaction.Glow = data.reaction.glow;
			reaction.Ripples = data.reaction.ripples;
			reaction.Wisps = data.reaction.wisps;
			reaction.Sparkle = data.reaction.sparkle;
			reaction.Smoothing = data.reaction.smoothing;

			NarratorOrbTimeConstants timeConstants = settings.TimeConstants;
			timeConstants.LevelAttackSeconds = data.timeConstants.levelAttack;
			timeConstants.LevelReleaseSeconds = data.timeConstants.levelRelease;
			timeConstants.HissAttackSeconds = data.timeConstants.hissAttack;
			timeConstants.HissReleaseSeconds = data.timeConstants.hissRelease;
			timeConstants.WispAttackSeconds = ListOrCurrent("timeConstants.wispAttack", data.timeConstants.wispAttack,
				timeConstants.WispAttackSeconds, problems);
			timeConstants.WispReleaseSeconds = ListOrCurrent("timeConstants.wispRelease",
				data.timeConstants.wispRelease, timeConstants.WispReleaseSeconds, problems);
			timeConstants.PulseDecayPerSecond = data.timeConstants.pulseDecayPerSecond;

			NarratorOrbRippleSettings ripple = settings.Ripple;
			ripple.SpeedRadiansPerSecond = data.ripple.speed;
			ripple.WidthRadians = data.ripple.width;
			ripple.LifeSeconds = data.ripple.life;
			ripple.SpreadFromFront = data.ripple.spreadFromFront;

			NarratorOrbMorphSettings morph = settings.Morph;
			if (data.morph.step >= 0 && data.morph.step < NarratorOrbMorphSettings.StepCount)
			{
				morph.Step = data.morph.step;
			}
			else
			{
				problems.Add($"morph.step {data.morph.step} isn't a step (0 to 4), so it stays {morph.Step}.");
			}

			morph.SmoothTimeSeconds = data.morph.smoothTime;
			morph.WispsLead = data.morph.wispsLead;
			morph.Steps = ListOrCurrent("morph.steps", data.morph.steps, morph.Steps, problems);

			NarratorOrbLookSettings look = settings.Look;
			look.ShapeA = ShapeOrCurrent("look.shapeA", data.look.shapeA, look.ShapeA, problems);
			look.ColorA = ColorOrCurrent("look.colorA", data.look.colorA, look.ColorA, problems);
			look.ShapeB = ShapeOrCurrent("look.shapeB", data.look.shapeB, look.ShapeB, problems);
			look.ColorB = ColorOrCurrent("look.colorB", data.look.colorB, look.ColorB, problems);

			NarratorOrbRenderSettings rendering = settings.Rendering;
			rendering.IsWebBlendOn = data.unity.webBlend;
			rendering.Intensity = data.unity.intensity;
			rendering.ParticleSizeMultiplier = data.unity.particleSize;
			rendering.MinParticlePixels = data.unity.minParticlePixels;
			rendering.AlphaMode = AlphaModeOrCurrent(data.unity.alphaMode, rendering.AlphaMode, problems);
			rendering.AlphaGain = data.unity.alphaGain;
		}

		private static float[] ListOrCurrent(string key, float[] values, float[] current, List<string> problems)
		{
			if (values != null && values.Length == current.Length)
			{
				return values;
			}

			int count = values == null ? 0 : values.Length;
			problems.Add($"{key} needs {current.Length} numbers but has {count}, so it stays as it was.");
			return current;
		}

		private static NarratorOrbShape ShapeOrCurrent(string key, string name, NarratorOrbShape current,
			List<string> problems)
		{
			if (Enum.TryParse(name, true, out NarratorOrbShape shape)
				&& Enum.IsDefined(typeof(NarratorOrbShape), shape))
			{
				return shape;
			}

			problems.Add($"{key} \"{name}\" isn't a shape (sphere, bloom, pebble, cube, crystal or star), "
				+ $"so it stays {ShapeName(current)}.");
			return current;
		}

		private static Color32 ColorOrCurrent(string key, string hex, Color32 current, List<string> problems)
		{
			if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color color))
			{
				Color32 parsed = color;
				parsed.a = byte.MaxValue;
				return parsed;
			}

			problems.Add($"{key} \"{hex}\" isn't a colour like #45d483, so it stays {ColorHex(current)}.");
			return current;
		}

		private static NarratorOrbAlphaMode AlphaModeOrCurrent(string name, NarratorOrbAlphaMode current,
			List<string> problems)
		{
			if (Enum.TryParse(name, true, out NarratorOrbAlphaMode mode)
				&& Enum.IsDefined(typeof(NarratorOrbAlphaMode), mode))
			{
				return mode;
			}

			problems.Add($"unity.alphaMode \"{name}\" isn't brightness, untouched or sameAsColor, "
				+ $"so it stays {AlphaModeName(current)}.");
			return current;
		}

		private static void AppendGroup(StringBuilder json, string name, bool isLast,
			params (string Key, string Value)[] entries)
		{
			json.Append(Indent).Append('"').Append(name).Append("\": {\n");
			for (int i = 0; i < entries.Length; i++)
			{
				json.Append(Indent).Append(Indent).Append('"').Append(entries[i].Key).Append("\": ")
					.Append(entries[i].Value).Append(i < entries.Length - 1 ? ",\n" : "\n");
			}

			json.Append(Indent).Append(isLast ? "}\n" : "},\n");
		}

		// Rounded to three decimals like the web page's export, written the way JavaScript writes numbers
		private static string Number(float value)
		{
			if (float.IsNaN(value) || float.IsInfinity(value))
			{
				return "null";
			}

			double rounded = Math.Round(NarratorOrbSettingsUtil.ToExactDouble(value), 3, MidpointRounding.AwayFromZero);
			return rounded.ToString("R", CultureInfo.InvariantCulture);
		}

		private static string NumberList(float[] values)
		{
			StringBuilder list = new();
			list.Append("[\n");
			for (int i = 0; i < values.Length; i++)
			{
				list.Append(Indent).Append(Indent).Append(Indent).Append(Number(values[i]))
					.Append(i < values.Length - 1 ? ",\n" : "\n");
			}

			list.Append(Indent).Append(Indent).Append(']');
			return list.ToString();
		}

		private static string Text(string value)
		{
			return $"\"{value}\"";
		}

		private static string ShapeName(NarratorOrbShape shape)
		{
			return shape.ToString().ToLowerInvariant();
		}

		private static string ColorHex(Color32 color)
		{
			return "#" + ColorUtility.ToHtmlStringRGB(color).ToLowerInvariant();
		}

		// brightness, untouched, sameAsColor
		private static string AlphaModeName(NarratorOrbAlphaMode mode)
		{
			string name = mode.ToString();
			return char.ToLowerInvariant(name[0]) + name.Substring(1);
		}

		#endregion
	}
}
