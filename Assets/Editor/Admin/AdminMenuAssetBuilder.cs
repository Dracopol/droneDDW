using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;

namespace DroneDDW.Admin.Editor
{
	// Menu: DroneDDW > Admin Menu > Build Admin Menu Assets. Safe to run again: every asset keeps its GUID.
	// 1. Makes TMP font assets from Inter Regular and SemiBold (Content/Fonts/Inter, OFL licence), static, with the
	//    characters the menu uses, plus copies of their materials at the menu's render queue
	// 2. Makes the menu's image material (UI/Default) at that queue and the pause dimmer's material
	// 3. Draws the rounded rectangle and the table frame as 9-slice sprites, and bakes the icons (AdminMenuIconBaker)
	// 4. Creates the settings asset if it is missing (never overwrites it)
	// 5. Builds the prefab, Content/Prefabs/Admin/Admin Menu.prefab (AdminMenuPrefabBuilder); edit the builders, not it
	// The menu draws at render queue 3600, after the dimmer (3500), which draws after the world's transparent objects.
	internal static class AdminMenuAssetBuilder
	{
		#region Fields

		internal const string SettingsPath = "Assets/Content/Data/Admin/Admin Menu Settings.asset";
		internal const string InputActionsPath = "Assets/Content/Input/Admin Menu.inputactions";
		internal const string RegularFontPath = FontFolder + "/Inter Regular SDF.asset";
		internal const string SemiBoldFontPath = FontFolder + "/Inter SemiBold SDF.asset";
		internal const int MenuRenderQueue = 3600;
		internal const int DimmerRenderQueue = 3500;

		// The rounded sprites' corner radius, in their pixels; Image.pixelsPerUnitMultiplier = this / wanted radius
		internal const float SpriteCornerRadius = 48f;

		// The table frame's corner radius and border width, in the menu's pixels
		internal const float TableCornerRadius = 14f;
		internal const float TableBorderWidth = 2f;

		// Panel fill and border colours from the mockups
		internal static readonly Color32 PanelColor = new(0x15, 0x1E, 0x2B, 0xFF);
		internal static readonly Color32 BorderColor = new(0x34, 0x42, 0x55, 0xFF);

		private const string MenuPath = "DroneDDW/Admin Menu/Build Admin Menu Assets";
		private const string FontFolder = "Assets/Content/Fonts/Inter";
		private const string RegularTtfPath = FontFolder + "/Inter-Regular.ttf";
		private const string SemiBoldTtfPath = FontFolder + "/Inter-SemiBold.ttf";
		private const string MaterialFolder = "Assets/Content/Materials/Admin";
		private const string TextureFolder = "Assets/Content/Textures/Admin";
		private const string RoundedRectPath = TextureFolder + "/Rounded Rect.png";
		private const string TableFramePath = TextureFolder + "/Table Frame.png";

		private const int SpriteSize = 128;
		private const int SpriteBorder = 56;
		private const float SpritePixelsPerUnit = 100f;

		// TMP atlas: glyphs sampled at 90 pt with 9 px padding (10%), in one 1024 x 1024 texture
		private const int SamplingPointSize = 90;
		private const int AtlasPadding = 9;
		private const int AtlasSize = 1024;

		// Printable ASCII plus the dash shown for missing cells and a few typographic marks
		private const string FontCharacters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`"
			+ "abcdefghijklmnopqrstuvwxyz{|}~–—…·’";

		#endregion

		#region Internal

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

		#region Private

		[MenuItem(MenuPath)]
		private static void Build()
		{
			try
			{
				EditorUtility.DisplayProgressBar("Admin menu", "Fonts", 0.1f);
				AdminMenuAssets assets = new()
				{
					Settings = LoadOrCreateSettings(),
					RegularFont = LoadOrCreateFont(RegularTtfPath, RegularFontPath),
					SemiBoldFont = LoadOrCreateFont(SemiBoldTtfPath, SemiBoldFontPath),
				};

				if (assets.RegularFont == null || assets.SemiBoldFont == null)
				{
					Debug.LogError($"[Admin] The Inter fonts are missing from {FontFolder}.");
					return;
				}

				EditorUtility.DisplayProgressBar("Admin menu", "Materials", 0.3f);
				assets.RegularText = SaveTextMaterial(assets.RegularFont, "Admin Menu Text Regular");
				assets.SemiBoldText = SaveTextMaterial(assets.SemiBoldFont, "Admin Menu Text SemiBold");
				assets.Ui = SaveUiMaterial();
				assets.Dimmer = SaveDimmerMaterial();

				EditorUtility.DisplayProgressBar("Admin menu", "Sprites and icons", 0.5f);
				assets.RoundedRect = SaveSprite(RoundedRectPath, DrawRoundedRect());
				assets.TableFrame = SaveSprite(TableFramePath, DrawTableFrame());
				assets.PlayIcon = AdminMenuIconBaker.Bake("Play", AdminMenuIconBaker.PlaySvg);
				assets.ResetIcon = AdminMenuIconBaker.Bake("Reset", AdminMenuIconBaker.ResetSvg);
				assets.HistoryIcon = AdminMenuIconBaker.Bake("History", AdminMenuIconBaker.HistorySvg);
				assets.LocationIcon = AdminMenuIconBaker.Bake("Location", AdminMenuIconBaker.LocationSvg);
				assets.LanguageIcon = AdminMenuIconBaker.Bake("Language", AdminMenuIconBaker.LanguageSvg);
				assets.SaveIcon = AdminMenuIconBaker.Bake("Save", AdminMenuIconBaker.SaveSvg);
				assets.BackIcon = AdminMenuIconBaker.Bake("Back", AdminMenuIconBaker.BackSvg);
				assets.EndIcon = AdminMenuIconBaker.Bake("End", AdminMenuIconBaker.EndSvg);

				if (!LoadActions(assets))
				{
					return;
				}

				EditorUtility.DisplayProgressBar("Admin menu", "Prefab", 0.8f);
				AdminMenuPrefabBuilder.Build(assets);
				AssetDatabase.SaveAssets();
				Debug.Log($"[Admin] Built the admin menu assets and {AdminMenuPrefabBuilder.PrefabPath}.");
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private static AdminMenuSettings LoadOrCreateSettings()
		{
			AdminMenuSettings settings = AssetDatabase.LoadAssetAtPath<AdminMenuSettings>(SettingsPath);
			if (settings != null)
			{
				return settings;
			}

			EnsureFolder(Path.GetDirectoryName(SettingsPath)?.Replace('\\', '/'));
			settings = ScriptableObject.CreateInstance<AdminMenuSettings>();
			AssetDatabase.CreateAsset(settings, SettingsPath);
			return settings;
		}

		// Made once: making it again would change the atlas and material the menu refers to
		private static TMP_FontAsset LoadOrCreateFont(string ttfPath, string assetPath)
		{
			TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
			if (fontAsset != null)
			{
				return fontAsset;
			}

			Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
			if (font == null)
			{
				return null;
			}

			fontAsset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
				AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
			string name = Path.GetFileNameWithoutExtension(assetPath);
			fontAsset.name = name;
			if (!fontAsset.TryAddCharacters(FontCharacters, out string missing))
			{
				Debug.LogWarning($"[Admin] {name} lacks these characters: {missing}");
			}

			// Static: the atlas holds every character the menu needs and never changes at runtime
			fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
			AssetDatabase.CreateAsset(fontAsset, assetPath);
			Texture2D atlas = fontAsset.atlasTexture;
			atlas.name = $"{name} Atlas";
			AssetDatabase.AddObjectToAsset(atlas, fontAsset);
			Material material = fontAsset.material;
			material.name = $"{name} Material";
			AssetDatabase.AddObjectToAsset(material, fontAsset);
			EditorUtility.SetDirty(fontAsset);
			AssetDatabase.SaveAssets();
			return fontAsset;
		}

		private static Material SaveTextMaterial(TMP_FontAsset fontAsset, string name)
		{
			Material material = LoadOrCreateMaterial(name, fontAsset.material.shader);
			material.CopyPropertiesFromMaterial(fontAsset.material);
			material.renderQueue = MenuRenderQueue;
			EditorUtility.SetDirty(material);
			return material;
		}

		private static Material SaveUiMaterial()
		{
			Material material = LoadOrCreateMaterial("Admin Menu UI", Shader.Find("UI/Default"));
			material.renderQueue = MenuRenderQueue;
			EditorUtility.SetDirty(material);
			return material;
		}

		// URP Unlit, transparent black like the rig's screen fade; the dimmer sets its opacity
		private static Material SaveDimmerMaterial()
		{
			Material material = LoadOrCreateMaterial("Pause Dimmer",
				Shader.Find("Universal Render Pipeline/Unlit"));
			material.SetFloat("_Surface", 1f);
			material.SetFloat("_Blend", 0f);
			material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
			material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
			material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
			material.SetFloat("_ZWrite", 0f);
			material.SetColor("_BaseColor", Color.black);
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Transparent");
			material.SetShaderPassEnabled("DepthOnly", false);
			material.SetShaderPassEnabled("SHADOWCASTER", false);
			material.SetShaderPassEnabled("MOTIONVECTORS", false);

			// URP recomputes a built-in shader's queue as Transparent (3000) + the queue offset whenever the material is
			// validated, so the offset is what keeps it at 3500
			material.SetFloat("_QueueOffset", DimmerRenderQueue - (int)RenderQueue.Transparent);
			material.renderQueue = DimmerRenderQueue;
			EditorUtility.SetDirty(material);
			return material;
		}

		private static Material LoadOrCreateMaterial(string name, Shader shader)
		{
			string path = $"{MaterialFolder}/{name}.mat";
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material != null)
			{
				material.shader = shader;
				return material;
			}

			EnsureFolder(MaterialFolder);
			material = new Material(shader) { name = name };
			AssetDatabase.CreateAsset(material, path);
			return material;
		}

		// Signed distance to a rounded square filling the sprite, negative inside
		private static float RoundedSquareDistance(int x, int y)
		{
			float half = SpriteSize * 0.5f;
			float qx = Mathf.Abs(x + 0.5f - half) - (half - SpriteCornerRadius);
			float qy = Mathf.Abs(y + 0.5f - half) - (half - SpriteCornerRadius);
			float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
			return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - SpriteCornerRadius;
		}

		// White with soft edges; images tint it
		private static Color32[] DrawRoundedRect()
		{
			Color32[] pixels = new Color32[SpriteSize * SpriteSize];
			for (int y = 0; y < SpriteSize; y++)
			{
				for (int x = 0; x < SpriteSize; x++)
				{
					float coverage = Mathf.Clamp01(0.5f - RoundedSquareDistance(x, y));
					pixels[y * SpriteSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
				}
			}

			return pixels;
		}

		// The border ring in the border colour, the panel colour outside it (hiding the rows' square corners) and
		// nothing inside; its width suits the table's corner radius
		private static Color32[] DrawTableFrame()
		{
			float ringWidth = TableBorderWidth * SpriteCornerRadius / TableCornerRadius;
			Color32[] pixels = new Color32[SpriteSize * SpriteSize];
			for (int y = 0; y < SpriteSize; y++)
			{
				for (int x = 0; x < SpriteSize; x++)
				{
					float distance = RoundedSquareDistance(x, y);
					float outside = Mathf.Clamp01(0.5f + distance);
					float inside = Mathf.Clamp01(0.5f - (distance + ringWidth));
					Color32 color = Color32.Lerp(BorderColor, PanelColor, outside);
					color.a = (byte)Mathf.RoundToInt((1f - inside) * 255f);
					pixels[y * SpriteSize + x] = color;
				}
			}

			return pixels;
		}

		// As many pixels per unit as a canvas, which the corner radii count on
		private static Sprite SaveSprite(string path, Color32[] pixels)
		{
			EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
			Texture2D texture = new(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
			try
			{
				texture.SetPixels32(pixels);
				File.WriteAllBytes(path, texture.EncodeToPNG());
			}
			finally
			{
				Object.DestroyImmediate(texture);
			}

			AssetDatabase.ImportAsset(path);
			TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Single;
			importer.spritePixelsPerUnit = SpritePixelsPerUnit;
			importer.spriteBorder = new Vector4(SpriteBorder, SpriteBorder, SpriteBorder, SpriteBorder);
			importer.alphaIsTransparency = true;
			importer.mipmapEnabled = true;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();
			return AssetDatabase.LoadAssetAtPath<Sprite>(path);
		}

		// The input actions file's action references (sub-assets Unity makes when it imports the file)
		private static bool LoadActions(AdminMenuAssets assets)
		{
			InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
				.OfType<InputActionReference>().ToArray();
			assets.ToggleAction = FindAction(references, "Toggle Menu");
			assets.NavigateAction = FindAction(references, "Navigate");
			assets.ClickAction = FindAction(references, "Click");
			assets.BackAction = FindAction(references, "Back");
			if (assets.ToggleAction != null && assets.NavigateAction != null && assets.ClickAction != null
				&& assets.BackAction != null)
			{
				return true;
			}

			Debug.LogError($"[Admin] {InputActionsPath} lacks the Toggle Menu, Navigate, Click or Back action.");
			return false;
		}

		private static InputActionReference FindAction(InputActionReference[] references, string actionName)
		{
			return references.FirstOrDefault(reference => reference.action?.name == actionName);
		}

		#endregion
	}
}
