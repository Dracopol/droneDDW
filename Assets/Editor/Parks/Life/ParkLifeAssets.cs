using System;
using System.Collections.Generic;
using System.IO;
using DroneDDW.Parks.Editor.Building;
using DroneDDW.Parks.Life;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneDDW.Parks.Editor.Life
{
	/// <summary>
	/// The assets the park's life needs: the settings asset (created once, then only its empty clip slots are filled,
	/// so tuning is never overwritten), the lights material for machine props with its emission mask, and the droplet
	/// material for the sprinklers. Textures are generated, so nothing has to be painted by hand.
	/// </summary>
	internal static class ParkLifeAssets
	{
		#region Fields

		internal const string SoundsFolder = ParkAssetUtility.SoundEffectsFolder + "/Park Life";

		private const string SettingsPath = ParkAssetUtility.LayoutsFolder + "/Park Life Settings.asset";
		private const string PaletteName = "Park_Palette.png";
		private const string LightsMaskName = "Park_Palette_Lights.png";
		private const string DropletTextureName = "Park_Droplet.png";
		private const string PropsMaterialName = "Park_Mat_Props.mat";
		private const string LightsMaterialName = "Park_Mat_Props_Lights.mat";
		private const string DropletMaterialName = "Park_Mat_Droplets.mat";
		private const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";
		private const string EmissionKeyword = "_EMISSION";
		private const int SwatchPixels = 16;
		private const int DropletPixels = 32;

		// The palette's lamp, LED and screen colours: the only swatches that glow on machine props
		private static readonly string[] LightSwatches = { "5fe0ff", "79d6f2", "e6f8ff" };

		#endregion

		#region Internal

		internal static ParkLifeSettings LoadOrCreateSettings()
		{
			ParkLifeSettings settings = AssetDatabase.LoadAssetAtPath<ParkLifeSettings>(SettingsPath);
			if (settings == null)
			{
				ParkAssetUtility.EnsureFolder(ParkAssetUtility.LayoutsFolder);
				settings = ScriptableObject.CreateInstance<ParkLifeSettings>();
				AssetDatabase.CreateAsset(settings, SettingsPath);
			}

			FillEmptyClips(settings);
			EditorUtility.SetDirty(settings);
			AssetDatabase.SaveAssets();
			return settings;
		}

		/// <summary>
		/// Park_Mat_Props with an emission map that keeps only the light swatches of the palette, so on machine props
		/// just their lamps, LEDs and screens glow. Black emission until the machine lights set each renderer's strength.
		/// </summary>
		internal static Material CreateLightsMaterial()
		{
			Texture2D mask = CreateLightsMask();
			Material props = AssetDatabase.LoadAssetAtPath<Material>($"{ParkAssetUtility.MaterialsFolder}/{PropsMaterialName}");
			if (props == null || mask == null)
			{
				Debug.LogWarning("[Parks] Missing the props material or the palette, so machine lights can't glow.");
				return props;
			}

			Material lights = LoadOrCopy(props, $"{ParkAssetUtility.MaterialsFolder}/{LightsMaterialName}");
			// Flagged realtime emissive: URP 17 turns emission off on materials without an emissive GI flag
			lights.SetTexture("_EmissionMap", mask);
			lights.EnableKeyword(EmissionKeyword);
			lights.SetColor("_EmissionColor", Color.black);
			lights.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
			EditorUtility.SetDirty(lights);
			AssetDatabase.SaveAssets();
			return lights;
		}

		/// <summary>A soft white dot on URP's unlit particle shader, alpha blended, tinted by each droplet.</summary>
		internal static Material CreateDropletMaterial()
		{
			Texture2D droplet = CreateDropletTexture();
			string path = $"{ParkAssetUtility.MaterialsFolder}/{DropletMaterialName}";
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			Shader shader = Shader.Find(ParticleShader);
			if (material == null)
			{
				material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
				AssetDatabase.CreateAsset(material, path);
			}
			else if (shader != null && material.shader != shader)
			{
				material.shader = shader;
			}

			material.SetTexture("_BaseMap", droplet);
			material.SetColor("_BaseColor", Color.white);
			material.SetFloat("_Surface", 1f);
			material.SetFloat("_Blend", 0f);
			material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
			material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
			material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
			material.SetFloat("_ZWrite", 0f);
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Transparent");
			material.renderQueue = (int)RenderQueue.Transparent;
			EditorUtility.SetDirty(material);
			AssetDatabase.SaveAssets();
			return material;
		}

		#endregion

		#region Private

		private static void FillEmptyClips(ParkLifeSettings settings)
		{
			settings.Drones.Sound.SetClipsIfEmpty(Clips("Drone"));
			settings.Robots.Motor.SetClipsIfEmpty(Clips("Robot Motor"));
			settings.Robots.Beeps.SetClipsIfEmpty(Clips("Robot Beep 1", "Robot Beep 2"));
			settings.Robots.Mower.SetClipsIfEmpty(Clips("Mower"));
			settings.Machines.ChargerHum.SetClipsIfEmpty(Clips("Charger Hum"));
			settings.Machines.DockHum.SetClipsIfEmpty(Clips("Charger Hum"));
			settings.Machines.HubVents.SetClipsIfEmpty(Clips("Hub Vents"));
			settings.Machines.SensorPing.SetClipsIfEmpty(Clips("Sensor Ping"));
			settings.Machines.TotemChime.SetClipsIfEmpty(Clips("Totem Chime"));
			settings.Machines.BinCompactor.SetClipsIfEmpty(Clips("Bin Compactor"));
			settings.Machines.BeaconTick.SetClipsIfEmpty(Clips("Beacon Tick"));
			settings.Sprinklers.Sound.SetClipsIfEmpty(Clips("Sprinkler"));
			settings.Birds.Songs.SetClipsIfEmpty(Clips("Bird Robin", "Bird Blackbird", "Bird Tit", "Bird Finch"));
			settings.Birds.WingFlutter.SetClipsIfEmpty(Clips("Wing Flutter"));
			settings.Birds.BuzzardCry.SetClipsIfEmpty(Clips("Buzzard Cry"));
			settings.Birds.Woodpecker.SetClipsIfEmpty(Clips("Woodpecker Drum"));
			settings.Birds.PigeonCoo.SetClipsIfEmpty(Clips("Pigeon Coo"));
			settings.Birds.CrowCaw.SetClipsIfEmpty(Clips("Crow Caw"));
			settings.Pond.Quacks.SetClipsIfEmpty(Clips("Duck Quack 1", "Duck Quack 2"));
			settings.Pond.Splash.SetClipsIfEmpty(Clips("Duck Splash"));
			settings.Pond.FrogCroaks.SetClipsIfEmpty(Clips("Frog Croak 1", "Frog Croak 2"));
			settings.Pond.FishPlop.SetClipsIfEmpty(Clips("Fish Plop"));
			settings.Pond.Lapping.SetClipsIfEmpty(Clips("Water Lapping"));
			settings.Pond.Fountain.SetClipsIfEmpty(Clips("Fountain Splash"));
			settings.Pond.Trickle.SetClipsIfEmpty(Clips("Pool Trickle"));
			settings.Insects.BeeBuzz.SetClipsIfEmpty(Clips("Bee Buzz"));
			settings.Insects.DragonflyWings.SetClipsIfEmpty(Clips("Dragonfly Wings"));
			settings.Insects.CricketSong.SetClipsIfEmpty(Clips("Crickets"));
			settings.Wind.Rustle.SetClipsIfEmpty(Clips("Leaf Rustle"));
		}

		private static AudioClip[] Clips(params string[] names)
		{
			List<AudioClip> clips = new List<AudioClip>();
			foreach (string name in names)
			{
				string path = $"{SoundsFolder}/{name}.wav";
				AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
				if (clip == null)
				{
					Debug.LogWarning($"[Parks] No sound at {path}, so that part of the park's life stays silent.");
					continue;
				}

				clips.Add(clip);
			}

			return clips.ToArray();
		}

		/// <summary>The palette with every swatch black except the light colours, written next to the palette.</summary>
		private static Texture2D CreateLightsMask()
		{
			string palettePath = $"{ParkAssetUtility.TexturesFolder}/{PaletteName}";
			if (!File.Exists(palettePath))
			{
				return null;
			}

			Texture2D palette = new Texture2D(2, 2, TextureFormat.RGB24, false);
			palette.LoadImage(File.ReadAllBytes(palettePath));
			Texture2D mask = new Texture2D(palette.width, palette.height, TextureFormat.RGB24, false);
			Color32[] pixels = new Color32[palette.width * palette.height];
			for (int cellY = 0; cellY < palette.height / SwatchPixels; cellY++)
			{
				for (int cellX = 0; cellX < palette.width / SwatchPixels; cellX++)
				{
					int half = SwatchPixels / 2;
					Color32 swatch = palette.GetPixel(cellX * SwatchPixels + half, cellY * SwatchPixels + half);
					string hex = $"{swatch.r:x2}{swatch.g:x2}{swatch.b:x2}";
					Color32 fill = Array.IndexOf(LightSwatches, hex) >= 0 ? swatch : new Color32(0, 0, 0, 255);
					for (int y = 0; y < SwatchPixels; y++)
					{
						for (int x = 0; x < SwatchPixels; x++)
						{
							pixels[(cellY * SwatchPixels + y) * palette.width + cellX * SwatchPixels + x] = fill;
						}
					}
				}
			}

			mask.SetPixels32(pixels);
			string path = $"{ParkAssetUtility.TexturesFolder}/{LightsMaskName}";
			File.WriteAllBytes(path, mask.EncodeToPNG());
			UnityEngine.Object.DestroyImmediate(palette);
			UnityEngine.Object.DestroyImmediate(mask);
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		private static Texture2D CreateDropletTexture()
		{
			Texture2D droplet = new Texture2D(DropletPixels, DropletPixels, TextureFormat.RGBA32, false);
			Color32[] pixels = new Color32[DropletPixels * DropletPixels];
			float center = (DropletPixels - 1) * 0.5f;
			for (int y = 0; y < DropletPixels; y++)
			{
				for (int x = 0; x < DropletPixels; x++)
				{
					float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
					byte alpha = (byte)Mathf.RoundToInt(255f * (1f - Mathf.SmoothStep(0.35f, 1f, distance)));
					pixels[y * DropletPixels + x] = new Color32(255, 255, 255, alpha);
				}
			}

			droplet.SetPixels32(pixels);
			string path = $"{ParkAssetUtility.TexturesFolder}/{DropletTextureName}";
			File.WriteAllBytes(path, droplet.EncodeToPNG());
			UnityEngine.Object.DestroyImmediate(droplet);
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
			if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
			{
				importer.alphaIsTransparency = true;
				importer.wrapMode = TextureWrapMode.Clamp;
				importer.SaveAndReimport();
			}

			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		private static Material LoadOrCopy(Material source, string path)
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				material = new Material(source) { name = Path.GetFileNameWithoutExtension(path) };
				AssetDatabase.CreateAsset(material, path);
				return material;
			}

			// Follow the source, which the builder rewrites on every run
			material.shader = source.shader;
			material.CopyPropertiesFromMaterial(source);
			return material;
		}

		#endregion
	}
}
