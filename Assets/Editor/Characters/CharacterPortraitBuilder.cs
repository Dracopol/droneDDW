using System.IO;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Characters.Editor
{
	// Menu: DroneDDW > Characters > Build Portrait Assets. Safe to run again: every asset keeps its GUID.
	// 1. Imports the portraits in Content/Textures/character_portraits (<character>_<emotion>_<closed|open>.png) at
	//    full size, with mipmaps, and with alpha as transparency, so their edges don't get dark fringes
	// 2. Fills one portrait set per character, measuring from the pixels the crop a card shows and the mouth
	// 3. Draws the cards' rounded panel as a 9-slice sprite
	// 4. Creates the settings asset if it is missing (never overwrites it)
	internal static class CharacterPortraitBuilder
	{
		#region Fields

		internal const string DataFolder = "Assets/Content/Data/Characters";
		internal const string SettingsPath = DataFolder + "/Character Portrait Settings.asset";
		internal const string PanelSpritePath = "Assets/Content/Textures/Characters/Card Panel.png";

		// The cards' portrait area, width / height
		internal const float CropAspect = 1.3f;

		// The panel sprite's corner radius, in its pixels
		internal const int PanelCornerRadius = 40;

		private const string MenuPath = "DroneDDW/Characters/Build Portrait Assets";
		private const string PortraitsFolder = "Assets/Content/Textures/character_portraits";
		private const int CropMarginPixels = 40;

		// Full size: the files are 1920 x 1080
		private const int PortraitMaxSize = 2048;
		private const int PanelSize = 128;
		private const int PanelBorder = 48;
		private const float PanelPixelsPerUnit = 100f;

		// File name, display name and card colour per character, left to right in the test scene
		internal static readonly (string File, string Name, Color Accent)[] Characters =
		{
			("grandma", "Grandma", new Color32(0xE9, 0xC0, 0x4F, 0xFF)),
			("student", "Student", new Color32(0x5B, 0x9B, 0xD5, 0xFF)),
			("worker", "Worker", new Color32(0xE6, 0x7E, 0x22, 0xFF)),
		};

		// In CharacterEmotion order
		private static readonly string[] EmotionFiles = { "natural", "happy", "angry" };

		#endregion

		#region Internal

		internal static string GetSetPath(string characterName)
		{
			return $"{DataFolder}/{characterName} Portraits.asset";
		}

		internal static string GetPortraitPath(string characterFile, int emotion, bool isMouthOpen)
		{
			string mouth = isMouthOpen ? "open" : "closed";
			return $"{PortraitsFolder}/{characterFile}_{EmotionFiles[emotion]}_{mouth}.png";
		}

		// Crop and mouth for one character, from all its portraits
		internal static void MeasureCharacter(string characterFile, out Rect cropRect, out Rect mouthRect)
		{
			RectInt visible = new(0, 0, 0, 0);
			RectInt mouth = new(0, 0, 0, 0);
			int width = 0;
			int height = 0;
			for (int emotion = 0; emotion < EmotionFiles.Length; emotion++)
			{
				Color32[] closed = CharacterPortraitAnalysis.ReadPixels(GetPortraitPath(characterFile, emotion, false),
					out width, out height);
				Color32[] open = CharacterPortraitAnalysis.ReadPixels(GetPortraitPath(characterFile, emotion, true),
					out _, out _);
				visible = CharacterPortraitAnalysis.Union(visible,
					CharacterPortraitAnalysis.FindVisibleBox(closed, width, height));
				mouth = CharacterPortraitAnalysis.Union(mouth,
					CharacterPortraitAnalysis.FindDifferenceBox(closed, open, width, height));
			}

			cropRect = CharacterPortraitAnalysis.ToCropRect(visible, width, height, CropAspect, CropMarginPixels);
			mouthRect = CharacterPortraitAnalysis.ToNormalized(mouth, width, height);
		}

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Build()
		{
			try
			{
				for (int character = 0; character < Characters.Length; character++)
				{
					(string file, string name, Color accent) = Characters[character];
					EditorUtility.DisplayProgressBar("Character portraits", $"Measuring {name}",
						(float)character / Characters.Length);
					if (!BuildCharacter(file, name, accent))
					{
						return;
					}
				}

				EditorUtility.DisplayProgressBar("Character portraits", "Panel sprite and settings", 0.9f);
				CreatePanelSprite();
				LoadOrCreateSettings();
				AssetDatabase.SaveAssets();
				Debug.Log($"[Characters] Built the portrait sets, the card panel and the settings in {DataFolder}.");
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private static bool BuildCharacter(string file, string name, Color accent)
		{
			CharacterEmotionPortraits[] emotions = new CharacterEmotionPortraits[EmotionFiles.Length];
			for (int emotion = 0; emotion < EmotionFiles.Length; emotion++)
			{
				Texture2D closed = ImportPortrait(GetPortraitPath(file, emotion, false));
				Texture2D open = ImportPortrait(GetPortraitPath(file, emotion, true));
				if (closed == null || open == null)
				{
					Debug.LogError($"[Characters] {name}'s {EmotionFiles[emotion]} portraits are missing: expected "
						+ $"{GetPortraitPath(file, emotion, false)} and its _open twin.");
					return false;
				}

				emotions[emotion] = new CharacterEmotionPortraits(closed, open);
			}

			MeasureCharacter(file, out Rect cropRect, out Rect mouthRect);
			string path = GetSetPath(name);
			CharacterPortraitSet set = AssetDatabase.LoadAssetAtPath<CharacterPortraitSet>(path);
			if (set == null)
			{
				EnsureFolder(DataFolder);
				set = ScriptableObject.CreateInstance<CharacterPortraitSet>();
				set.ConfigureLook(name, accent);
				AssetDatabase.CreateAsset(set, path);
			}

			set.Configure(emotions, cropRect, mouthRect);
			EditorUtility.SetDirty(set);
			return true;
		}

		private static Texture2D ImportPortrait(string path)
		{
			if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
			{
				return null;
			}

			// The cards show the portraits a few times smaller than the files, so they blend between mipmaps
			bool isChanged = importer.textureType != TextureImporterType.Default
				|| !importer.alphaIsTransparency
				|| !importer.mipmapEnabled
				|| importer.filterMode != FilterMode.Trilinear
				|| importer.wrapMode != TextureWrapMode.Clamp
				|| importer.npotScale != TextureImporterNPOTScale.None
				|| importer.maxTextureSize < PortraitMaxSize;
			if (isChanged)
			{
				importer.textureType = TextureImporterType.Default;
				importer.alphaIsTransparency = true;
				importer.mipmapEnabled = true;
				importer.filterMode = FilterMode.Trilinear;
				importer.wrapMode = TextureWrapMode.Clamp;
				importer.npotScale = TextureImporterNPOTScale.None;
				importer.maxTextureSize = PortraitMaxSize;
				importer.SaveAndReimport();
			}

			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		// A white rounded rectangle with soft edges; the cards tint it for the panel and the border
		private static void CreatePanelSprite()
		{
			if (!File.Exists(PanelSpritePath))
			{
				EnsureFolder(Path.GetDirectoryName(PanelSpritePath)?.Replace('\\', '/'));
				Texture2D texture = new(PanelSize, PanelSize, TextureFormat.RGBA32, false);
				try
				{
					Color32[] pixels = new Color32[PanelSize * PanelSize];
					float half = PanelSize * 0.5f;
					for (int y = 0; y < PanelSize; y++)
					{
						for (int x = 0; x < PanelSize; x++)
						{
							// Signed distance to the rounded rectangle's edge, negative inside
							float qx = Mathf.Abs(x + 0.5f - half) - (half - PanelCornerRadius);
							float qy = Mathf.Abs(y + 0.5f - half) - (half - PanelCornerRadius);
							float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
							float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - PanelCornerRadius;
							byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
							pixels[y * PanelSize + x] = new Color32(255, 255, 255, alpha);
						}
					}

					texture.SetPixels32(pixels);
					File.WriteAllBytes(PanelSpritePath, texture.EncodeToPNG());
				}
				finally
				{
					Object.DestroyImmediate(texture);
				}

				AssetDatabase.ImportAsset(PanelSpritePath);
			}

			// As many pixels per unit as a canvas, which the cards' corner radii count on
			TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(PanelSpritePath);
			bool isSet = importer.textureType == TextureImporterType.Sprite
				&& Mathf.Approximately(importer.spriteBorder.x, PanelBorder)
				&& Mathf.Approximately(importer.spritePixelsPerUnit, PanelPixelsPerUnit);
			if (isSet)
			{
				return;
			}

			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Single;
			importer.spritePixelsPerUnit = PanelPixelsPerUnit;
			importer.spriteBorder = new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder);
			importer.alphaIsTransparency = true;
			importer.mipmapEnabled = true;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.SaveAndReimport();
		}

		private static void LoadOrCreateSettings()
		{
			if (AssetDatabase.LoadAssetAtPath<CharacterPortraitSettings>(SettingsPath) != null)
			{
				return;
			}

			EnsureFolder(DataFolder);
			AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CharacterPortraitSettings>(), SettingsPath);
		}

		internal static void EnsureFolder(string folder)
		{
			if (AssetDatabase.IsValidFolder(folder))
			{
				return;
			}

			int slash = folder.LastIndexOf('/');
			string parent = folder.Substring(0, slash);
			EnsureFolder(parent);
			AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
		}

		#endregion
	}
}
