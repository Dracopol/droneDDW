using System.Collections.Generic;
using DroneDDW.Parks.Audio;
using DroneDDW.Parks.Editor.Life;
using DroneDDW.Parks.Experience;
using DroneDDW.Parks.Levels;
using DroneDDW.Parks.Table;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// Menu: DroneDDW / Parks / Build Level Park (5 Levels).
	/// 1. Creates the palette materials (plus the pale maquette one) and reimports every park FBX
	///    and ambience loop.
	/// 2. Saves the modular pieces of Park_Levels_Kit as prefabs (Content/Prefabs/Parks/Park_Levels).
	/// 3. Builds Scenes/Park Levels.unity: one park holding all five levels, every changing object
	///    tagged with the levels it belongs to, a level controller that runs the wave transition,
	///    the seat anchor with a copy of the project's XR rig (see <see cref="ParkRigBuilder"/>), the floating board
	///    that follows the head (see <see cref="ParkBoardBuilder"/>) with the five-detent slider and the five miniatures,
	///    the "Slider Tuning" object showing the shared push and board follow settings, two ambience sources playing the
	///    Content/Audio/Ambience loops and a realtime sun.
	/// Safe to run again: materials and prefabs keep their GUIDs, the scene is rebuilt.
	/// </summary>
	internal static class ParkLevelSceneBuilder
	{
		#region Fields

		private const string MenuPath = "DroneDDW/Parks/Build Level Park (5 Levels)";
		private const string DialogTitle = "Level park";
		private const string LayoutFileName = "Park_Levels_Layout.json";
		private const string KitName = "Park_Levels_Kit";
		private const string ModelName = "Park_Levels_Model";
		private const string SceneName = "Park Levels";
		private const string ModelLevelPrefix = "Model_Level_";
		private const string NatureClipFileName = "Park_Ambience_Nature.ogg";
		private const string EfficiencyClipFileName = "Park_Ambience_Efficiency.ogg";
		private const int StartLevel = 2;

		// Testing aid: draws the spheres the hands push the slider with, and the knob's push and grab boxes
		private const bool ShowHandSpheres = false;
		private const float SeatHeight = 0.12f;
		private const float EyeHeight = 1.2f;
		private const float FarClipPlane = 300f;
		private const float SunIntensity = 1.1f;

		private static readonly string[] GroupNames = { "Terrain", "Paths", "Vegetation", "Props", "Structures" };
		private static readonly Vector3 SunEuler = new Vector3(50f, 30f, 0f);
		private static readonly Vector3 CameraEuler = new Vector3(14f, 0f, 0f);
		private static readonly Color SunColor = new Color(1f, 0.957f, 0.86f);

		private const StaticEditorFlags FixedObjectFlags = StaticEditorFlags.BatchingStatic
			| StaticEditorFlags.OccludeeStatic
			| StaticEditorFlags.ReflectionProbeStatic;

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Build()
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{ParkAssetUtility.LayoutsFolder}/{LayoutFileName}");
			if (json == null || ParkAssetUtility.LoadModel(KitName) == null)
			{
				EditorUtility.DisplayDialog(DialogTitle,
					$"Missing {LayoutFileName} in {ParkAssetUtility.LayoutsFolder} or {KitName}.fbx in {ParkAssetUtility.ModelsFolder}.", "OK");
				return;
			}

			try
			{
				EditorUtility.DisplayProgressBar(DialogTitle, "Creating materials", 0.1f);
				Dictionary<string, Material> materials = ParkAssetUtility.CreateMaterials();

				EditorUtility.DisplayProgressBar(DialogTitle, "Remapping and reimporting the FBX and audio files", 0.25f);
				ParkAssetUtility.RemapModels(materials);
				ParkAssetUtility.ReimportAmbience();

				// Load after the reimport so no reference points at a replaced import result.
				GameObject kit = ParkAssetUtility.LoadModel(KitName);
				GameObject rig = ParkAssetUtility.LoadModel(ParkBoardBuilder.RigModelName);
				GameObject models = ParkAssetUtility.LoadModel(ModelName);
				ParkLayout layout = JsonUtility.FromJson<ParkLayout>(json.text);

				EditorUtility.DisplayProgressBar(DialogTitle, "Saving prefabs", 0.45f);
				EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
				Dictionary<string, GameObject> pieces = IndexChildren(kit);
				Dictionary<string, GameObject> prefabs = CreatePrefabs(layout, pieces);

				EditorUtility.DisplayProgressBar(DialogTitle, "Building the scene", 0.65f);
				string scenePath = BuildScene(layout, pieces, prefabs, rig, models);
				AssetDatabase.SaveAssets();
				if (!string.IsNullOrEmpty(scenePath))
				{
					Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private static Dictionary<string, GameObject> IndexChildren(GameObject model)
		{
			Dictionary<string, GameObject> children = new Dictionary<string, GameObject>();
			foreach (Transform child in model.transform)
			{
				children[child.name] = child.gameObject;
			}

			return children;
		}

		/// <summary>Prefabs for the repeated pieces only; one-off pieces are copied straight into the scene.</summary>
		private static Dictionary<string, GameObject> CreatePrefabs(ParkLayout layout, Dictionary<string, GameObject> pieces)
		{
			string folder = ParkAssetUtility.EnsureFolder($"{ParkAssetUtility.PrefabsFolder}/{layout.PrefabFolder}");
			Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
			foreach (ParkLayoutEntry entry in layout.Entries)
			{
				if (entry.Unique || prefabs.ContainsKey(entry.Prefab))
				{
					continue;
				}

				if (!pieces.TryGetValue(entry.Prefab, out GameObject piece))
				{
					Debug.LogError($"[Parks] {KitName}.fbx has no piece named {entry.Prefab}.");
					prefabs.Add(entry.Prefab, null);
					continue;
				}

				prefabs.Add(entry.Prefab, ParkAssetUtility.SavePiecePrefab(piece, folder));
			}

			return prefabs;
		}

		private static string BuildScene(ParkLayout layout, Dictionary<string, GameObject> pieces,
			Dictionary<string, GameObject> prefabs, GameObject rigModel, GameObject levelModels)
		{
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
			GameObject seat = new GameObject("SeatAnchor");
			seat.transform.position = new Vector3(0f, SeatHeight, 0f);
			Transform head = SetUpSunAndViewer(scene, seat.transform, out Transform trackingSpace);

			GameObject world = new GameObject(layout.ParkName);
			Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
			foreach (string groupName in GroupNames)
			{
				GameObject group = new GameObject(groupName);
				group.transform.SetParent(world.transform, false);
				groups.Add(groupName, group.transform);
			}

			List<ParkLevelElement> elements = new List<ParkLevelElement>();
			List<ParkLifePiece> lifePieces = new List<ParkLifePiece>();
			int missing = 0;
			foreach (ParkLayoutEntry entry in layout.Entries)
			{
				if (!groups.TryGetValue(entry.Group, out Transform parent))
				{
					parent = world.transform;
				}

				GameObject instance = CreateInstance(entry, pieces, prefabs, parent);
				if (instance == null)
				{
					missing++;
					continue;
				}

				Transform body = instance.transform;
				if (ParkLifeBuilder.IsMover(entry.Prefab))
				{
					// The level element goes on a wrapper, the mesh on a child the park's life moves around
					instance = WrapMover(entry, body, parent);
				}

				Transform instanceTransform = instance.transform;
				instanceTransform.localPosition = entry.Position;
				instanceTransform.localRotation = Quaternion.Euler(0f, entry.RotationY, 0f);
				instanceTransform.localScale = entry.Scale;
				if ((entry.LevelMask & ParkLevels.AllMask) == ParkLevels.AllMask)
				{
					// Ground and seat platform never change: static, so they batch.
					GameObjectUtility.SetStaticEditorFlags(instance, FixedObjectFlags);
					lifePieces.Add(new ParkLifePiece(entry, instance, body, null));
					continue;
				}

				ParkLevelElement element = instance.AddComponent<ParkLevelElement>();
				element.Configure(entry.LevelMask, (ParkTransitionStyle)entry.Style, entry.LiftDepth);
				elements.Add(element);
				lifePieces.Add(new ParkLifePiece(entry, instance, body, element));
			}

			GameObject experience = new GameObject("Park_Experience");
			ParkLevelController controller = experience.AddComponent<ParkLevelController>();
			controller.Configure(elements.ToArray(), seat.transform, StartLevel);
			controller.ShowLevelInEditor(StartLevel);

			ParkPushSettings pushSettings = ParkAssetUtility.LoadOrCreatePushSettings();
			ParkBoardFollowSettings followSettings = ParkAssetUtility.LoadOrCreateFollowSettings();
			ParkBoardBuilder.Board board = ParkBoardBuilder.Build(rigModel, false, seat.transform.position, StartLevel, head,
				trackingSpace, controller, pushSettings, followSettings, ShowHandSpheres);
			ParkModelPreview preview = AddMiniatures(levelModels, board, world.transform, head);
			ParkAmbience ambience = BuildAmbience(experience.transform);
			ParkExperience coordinator = experience.AddComponent<ParkExperience>();
			coordinator.Configure(controller, board.Slider, preview, ambience);
			ParkLevelDebugKeys debugKeys = experience.AddComponent<ParkLevelDebugKeys>();
			debugKeys.Configure(controller);
			ParkLifeBuilder.Build(lifePieces, controller, head);
			ParkBoardBuilder.AddTuningObject(pushSettings, followSettings, board);

			string scenesFolder = ParkAssetUtility.EnsureFolder(ParkAssetUtility.ScenesFolder);
			string scenePath = $"{scenesFolder}/{SceneName}.unity";
			EditorSceneManager.SaveScene(scene, scenePath);
			if (missing > 0)
			{
				Debug.LogWarning($"[Parks] {missing} layout entries had no piece or prefab.");
			}

			Debug.Log($"[Parks] Built {scenePath}: {elements.Count} changing objects over {ParkLevels.Count} levels, starting at level {StartLevel} ({ParkLevels.GetName(StartLevel)}).");
			return scenePath;
		}

		/// <summary>An empty named after the entry, holding the mesh as "Body" at its origin.</summary>
		private static GameObject WrapMover(ParkLayoutEntry entry, Transform body, Transform parent)
		{
			GameObject wrapper = new GameObject(entry.Name);
			wrapper.transform.SetParent(parent, false);
			body.SetParent(wrapper.transform, false);
			body.name = "Body";
			body.localPosition = Vector3.zero;
			body.localRotation = Quaternion.identity;
			body.localScale = Vector3.one;
			return wrapper;
		}

		private static GameObject CreateInstance(ParkLayoutEntry entry, Dictionary<string, GameObject> pieces,
			Dictionary<string, GameObject> prefabs, Transform parent)
		{
			GameObject instance;
			if (entry.Unique)
			{
				if (!pieces.TryGetValue(entry.Prefab, out GameObject piece))
				{
					return null;
				}

				instance = Object.Instantiate(piece, parent);
			}
			else
			{
				if (!prefabs.TryGetValue(entry.Prefab, out GameObject prefab) || prefab == null)
				{
					return null;
				}

				instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
			}

			instance.name = entry.Name;
			return instance;
		}

		/// <summary>
		/// Turns the default light into the sun and seats the viewer: a copy of the project's XR rig, or the
		/// default camera as a desktop stand-in when there is no rig to copy. Returns the viewer's head, and hands out
		/// the rig's tracking space (null for the desktop camera).
		/// </summary>
		private static Transform SetUpSunAndViewer(Scene scene, Transform seat, out Transform trackingSpace)
		{
			Camera defaultCamera = null;
			foreach (GameObject rootObject in scene.GetRootGameObjects())
			{
				if (rootObject.TryGetComponent(out Light light))
				{
					// Realtime only: the level objects move, so there is nothing to bake.
					rootObject.name = "Sun";
					light.type = LightType.Directional;
					light.lightmapBakeType = LightmapBakeType.Realtime;
					light.shadows = LightShadows.Soft;
					light.intensity = SunIntensity;
					light.color = SunColor;
					light.transform.rotation = Quaternion.Euler(SunEuler);
				}

				if (rootObject.TryGetComponent(out Camera camera))
				{
					defaultCamera = camera;
				}
			}

			Camera rigCamera = ParkRigBuilder.CopyXRRig(scene, seat.position, false, FarClipPlane, out trackingSpace);
			if (rigCamera != null)
			{
				if (defaultCamera != null)
				{
					Object.DestroyImmediate(defaultCamera.gameObject);
				}

				return rigCamera.transform;
			}

			if (defaultCamera == null)
			{
				return null;
			}

			Debug.LogWarning($"[Parks] No XR rig found in {ParkRigBuilder.MainScenePath}, so the park has a desktop camera instead.");
			defaultCamera.gameObject.name = "Main Camera (desktop preview)";
			defaultCamera.transform.SetPositionAndRotation(seat.position + Vector3.up * EyeHeight, Quaternion.Euler(CameraEuler));
			defaultCamera.nearClipPlane = 0.03f;
			defaultCamera.farClipPlane = FarClipPlane;
			return defaultCamera.transform;
		}

		/// <summary>
		/// The five level miniatures on the board's model root, and the preview that shows the right one and turns the
		/// view cone. Null when the board has no model root.
		/// </summary>
		private static ParkModelPreview AddMiniatures(GameObject levelModels, ParkBoardBuilder.Board board,
			Transform parkRoot, Transform head)
		{
			if (board.ModelRoot == null)
			{
				return null;
			}

			GameObject[] miniatures = AttachMiniatures(levelModels, board.ModelRoot);
			ParkModelPreview preview = board.ModelRoot.gameObject.AddComponent<ParkModelPreview>();
			preview.Configure(miniatures, board.ViewCone, parkRoot, head);
			preview.ShowLevel(StartLevel);
			return preview;
		}

		private static GameObject[] AttachMiniatures(GameObject levelModels, Transform modelRoot)
		{
			GameObject[] miniatures = new GameObject[ParkLevels.Count];
			GameObject container = (GameObject)PrefabUtility.InstantiatePrefab(levelModels);
			PrefabUtility.UnpackPrefabInstance(container, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			for (int level = 0; level < ParkLevels.Count; level++)
			{
				Transform miniature = ParkAssetUtility.FindDeep(container.transform, ModelLevelPrefix + level);
				if (miniature == null)
				{
					Debug.LogError($"[Parks] {ModelName}.fbx has no {ModelLevelPrefix}{level}.");
					continue;
				}

				// The model root carries the 1:119 scale; the miniatures are authored in park metres.
				miniature.SetParent(modelRoot, false);
				miniature.localPosition = Vector3.zero;
				miniature.localRotation = Quaternion.identity;
				miniature.localScale = Vector3.one;
				miniatures[level] = miniature.gameObject;
			}

			Object.DestroyImmediate(container);
			return miniatures;
		}

		private static ParkAmbience BuildAmbience(Transform parent)
		{
			GameObject ambienceObject = new GameObject("Ambience");
			ambienceObject.transform.SetParent(parent, false);
			AudioClip natureClip = LoadAmbienceClip(NatureClipFileName);
			AudioClip efficiencyClip = LoadAmbienceClip(EfficiencyClipFileName);
			AudioSource nature = CreateLoopSource("Ambience_Nature", ambienceObject.transform, natureClip);
			AudioSource efficiency = CreateLoopSource("Ambience_Efficiency", ambienceObject.transform, efficiencyClip);
			ParkAmbience ambience = ambienceObject.AddComponent<ParkAmbience>();
			ambience.Configure(nature, efficiency);
			return ambience;
		}

		private static AudioClip LoadAmbienceClip(string fileName)
		{
			string path = $"{ParkAssetUtility.AudioFolder}/{fileName}";
			AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
			if (clip == null)
			{
				Debug.LogWarning($"[Parks] No ambience loop at {path}, so that ambience source stays silent.");
			}

			return clip;
		}

		private static AudioSource CreateLoopSource(string name, Transform parent, AudioClip clip)
		{
			GameObject sourceObject = new GameObject(name);
			sourceObject.transform.SetParent(parent, false);
			AudioSource source = sourceObject.AddComponent<AudioSource>();
			source.clip = clip;
			source.loop = true;
			source.playOnAwake = false;
			source.spatialBlend = 0f;
			source.volume = 0f;
			return source;
		}

		#endregion
	}
}
