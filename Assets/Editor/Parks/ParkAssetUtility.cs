using System.Collections.Generic;
using System.IO;
using DroneDDW.Parks.Editor.Import;
using DroneDDW.Parks.Table;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// Asset steps shared by both builders: the park folders, the palette materials,
	/// material remapping on the FBX files, and saving kit pieces as prefabs.
	/// </summary>
	internal static class ParkAssetUtility
	{
		#region Fields

		// Where the park files live in the project's folder structure.
		internal const string ModelsFolder = "Assets/Content/Models/Parks";
		internal const string LayoutsFolder = "Assets/Content/Data/Parks";
		internal const string TexturesFolder = "Assets/Content/Textures/Parks";
		internal const string AudioFolder = "Assets/Content/Audio/Ambience";
		internal const string SoundEffectsFolder = "Assets/Content/Audio/SFX";
		internal const string MaterialsFolder = "Assets/Content/Materials/Parks";
		internal const string PrefabsFolder = "Assets/Content/Prefabs/Parks";
		internal const string ScenesFolder = "Assets/Scenes";

		private const string PushSettingsPath = LayoutsFolder + "/Park Push Settings.asset";
		private const string PaletteFileName = "Park_Palette.png";
		private const string MaquettePaletteFileName = "Park_Palette_Maquette.png";
		private const string MaterialPrefix = "Park_Mat_";
		private const string PropsKey = "Props";
		private const string VegetationKey = "Vegetation";
		private const string VegetationShader = "DroneDDW/Parks/Vegetation Wind";
		private const string WaterShader = "DroneDDW/Parks/Water";
		private const string GlowSuffix = "_Glow";
		private const string EmissionKeyword = "_EMISSION";
		private const string EmissionColorProperty = "_EmissionColor";
		private const string MotionVectorsPass = "MotionVectors";
		private const string WaterKey = "Water";
		private const string MaquetteKey = "Maquette";
		private const string HandSpheresKey = "HandSpheres";
		private const string TransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
		private const string ShadowCasterPass = "ShadowCaster";
		private const string UrpUnlitShader = "Universal Render Pipeline/Unlit";
		private const string UrpLitShader = "Universal Render Pipeline/Lit";
		private const string BuiltInLitShader = "Standard";
		private const float MatteSmoothness = 0.1f;
		private const float WaterSmoothness = 0.85f;
		private const float MaquetteSmoothness = 0.05f;

		private static readonly string[] MaterialKeys =
		{
			"Terrain", "Paths", VegetationKey, PropsKey, "Structures", WaterKey, MaquetteKey,
		};

		#endregion

		#region Internal

		/// <summary>Creates every missing folder along an Assets path and returns the path.</summary>
		internal static string EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
			{
				return path;
			}

			string parent = EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
			AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
			return path;
		}

		internal static List<string> FindParkModels()
		{
			List<string> paths = new List<string>();
			foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (ParkModelPostprocessor.IsParkModelPath(path))
				{
					paths.Add(path);
				}
			}

			return paths;
		}

		internal static GameObject LoadModel(string fileName)
		{
			return AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{fileName}.fbx");
		}

		/// <summary>
		/// The push settings shared by every park slider (Content/Data/Parks/Park Push Settings.asset): created with the
		/// defaults the first time, never overwritten after, so tuning survives every rebuild.
		/// </summary>
		internal static ParkPushSettings LoadOrCreatePushSettings()
		{
			ParkPushSettings settings = AssetDatabase.LoadAssetAtPath<ParkPushSettings>(PushSettingsPath);
			if (settings != null)
			{
				return settings;
			}

			EnsureFolder(LayoutsFolder);
			settings = ScriptableObject.CreateInstance<ParkPushSettings>();
			AssetDatabase.CreateAsset(settings, PushSettingsPath);
			AssetDatabase.SaveAssets();
			return settings;
		}

		/// <summary>Editor-time lookup of a node by name (the Blender build script's nodes, the rig's hand visuals).</summary>
		internal static Transform FindDeep(Transform parent, string name)
		{
			foreach (Transform child in parent)
			{
				if (child.name == name)
				{
					return child;
				}

				Transform found = FindDeep(child, name);
				if (found != null)
				{
					return found;
				}
			}

			return null;
		}

		/// <summary>One URP Lit material per category, all sampling the palette (the maquette its paler copy).</summary>
		internal static Dictionary<string, Material> CreateMaterials()
		{
			string folder = EnsureFolder(MaterialsFolder);
			Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{PaletteFileName}");
			Texture2D maquettePalette = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{MaquettePaletteFileName}");
			Shader shader = Shader.Find(UrpLitShader);
			if (shader == null)
			{
				Debug.LogWarning("[Parks] URP Lit shader not found, using the built-in Standard shader.");
				shader = Shader.Find(BuiltInLitShader);
			}

			Dictionary<string, Material> materials = new Dictionary<string, Material>();
			foreach (string key in MaterialKeys)
			{
				string materialName = MaterialPrefix + key;
				string path = $"{folder}/{materialName}.mat";
				Shader keyShader = ShaderFor(key, shader);
				Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
				if (material == null)
				{
					material = new Material(keyShader) { name = materialName };
					AssetDatabase.CreateAsset(material, path);
				}
				else if (material.shader != keyShader)
				{
					material.shader = keyShader;
				}

				bool isMaquette = key == MaquetteKey;
				float smoothness = key == WaterKey ? WaterSmoothness : isMaquette ? MaquetteSmoothness : MatteSmoothness;
				material.mainTexture = isMaquette && maquettePalette != null ? maquettePalette : palette;
				material.color = Color.white;
				SetFloatIfPresent(material, "_Smoothness", smoothness);
				SetFloatIfPresent(material, "_Glossiness", smoothness);

				// As URP's Lit inspector sets it (the park's own shaders use that inspector too): no material here
				// writes motion vectors of its own
				material.SetShaderPassEnabled(MotionVectorsPass, false);
				EditorUtility.SetDirty(material);
				materials.Add(materialName, material);
			}

			AssetDatabase.SaveAssets();
			return materials;
		}

		/// <summary>
		/// The props material with emission switched on and black (Park_Mat_Props_Glow), for the table parts that
		/// light up at runtime: it looks the same until ParkSliderVisuals lights a part. Being used in the scene keeps
		/// the emission shader variant in the Quest build. Call after <see cref="CreateMaterials"/>.
		/// </summary>
		internal static Material CreateGlowMaterial()
		{
			string propsPath = $"{MaterialsFolder}/{MaterialPrefix}{PropsKey}.mat";
			Material props = AssetDatabase.LoadAssetAtPath<Material>(propsPath);
			if (props == null)
			{
				Debug.LogWarning($"[Parks] No props material at {propsPath}, so the slider parts can't glow.");
				return null;
			}

			string glowName = MaterialPrefix + PropsKey + GlowSuffix;
			string path = $"{EnsureFolder(MaterialsFolder)}/{glowName}.mat";
			Material glow = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (glow == null)
			{
				glow = new Material(props) { name = glowName };
				AssetDatabase.CreateAsset(glow, path);
			}
			else
			{
				// Follow the props material, which the builder rewrites on every run.
				glow.shader = props.shader;
				glow.CopyPropertiesFromMaterial(props);
			}

			// URP 17 keeps emission on only while a material is flagged as emissive for GI; realtime keeps it on with a
			// black colour (baked would be treated as "emissive is black"), and costs nothing without realtime GI.
			glow.EnableKeyword(EmissionKeyword);
			glow.SetColor(EmissionColorProperty, Color.black);
			glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
			EditorUtility.SetDirty(glow);
			AssetDatabase.SaveAssets();
			return glow;
		}

		/// <summary>
		/// A see-through URP Unlit material with GPU instancing (Park_Mat_HandSpheres), for the testing view of the
		/// hands' push spheres. Being used in the scene keeps its shader variant in the Quest build. Null without URP.
		/// </summary>
		internal static Material CreateHandSpheresMaterial()
		{
			Shader shader = Shader.Find(UrpUnlitShader);
			if (shader == null)
			{
				Debug.LogWarning("[Parks] URP Unlit shader not found, so the hand spheres can't be shown.");
				return null;
			}

			string materialName = MaterialPrefix + HandSpheresKey;
			string path = $"{EnsureFolder(MaterialsFolder)}/{materialName}.mat";
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				material = new Material(shader) { name = materialName };
				AssetDatabase.CreateAsset(material, path);
			}

			// Transparent, as URP's Unlit inspector sets it; the view gives each sphere its colour
			material.shader = shader;
			SetFloatIfPresent(material, "_Surface", 1f);
			SetFloatIfPresent(material, "_Blend", 0f);
			SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
			SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
			SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
			SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
			SetFloatIfPresent(material, "_ZWrite", 0f);
			material.EnableKeyword(TransparentKeyword);
			material.SetOverrideTag("RenderType", "Transparent");
			material.renderQueue = (int)RenderQueue.Transparent;
			material.SetShaderPassEnabled(ShadowCasterPass, false);
			material.SetShaderPassEnabled(MotionVectorsPass, false);
			material.color = Color.white;
			material.enableInstancing = true;
			EditorUtility.SetDirty(material);
			AssetDatabase.SaveAssets();
			return material;
		}

		internal static void RemapModels(Dictionary<string, Material> materials)
		{
			foreach (string path in FindParkModels())
			{
				ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
				if (importer == null)
				{
					continue;
				}

				foreach (KeyValuePair<string, Material> pair in materials)
				{
					importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
				}

				// Reimporting also runs ParkModelPostprocessor, so colliders and static flags are current.
				importer.SaveAndReimport();
			}
		}

		internal static void ReimportModels()
		{
			foreach (string path in FindParkModels())
			{
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			}
		}

		/// <summary>Reimports the ambience loops so ParkModelPostprocessor's streaming settings are current.</summary>
		internal static void ReimportAmbience()
		{
			if (!AssetDatabase.IsValidFolder(AudioFolder))
			{
				return;
			}

			foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (ParkModelPostprocessor.IsAmbiencePath(path))
				{
					AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
				}
			}
		}

		/// <summary>Saves one kit piece as a prefab at the origin. Updating an existing prefab keeps its GUID.</summary>
		internal static GameObject SavePiecePrefab(GameObject piece, string folder)
		{
			GameObject clone = Object.Instantiate(piece);
			clone.name = piece.name;
			clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(clone, $"{folder}/{piece.name}.prefab");
			Object.DestroyImmediate(clone);
			return prefab;
		}

		#endregion

		#region Private

		/// <summary>
		/// The park's own shader for plants (they sway in the wind) and water (waves and ripples), both URP Lit with one
		/// hook; URP Lit for everything else, and as the fallback if either is missing or doesn't compile.
		/// </summary>
		private static Shader ShaderFor(string key, Shader lit)
		{
			string name = key == VegetationKey ? VegetationShader : key == WaterKey ? WaterShader : null;
			if (name == null)
			{
				return lit;
			}

			Shader custom = Shader.Find(name);
			if (custom == null || ShaderUtil.ShaderHasError(custom))
			{
				Debug.LogWarning($"[Parks] Shader {name} is missing or doesn't compile, "
					+ $"so {MaterialPrefix}{key} stays on URP Lit.");
				return lit;
			}

			return custom;
		}

		private static void SetFloatIfPresent(Material material, string property, float value)
		{
			if (material.HasProperty(property))
			{
				material.SetFloat(property, value);
			}
		}

		#endregion
	}
}
