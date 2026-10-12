using DroneDDW.Admin.Sessions;
using DroneDDW.Admin.TestScene;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Admin.Editor
{
	// Menu: DroneDDW > Admin Menu > Create Admin Menu Test Scene, which builds Scenes/Admin/Admin Menu Test.unity:
	// - a copy of the main scene's XR rig and passthrough layer, so it starts in passthrough like the tutorial
	// - Session Control and a Player Spot at the origin facing +Z, marked by a floor ring and an arrow
	// - the admin menu prefab, wired to the rig's camera and the session control, with its test save button shown
	// - a demo session in front of the spot: a clock and a spinning cube on scaled time and a quiet looping hum, so
	//   pausing, resuming and resetting show
	// Run Build Admin Menu Assets first. Rebuilding replaces the scene.
	internal static class AdminMenuTestSceneBuilder
	{
		#region Fields

		internal const string ScenePath = "Assets/Scenes/Admin/Admin Menu Test.unity";

		private const string MenuPath = "DroneDDW/Admin Menu/Create Admin Menu Test Scene";
		private const string DialogTitle = "Admin menu test scene";
		private const string MainScenePath = "Assets/Scenes/Questionnaire.unity";
		private const string HumClipPath = "Assets/Content/Audio/SFX/Drone Hum.wav";
		private const string MarkerMaterialPath = "Assets/Content/Materials/White.mat";
		private const string SpinnerMaterialPath = "Assets/Content/Materials/Yellow.mat";

		private const float SeatedEyeHeight = 1.2f;
		private const float DemoDistanceMeters = 1.4f;
		private const float SpinnerSizeMeters = 0.15f;
		private const float HumVolume = 0.15f;

		// The clock's canvas: one unit is a millimetre
		private const float ClockWidth = 700f;
		private const float ClockHeight = 160f;
		private const float ClockFontSize = 44f;
		private const float ClockHeightAboveSpinner = 0.2f;

		private const float RingDiameterMeters = 0.6f;
		private const float MarkerThicknessMeters = 0.004f;
		private const float ArrowLengthMeters = 0.35f;
		private const float ArrowWidthMeters = 0.04f;

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Create()
		{
			GameObject menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdminMenuPrefabBuilder.PrefabPath);
			TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AdminMenuAssetBuilder.RegularFontPath);
			if (menuPrefab == null || font == null)
			{
				Debug.LogError("[Admin] The test scene needs the admin menu prefab and its fonts: run DroneDDW > Admin "
					+ "Menu > Build Admin Menu Assets first.");
				return;
			}

			bool isReplacing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
			if (!Application.isBatchMode && isReplacing
				&& !EditorUtility.DisplayDialog(DialogTitle, $"Replace {ScenePath}?", "Replace", "Cancel"))
			{
				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			XROrigin origin = CopyMainSceneRig(scene);
			Transform head = origin != null ? origin.Camera.transform : null;
			if (origin == null)
			{
				Debug.LogWarning($"[Admin] No XR rig found in {MainScenePath}: the menu has no head to follow and "
					+ "Reset player location won't work.");
			}

			CreateLight();
			PlayerSpot spot = CreatePlayerSpot();
			SessionControl sessionControl = new GameObject("Session Control").AddComponent<SessionControl>();
			sessionControl.Configure(origin, spot);

			// No check-in: the demo session runs from the start, without a participant
			sessionControl.ConfigureSessions(
				AssetDatabase.LoadAssetAtPath<AdminMenuSettings>(AdminMenuAssetBuilder.SettingsPath), false);

			AdminMenu menu = ((GameObject)PrefabUtility.InstantiatePrefab(menuPrefab, scene))
				.GetComponent<AdminMenu>();
			menu.ConfigureScene(sessionControl, head);

			// The test scene shows the test save button, to try saving and deleting rows on the headset
			menu.ConfigureTestSave(true);
			PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
			PrefabUtility.RecordPrefabInstancePropertyModifications(menu.GetComponent<AdminMenuPlacement>());
			PrefabUtility.RecordPrefabInstancePropertyModifications(menu.Dimmer);

			CreateDemoSession(spot, font);

			AdminMenuAssetBuilder.EnsureFolder(ScenePath.Substring(0, ScenePath.LastIndexOf('/')));
			EditorSceneManager.SaveScene(scene, ScenePath);
			Debug.Log($"[Admin] Built {ScenePath}.");
		}

		// Copies the main scene's XR rig (the root with an XR Origin) to the origin, and the root holding its
		// passthrough layer. It is a copy: rebuild the scene after changing the rig there.
		private static XROrigin CopyMainSceneRig(Scene scene)
		{
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
			{
				return null;
			}

			Scene source = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
			try
			{
				XROrigin rig = null;
				foreach (GameObject rootObject in source.GetRootGameObjects())
				{
					bool isRig = rootObject.TryGetComponent(out XROrigin origin) && origin.Camera != null;
					bool isPassthrough = rootObject.GetComponentInChildren<OVRPassthroughLayer>(true) != null;
					if (!isRig && !isPassthrough)
					{
						continue;
					}

					GameObject copy = Object.Instantiate(rootObject);
					copy.name = rootObject.name;
					SceneManager.MoveGameObjectToScene(copy, scene);
					if (isRig)
					{
						copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
						rig = copy.GetComponent<XROrigin>();
					}
				}

				return rig;
			}
			finally
			{
				EditorSceneManager.CloseScene(source, true);
			}
		}

		private static void CreateLight()
		{
			Light light = new GameObject("Directional Light").AddComponent<Light>();
			light.type = LightType.Directional;
			light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
		}

		// At the origin facing +Z: a flat ring on the floor with an arrow pointing the way the player should face
		private static PlayerSpot CreatePlayerSpot()
		{
			PlayerSpot spot = new GameObject("Player Spot").AddComponent<PlayerSpot>();
			Material marker = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);

			GameObject ring = CreatePrimitive(PrimitiveType.Cylinder, "Floor Ring", spot.transform, marker);
			ring.transform.localScale = new Vector3(RingDiameterMeters, MarkerThicknessMeters * 0.5f,
				RingDiameterMeters);
			ring.transform.localPosition = new Vector3(0f, MarkerThicknessMeters * 0.5f, 0f);

			GameObject arrow = CreatePrimitive(PrimitiveType.Cube, "Forward Arrow", spot.transform, marker);
			arrow.transform.localScale = new Vector3(ArrowWidthMeters, MarkerThicknessMeters * 2f, ArrowLengthMeters);
			arrow.transform.localPosition = new Vector3(0f, MarkerThicknessMeters,
				RingDiameterMeters * 0.5f + ArrowLengthMeters * 0.5f);
			return spot;
		}

		// In front of the Player Spot at seated eye height, facing it
		private static void CreateDemoSession(PlayerSpot spot, TMP_FontAsset font)
		{
			Vector3 forward = spot.Forward;
			GameObject demo = new("Demo Session");
			demo.transform.SetPositionAndRotation(
				spot.Position + forward * DemoDistanceMeters + Vector3.up * SeatedEyeHeight,
				Quaternion.LookRotation(forward, Vector3.up));

			AudioSource hum = demo.AddComponent<AudioSource>();
			hum.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(HumClipPath);
			hum.loop = true;
			hum.playOnAwake = true;
			hum.volume = HumVolume;
			hum.spatialBlend = 1f;
			hum.dopplerLevel = 0f;

			Material spinnerMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpinnerMaterialPath);
			GameObject spinner = CreatePrimitive(PrimitiveType.Cube, "Spinner", demo.transform, spinnerMaterial);
			spinner.transform.localScale = Vector3.one * SpinnerSizeMeters;
			spinner.transform.localRotation = Quaternion.Euler(20f, 30f, 0f);

			GameObject clockObject = new("Clock", typeof(RectTransform));
			RectTransform clockCanvas = (RectTransform)clockObject.transform;
			clockCanvas.SetParent(demo.transform, false);
			Canvas canvas = clockObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.WorldSpace;
			canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
				| AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
			clockCanvas.sizeDelta = new Vector2(ClockWidth, ClockHeight);
			clockCanvas.localPosition = new Vector3(0f, ClockHeightAboveSpinner, 0f);
			clockCanvas.localScale = Vector3.one * 0.001f;

			GameObject labelObject = new("Label", typeof(RectTransform));
			RectTransform labelRect = (RectTransform)labelObject.transform;
			labelRect.SetParent(clockCanvas, false);
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.sizeDelta = Vector2.zero;
			TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
			label.font = font;
			label.fontSize = ClockFontSize;
			label.alignment = TextAlignmentOptions.Center;
			label.color = Color.white;
			label.raycastTarget = false;
			label.text = "Session running  0:00";

			demo.AddComponent<AdminMenuDemoSession>().Configure(label, spinner.transform);
		}

		private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material material)
		{
			GameObject primitive = GameObject.CreatePrimitive(type);
			primitive.name = name;
			primitive.transform.SetParent(parent, false);
			Object.DestroyImmediate(primitive.GetComponent<Collider>());
			if (material != null)
			{
				primitive.GetComponent<MeshRenderer>().sharedMaterial = material;
			}

			return primitive;
		}

		#endregion
	}
}
