using System.Collections.Generic;
using DroneDDW.FingerGrab.Editor;
using DroneDDW.Parks.Audio;
using DroneDDW.Parks.Experience;
using DroneDDW.Parks.Levels;
using DroneDDW.Parks.Table;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if PARKS_XRI
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
#if PARKS_XRI_3
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#endif
#endif

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// Menu: DroneDDW / Parks / Build Level Park (5 Levels).
	/// 1. Creates the palette materials (plus the pale maquette one) and reimports every park FBX
	///    and ambience loop.
	/// 2. Saves the modular pieces of Park_Levels_Kit as prefabs (Content/Prefabs/Parks/Park_Levels).
	/// 3. Builds Scenes/Park Levels.unity: one park holding all five levels, every changing object
	///    tagged with the levels it belongs to, a level controller that runs the wave transition,
	///    the seat anchor with a copy of the project's XR rig (taken from the main scene, hands set up
	///    as in the Interaction Tests scenes, grabbing with the finger grab and its shared settings),
	///    the table with the five-detent slider (pushed by any part of a hand, grabbed once the fingers close)
	///    and the five miniatures, two ambience sources playing the Content/Audio/Ambience loops and a realtime sun.
	/// Safe to run again: materials and prefabs keep their GUIDs, the scene is rebuilt.
	/// </summary>
	internal static class ParkLevelSceneBuilder
	{
		#region Entities

		private readonly struct TableParts
		{
			public TableParts(ParkLevelSlider slider, ParkModelPreview preview)
			{
				Slider = slider;
				Preview = preview;
			}

			public ParkLevelSlider Slider { get; }
			public ParkModelPreview Preview { get; }
		}

		#endregion

		#region Fields

		private const string MenuPath = "DroneDDW/Parks/Build Level Park (5 Levels)";
		private const string DialogTitle = "Level park";
		private const string LayoutFileName = "Park_Levels_Layout.json";
		private const string KitName = "Park_Levels_Kit";
		private const string RigName = "Park_Levels_Rig";
		private const string ModelName = "Park_Levels_Model";
		private const string SceneName = "Park Levels";
		private const string ModelLevelPrefix = "Model_Level_";
		private const string XRRigScenePath = "Assets/Scenes/Questionnaire.unity";
		private const string LeftHandVisualName = "LeftHandQuestVisual";
		private const string HandVisualizerName = "Hand Visualizer";
		private const string NatureClipFileName = "Park_Ambience_Nature.ogg";
		private const string EfficiencyClipFileName = "Park_Ambience_Efficiency.ogg";
		private const int StartLevel = 2;
		private const float SeatHeight = 0.12f;
		private const float EyeHeight = 1.2f;
		private const float FarClipPlane = 300f;
		private const float TableDistance = 0.75f;
		private const float SliderHalfTravel = 0.3f;
		private const float SunIntensity = 1.1f;

		private static readonly string[] GroupNames = { "Terrain", "Paths", "Vegetation", "Props", "Structures" };
		private static readonly string[] RigParts = { "Rig_Slider", "Rig_Handle", "Rig_ModelRoot", "Rig_ViewCone" };
		private static readonly Vector3 HandleColliderCenter = new Vector3(0f, 0.03f, 0f);
		private static readonly Vector3 HandleColliderSize = new Vector3(0.08f, 0.07f, 0.08f);
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
				GameObject rig = ParkAssetUtility.LoadModel(RigName);
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

				Transform instanceTransform = instance.transform;
				instanceTransform.localPosition = entry.Position;
				instanceTransform.localRotation = Quaternion.Euler(0f, entry.RotationY, 0f);
				instanceTransform.localScale = entry.Scale;
				if ((entry.LevelMask & ParkLevels.AllMask) == ParkLevels.AllMask)
				{
					// Ground and seat platform never change: static, so they batch.
					GameObjectUtility.SetStaticEditorFlags(instance, FixedObjectFlags);
					continue;
				}

				ParkLevelElement element = instance.AddComponent<ParkLevelElement>();
				element.Configure(entry.LevelMask, (ParkTransitionStyle)entry.Style, entry.LiftDepth);
				elements.Add(element);
			}

			GameObject experience = new GameObject("Park_Experience");
			ParkLevelController controller = experience.AddComponent<ParkLevelController>();
			controller.Configure(elements.ToArray(), seat.transform, StartLevel);
			controller.ShowLevelInEditor(StartLevel);

			TableParts table = BuildTable(rigModel, levelModels, world.transform, head, trackingSpace);
			ParkAmbience ambience = BuildAmbience(experience.transform);
			ParkExperience coordinator = experience.AddComponent<ParkExperience>();
			coordinator.Configure(controller, table.Slider, table.Preview, ambience);
			ParkLevelDebugKeys debugKeys = experience.AddComponent<ParkLevelDebugKeys>();
			debugKeys.Configure(controller);

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

			Camera rigCamera = CopyXRRig(scene, seat, out trackingSpace);
			if (rigCamera != null)
			{
				if (defaultCamera != null)
				{
					Object.DestroyImmediate(defaultCamera.gameObject);
				}

				// Thumb plus any finger grabs the slider, tuned by the shared Finger Grab Settings asset
				int fingerGrabHandCount = FingerGrabRigSetup.AddToScene(scene);
				Debug.Log($"[Parks] Finger grab set up on {fingerGrabHandCount} hands.");
				return rigCamera.transform;
			}

			if (defaultCamera == null)
			{
				return null;
			}

			Debug.LogWarning($"[Parks] No XR rig found in {XRRigScenePath}, so the park has a desktop camera instead.");
			defaultCamera.gameObject.name = "Main Camera (desktop preview)";
			defaultCamera.transform.SetPositionAndRotation(seat.position + Vector3.up * EyeHeight, Quaternion.Euler(CameraEuler));
			defaultCamera.nearClipPlane = 0.03f;
			defaultCamera.farClipPlane = FarClipPlane;
			return defaultCamera.transform;
		}

		/// <summary>
		/// Copies the XR rig (the root with an XR Origin) from the main scene onto the seat, so the park runs on
		/// the same seated, hand-tracked setup. It is a copy: rebuild the park after changing the rig there.
		/// Returns the rig's camera, or null when the main scene or its rig is missing, and hands out the rig's
		/// tracking space: the camera offset its hand joints are relative to.
		/// </summary>
		private static Camera CopyXRRig(Scene scene, Transform seat, out Transform trackingSpace)
		{
			trackingSpace = null;
#if PARKS_XRI
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(XRRigScenePath) == null)
			{
				return null;
			}

			Scene source = EditorSceneManager.OpenScene(XRRigScenePath, OpenSceneMode.Additive);
			try
			{
				foreach (GameObject rootObject in source.GetRootGameObjects())
				{
					if (!rootObject.TryGetComponent(out XROrigin sourceOrigin) || sourceOrigin.Camera == null)
					{
						continue;
					}

					GameObject rig = Object.Instantiate(rootObject);
					rig.name = rootObject.name;
					SceneManager.MoveGameObjectToScene(rig, scene);
					rig.transform.SetPositionAndRotation(seat.position, seat.rotation);
					UseInteractionTestHands(rig.transform);

					// The main scene's camera clears to transparent black for passthrough; the park needs its sky.
					XROrigin origin = rig.GetComponent<XROrigin>();
					trackingSpace = origin.CameraFloorOffsetObject.transform;
					Camera camera = origin.Camera;
					camera.clearFlags = CameraClearFlags.Skybox;
					camera.farClipPlane = Mathf.Max(camera.farClipPlane, FarClipPlane);
					return camera;
				}

				return null;
			}
			finally
			{
				EditorSceneManager.CloseScene(source, true);
			}
#else
			return null;
#endif
		}

		/// <summary>
		/// Matches the hand visuals of the Interaction Tests scenes: both Quest hand meshes on, the XR Hands
		/// sample visualizer off. The main scene currently hides the left mesh and shows the visualizer instead.
		/// </summary>
		private static void UseInteractionTestHands(Transform rig)
		{
			SetActiveInRig(rig, LeftHandVisualName, true);
			SetActiveInRig(rig, HandVisualizerName, false);
		}

		private static void SetActiveInRig(Transform rig, string objectName, bool isActive)
		{
			Transform target = FindDeep(rig, objectName);
			if (target == null)
			{
				Debug.LogWarning($"[Parks] The XR rig has no {objectName}, so its hand visuals stay as in the main scene.");
				return;
			}

			target.gameObject.SetActive(isActive);
		}

		private static TableParts BuildTable(GameObject rigModel, GameObject levelModels, Transform parkRoot,
			Transform head, Transform trackingSpace)
		{
			GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(rigModel);
			PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			rig.name = "Park_Table";
			rig.transform.SetPositionAndRotation(new Vector3(0f, SeatHeight, TableDistance), Quaternion.identity);

			Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
			foreach (string partName in RigParts)
			{
				Transform part = FindDeep(rig.transform, partName);
				if (part == null)
				{
					Debug.LogError($"[Parks] {RigName}.fbx has no {partName}; the table is incomplete.");
					return new TableParts(null, null);
				}

				parts.Add(partName, part);
			}

			Transform slider = parts["Rig_Slider"];
			Transform handle = parts["Rig_Handle"];
			Transform modelRoot = parts["Rig_ModelRoot"];
			GameObject[] miniatures = AttachMiniatures(levelModels, modelRoot);

			ParkModelPreview preview = modelRoot.gameObject.AddComponent<ParkModelPreview>();
			preview.Configure(miniatures, parts["Rig_ViewCone"], parkRoot, head);
			preview.ShowLevel(StartLevel);

			ParkLevelSlider levelSlider = slider.gameObject.AddComponent<ParkLevelSlider>();
			levelSlider.Configure(handle, -SliderHalfTravel, SliderHalfTravel);
			Vector3 handlePosition = handle.localPosition;
			handlePosition.x = Mathf.Lerp(-SliderHalfTravel, SliderHalfTravel, ParkLevels.ToBlend(StartLevel));
			handle.localPosition = handlePosition;
			if (handle.TryGetComponent(out BoxCollider handleCollider))
			{
				// Bigger than the knob, so it is easy to grab. The hands push against the knob itself.
				handleCollider.center = HandleColliderCenter;
				handleCollider.size = HandleColliderSize;
			}

			AddHandInput(levelSlider, handle.gameObject, trackingSpace);
			return new TableParts(levelSlider, preview);
		}

		private static GameObject[] AttachMiniatures(GameObject levelModels, Transform modelRoot)
		{
			GameObject[] miniatures = new GameObject[ParkLevels.Count];
			GameObject container = (GameObject)PrefabUtility.InstantiatePrefab(levelModels);
			PrefabUtility.UnpackPrefabInstance(container, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			for (int level = 0; level < ParkLevels.Count; level++)
			{
				Transform miniature = FindDeep(container.transform, ModelLevelPrefix + level);
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

		/// <summary>
		/// Hands push the handle by default and grab it once the fingers close: the interactable is what the finger
		/// grab selects, the hand bones are what pushes, and the driver combines both into one drag.
		/// </summary>
		private static void AddHandInput(ParkLevelSlider slider, GameObject handle, Transform trackingSpace)
		{
#if PARKS_XRI
			if (!handle.TryGetComponent(out MeshFilter handleMesh))
			{
				Debug.LogError($"[Parks] {RigName}.fbx's Rig_Handle has no mesh, so the hands can't push it.");
			}

			XRSimpleInteractable interactable = handle.AddComponent<XRSimpleInteractable>();
			ParkHandBones handBones = slider.gameObject.AddComponent<ParkHandBones>();
			handBones.Configure(trackingSpace);
			ParkSliderHandDriver driver = slider.gameObject.AddComponent<ParkSliderHandDriver>();
			driver.Configure(slider, interactable, handleMesh, handBones);
#else
			Debug.LogWarning("[Parks] XR Interaction Toolkit is not installed, so the slider has no XR input yet. "
				+ "Install it and run this menu again, or call ParkLevelSlider.BeginDrag / DragTo / EndDrag from your own input.");
#endif
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

		/// <summary>Editor-time lookup of a node by name (the Blender build script's nodes, the rig's hand visuals).</summary>
		private static Transform FindDeep(Transform parent, string name)
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

		#endregion
	}
}
