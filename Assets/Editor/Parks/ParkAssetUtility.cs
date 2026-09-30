using System.Collections.Generic;
using System.IO;
using DroneDDW.Parks.Editor.Import;
using UnityEditor;
using UnityEngine;

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
		internal const string MaterialsFolder = "Assets/Content/Materials/Parks";
		internal const string PrefabsFolder = "Assets/Content/Prefabs/Parks";
		internal const string ScenesFolder = "Assets/Scenes";

		private const string PaletteFileName = "Park_Palette.png";
		private const string MaquettePaletteFileName = "Park_Palette_Maquette.png";
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
