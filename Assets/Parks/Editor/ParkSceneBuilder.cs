using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// Menu: DroneDDW / Parks / Build A-B Scenes (the two stand-alone parks from the first version).
	/// 1. Creates one URP Lit material per category (all sharing the palette texture).
	/// 2. Remaps the FBX materials to them and reimports the park models.
	/// 3. Saves every modular piece of Park_A_Kit / Park_B_Kit as a prefab.
	/// 4. Builds Park_A_NatureFirst.unity and Park_B_EfficiencyFirst.unity from the layout files,
	///    grouped Terrain / Paths / Vegetation / Props / Structures, with a light probe grid.
	/// Safe to run again: materials and prefabs are updated in place and keep their GUIDs.
	/// </summary>
	internal static class ParkSceneBuilder
	{
		#region Fields

		private const string MenuRoot = "DroneDDW/Parks/";
		private const string DialogTitle = "Park scenes";
		private const float ProbeSpacing = 10f;
		private const float HalfExtent = 50f;

		private static readonly string[] ParkNames = { "Park_A_NatureFirst", "Park_B_EfficiencyFirst" };
		private static readonly string[] KitNames = { "Park_A_Kit", "Park_B_Kit" };
		private static readonly string[] GroupNames = { "Terrain", "Paths", "Vegetation", "Props", "Structures" };
		private static readonly float[] ProbeHeights = { 0.6f, 4.5f };
		private static readonly Vector3 CameraPosition = new Vector3(0f, 1.7f, -53f);
		private static readonly Vector3 CameraEuler = new Vector3(4f, 0f, 0f);

		#endregion

		#region Private

		[MenuItem(MenuRoot + "Build A-B Scenes")]
		private static void BuildAll()
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			string root = ParkAssetUtility.FindRootFolder();
			if (string.IsNullOrEmpty(root))
			{
				EditorUtility.DisplayDialog(DialogTitle, "Could not find " + ParkAssetUtility.PaletteFileName + ". Import the whole Parks folder first.", "OK");
				return;
			}

			if (ParkAssetUtility.LoadModel(root, KitNames[0]) == null)
			{
				EditorUtility.DisplayDialog(DialogTitle, "The A/B park files (Park_A_Kit.fbx and friends) are not in " + root + "/Models.", "OK");
				return;
			}

			try
			{
				EditorUtility.DisplayProgressBar(DialogTitle, "Creating materials", 0.1f);
				Dictionary<string, Material> materials = ParkAssetUtility.CreateMaterials(root);

				EditorUtility.DisplayProgressBar(DialogTitle, "Remapping and reimporting the FBX files", 0.25f);
				ParkAssetUtility.RemapModels(root, materials);

				EditorUtility.DisplayProgressBar(DialogTitle, "Saving prefabs", 0.45f);
				EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
				int prefabCount = 0;
				foreach (string kitName in KitNames)
				{
					prefabCount += CreatePrefabs(root, kitName);
				}

				int instanceCount = 0;
				for (int i = 0; i < ParkNames.Length; i++)
				{
					EditorUtility.DisplayProgressBar(DialogTitle, "Building " + ParkNames[i], 0.6f + 0.2f * i);
					instanceCount += BuildScene(root, ParkNames[i]);
				}

				AssetDatabase.SaveAssets();
				EditorSceneManager.OpenScene($"{root}/{ParkAssetUtility.ScenesFolder}/{ParkNames[0]}.unity", OpenSceneMode.Single);
				Debug.Log($"[Parks] Built {prefabCount} prefabs and {ParkNames.Length} scenes ({instanceCount} placed objects) in {root}.");
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		[MenuItem(MenuRoot + "Reimport Park Models")]
		private static void ReimportModels()
		{
			string root = ParkAssetUtility.FindRootFolder();
			if (!string.IsNullOrEmpty(root))
			{
				ParkAssetUtility.ReimportModels(root);
			}
		}

		private static int CreatePrefabs(string root, string kitName)
		{
			GameObject kit = ParkAssetUtility.LoadModel(root, kitName);
			if (kit == null)
			{
				Debug.LogError($"[Parks] Missing {kitName}.fbx in {root}/{ParkAssetUtility.ModelsFolder}.");
				return 0;
			}

			string parkFolder = kitName.Substring(0, kitName.LastIndexOf('_'));
			string folder = ParkAssetUtility.EnsureFolder(ParkAssetUtility.EnsureFolder(root, ParkAssetUtility.PrefabsFolder), parkFolder);
			int count = 0;
			foreach (Transform piece in kit.transform)
			{
				ParkAssetUtility.SavePiecePrefab(piece.gameObject, folder);
				count++;
			}

			return count;
		}

		private static int BuildScene(string root, string parkName)
		{
			TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{ParkAssetUtility.LayoutsFolder}/{parkName}_Layout.json");
			if (json == null)
			{
				Debug.LogError($"[Parks] Missing layout for {parkName}.");
				return 0;
			}

			ParkLayout layout = JsonUtility.FromJson<ParkLayout>(json.text);
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
			GameObject parkRoot = new GameObject(layout.ParkName);
			Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
			foreach (string groupName in GroupNames)
			{
				GameObject group = new GameObject(groupName);
				group.transform.SetParent(parkRoot.transform, false);
				groups.Add(groupName, group.transform);
			}

			string prefabFolder = $"{root}/{ParkAssetUtility.PrefabsFolder}/{layout.PrefabFolder}";
			Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
			int placed = 0;
			int missing = 0;
			foreach (ParkLayoutEntry entry in layout.Entries)
			{
				if (!prefabs.TryGetValue(entry.Prefab, out GameObject prefab))
				{
					prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabFolder}/{entry.Prefab}.prefab");
					prefabs.Add(entry.Prefab, prefab);
				}

				if (prefab == null)
				{
					missing++;
					continue;
				}

				if (!groups.TryGetValue(entry.Group, out Transform parent))
				{
					parent = parkRoot.transform;
				}

				GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
				instance.name = entry.Name;
				Transform instanceTransform = instance.transform;
				instanceTransform.localPosition = entry.Position;
				instanceTransform.localRotation = Quaternion.Euler(0f, entry.RotationY, 0f);
				instanceTransform.localScale = entry.Scale;
				placed++;
			}

			GameObjectUtility.SetStaticEditorFlags(parkRoot, StaticEditorFlags.BatchingStatic);
			foreach (Transform group in groups.Values)
			{
				GameObjectUtility.SetStaticEditorFlags(group.gameObject, StaticEditorFlags.BatchingStatic);
			}

			AddLightProbes(parkRoot.transform);
			PlaceCamera(scene);
			string scenesFolder = ParkAssetUtility.EnsureFolder(root, ParkAssetUtility.ScenesFolder);
			EditorSceneManager.SaveScene(scene, $"{scenesFolder}/{parkName}.unity");
			if (missing > 0)
			{
				Debug.LogWarning($"[Parks] {parkName}: {missing} layout entries had no prefab.");
			}

			return placed;
		}

		private static void AddLightProbes(Transform parent)
		{
			GameObject probes = new GameObject("LightProbes");
			probes.transform.SetParent(parent, false);
			LightProbeGroup group = probes.AddComponent<LightProbeGroup>();
			List<Vector3> positions = new List<Vector3>();
			for (float x = -HalfExtent; x <= HalfExtent + 0.01f; x += ProbeSpacing)
			{
				for (float z = -HalfExtent; z <= HalfExtent + 0.01f; z += ProbeSpacing)
				{
					foreach (float y in ProbeHeights)
					{
						positions.Add(new Vector3(x, y, z));
					}
				}
			}

			group.probePositions = positions.ToArray();
		}

		private static void PlaceCamera(Scene scene)
		{
			foreach (GameObject rootObject in scene.GetRootGameObjects())
			{
				if (!rootObject.TryGetComponent(out Camera camera))
				{
					continue;
				}

				// Eye height at the shared south entrance, looking north into the park.
				camera.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraEuler));
				camera.nearClipPlane = 0.1f;
				camera.farClipPlane = 400f;
			}
		}

		#endregion
	}
}
