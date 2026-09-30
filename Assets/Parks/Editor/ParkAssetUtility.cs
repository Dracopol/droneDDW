using System.Collections.Generic;
using System.IO;
using DroneDDW.Parks.Editor.Import;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// Asset steps shared by both builders: locating the Parks folder, the palette materials,
	/// material remapping on the FBX files, and saving kit pieces as prefabs.
	/// </summary>
	internal static class ParkAssetUtility
	{
		#region Fields

		internal const string ModelsFolder = "Models";
		internal const string LayoutsFolder = "Layouts";
		internal const string TexturesFolder = "Textures";
		internal const string MaterialsFolder = "Materials";
		internal const string PrefabsFolder = "Prefabs";
		internal const string ScenesFolder = "Scenes";
		internal const string AudioFolder = "Audio";
		internal const string PaletteFileName = "Park_Palette.png";

		private const string MaquettePaletteFileName = "Park_Palette_Maquette.png";
		private const string PaletteSearch = "Park_Palette t:Texture2D";
		private const string MaterialPrefix = "Park_Mat_";
		private const string WaterKey = "Water";
		private const string MaquetteKey = "Maquette";
		private const string UrpLitShader = "Universal Render Pipeline/Lit";
		private const string BuiltInLitShader = "Standard";
		private const float MatteSmoothness = 0.1f;
		private const float WaterSmoothness = 0.85f;
		private const float MaquetteSmoothness = 0.05f;

		private static readonly string[] MaterialKeys = { "Terrain", "Paths", "Vegetation", "Props", "Structures", WaterKey, MaquetteKey };

		#endregion

		#region Internal

		/// <summary>The Parks folder, found through the palette texture so it can live anywhere under Assets.</summary>
		internal static string FindRootFolder()
		{
			foreach (string guid in AssetDatabase.FindAssets(PaletteSearch))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (Path.GetFileName(path) != PaletteFileName)
				{
					continue;
				}

				string texturesFolder = Path.GetDirectoryName(path);
				string root = Path.GetDirectoryName(texturesFolder);
				return string.IsNullOrEmpty(root) ? null : root.Replace('\\', '/');
			}

			return null;
		}

		internal static string EnsureFolder(string parent, string name)
		{
			string path = $"{parent}/{name}";
			if (!AssetDatabase.IsValidFolder(path))
			{
				AssetDatabase.CreateFolder(parent, name);
			}

			return path;
		}

		internal static List<string> FindParkModels(string root)
		{
			List<string> paths = new List<string>();
			foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { $"{root}/{ModelsFolder}" }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (ParkModelPostprocessor.IsParkModelPath(path))
				{
					paths.Add(path);
				}
			}

			return paths;
		}

		internal static GameObject LoadModel(string root, string fileName)
		{
			return AssetDatabase.LoadAssetAtPath<GameObject>($"{root}/{ModelsFolder}/{fileName}.fbx");
		}

		/// <summary>One URP Lit material per category, all sampling the palette (the maquette its paler copy).</summary>
		internal static Dictionary<string, Material> CreateMaterials(string root)
		{
			string folder = EnsureFolder(root, MaterialsFolder);
			Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>($"{root}/{TexturesFolder}/{PaletteFileName}");
			Texture2D maquettePalette = AssetDatabase.LoadAssetAtPath<Texture2D>($"{root}/{TexturesFolder}/{MaquettePaletteFileName}");
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
				Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
				if (material == null)
				{
					material = new Material(shader) { name = materialName };
					AssetDatabase.CreateAsset(material, path);
				}
				else if (material.shader != shader)
				{
					material.shader = shader;
				}

				bool isMaquette = key == MaquetteKey;
				float smoothness = key == WaterKey ? WaterSmoothness : isMaquette ? MaquetteSmoothness : MatteSmoothness;
				material.mainTexture = isMaquette && maquettePalette != null ? maquettePalette : palette;
				material.color = Color.white;
				SetFloatIfPresent(material, "_Smoothness", smoothness);
				SetFloatIfPresent(material, "_Glossiness", smoothness);
				EditorUtility.SetDirty(material);
				materials.Add(materialName, material);
			}

			AssetDatabase.SaveAssets();
			return materials;
		}

		internal static void RemapModels(string root, Dictionary<string, Material> materials)
		{
			foreach (string path in FindParkModels(root))
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

		internal static void ReimportModels(string root)
		{
			foreach (string path in FindParkModels(root))
			{
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			}
		}

		/// <summary>Reimports the ambience loops so ParkModelPostprocessor's streaming settings are current.</summary>
		internal static void ReimportAmbience(string root)
		{
			string folder = $"{root}/{AudioFolder}";
			if (!AssetDatabase.IsValidFolder(folder))
			{
				return;
			}

			foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
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
