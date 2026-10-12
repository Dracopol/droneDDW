using System.Collections.Generic;
using DroneDDW.Admin;
using DroneDDW.Admin.Editor;
using DroneDDW.Admin.Sessions;
using DroneDDW.Characters;
using DroneDDW.Characters.Editor;
using DroneDDW.FingerGrab.Editor;
using DroneDDW.NarratorOrb;
using DroneDDW.Neighborhood.Editor.Building;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Table;
using DroneDDW.TicketScan;
using Meta.XR;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

namespace DroneDDW.Experience.Editor
{
	// Menu: DroneDDW > Experience > Build Experience Scene. Builds Scenes/Experience/Experience.unity, the scene a
	// participant's session runs in from check-in to goodbye (see ExperienceDirector), and sets the build's scene list
	// to it and the Neighborhood (which it loads on top of itself for the VR part):
	// - a copy of the main scene's XR rig and passthrough layer at the Neighborhood's seat, so the practice table stands
	//   where the street's table will, with the Interaction Tests hand visuals and the finger grab
	// - Session Control with check-in on, the Player Spot at the seat facing the table, the admin menu, and the ticket
	//   scanner the menu's check-in screen switches on (Passthrough Camera Access, off otherwise)
	// - the Narrator Orb above the table, the practice table (the Neighborhood's table, slider and lock-in button, no
	//   miniatures), the three portrait cards behind it, the Questionnaire's ghost hands (with the ported hand check),
	//   the notice panel, the view fade, a light for passthrough
	// - the director with its steps, headset presence (A / X, or Space, stand in for the sensor), the environment
	//   switch and the debug keys (N skips)
	// Everything said comes from Content/Data/Experience/Experience Settings.asset, made once with the placeholder line
	// everywhere and never overwritten. Run the Admin Menu, Narrator Orb, Characters and Neighborhood builders first.
	// Rebuilding replaces the scene.
	internal static class ExperienceSceneBuilder
	{
		#region Fields

		internal const string ScenePath = "Assets/Scenes/Experience/Experience.unity";
		internal const string SettingsPath = "Assets/Content/Data/Experience/Experience Settings.asset";
		internal const string NeighborhoodScenePath = "Assets/Scenes/Neighborhood/Neighborhood.unity";

		private const string MenuPath = "DroneDDW/Experience/Build Experience Scene";
		private const string DialogTitle = "Experience scene";
		private const string MainScenePath = "Assets/Scenes/Questionnaire.unity";
		private const string OrbPrefabPath = "Assets/Content/Prefabs/Narrator Orb/Narrator Orb.prefab";
		private const string PlaceholderLinePath = "Assets/Content/Audio/Voice/Narrator Orb/Narrator_TestSample.wav";
		private const string GhostMaterialPath = "Assets/Content/Materials/Ghost Hand.mat";
		private const string LeftGhostModelPath = "Assets/Samples/XR Hands/1.7.3/HandVisualizer/Models/LeftHand.fbx";
		private const string RightGhostModelPath = "Assets/Samples/XR Hands/1.7.3/HandVisualizer/Models/RightHand.fbx";
		private const string LockInClipPath = "Assets/Content/Audio/SFX/Lock In.wav";
		private const string FadeMaterialPath = "Assets/Content/Materials/Experience/View Fade.mat";
		private const string RegularFontPath = "Assets/Content/Fonts/Inter/Inter Regular SDF.asset";
		private const string NeighborhoodLayoutFile = "Street_Layout.json";
		private const string GhostHandsName = "Ghost Hands";
		private const string LeftHandVisualName = "LeftHandQuestVisual";
		private const string HandVisualizerName = "Hand Visualizer";

		// The fade draws over the world and the pause dimmer (3500), under the admin menu (3600)
		private const int FadeRenderQueue = 3550;
		private const float FadeDistanceMeters = 0.08f;

		// The notice panel: one unit is a millimetre
		private const float NoticeWidth = 600f;
		private const float NoticeHeight = 150f;
		private const float NoticeFontSize = 30f;
		private const float NoticePadding = 24f;
		private const float MetersPerMillimeter = 0.001f;

		// The cards stand in an arc behind the table, a little above the eyes
		private const float CardDistanceMeters = 1.5f;
		private const float CardSpreadDegrees = 30f;
		private const float CardHeightAboveEyesMeters = 0.2f;

		// Used when the Neighborhood's layout can't be read
		private const float DefaultSeatHeight = 0.12f;
		private const float DefaultEyeHeight = 1.2f;

		// Where the orb floats, from the seated eyes (the Neighborhood's eye height above its seat, as its camera)
		private static readonly Vector3 OrbHomeOffset = new(0f, -0.05f, 1f);
		private static readonly Vector3 OrbCharactersOffset = new(-0.5f, -0.22f, 0.8f);
		private static readonly Vector3 OrbNeighborhoodOffset = new(-0.6f, 0.25f, 0.9f);

		private static readonly Color NoticeBackground = new(0.07f, 0.08f, 0.1f, 0.92f);

		// The camera at its largest square size; it takes the next smaller size it has otherwise
		private static readonly Vector2Int CameraResolution = new(1280, 1280);

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Build()
		{
			GameObject menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdminMenuPrefabBuilder.PrefabPath);
			AdminMenuSettings adminSettings =
				AssetDatabase.LoadAssetAtPath<AdminMenuSettings>(AdminMenuAssetBuilder.SettingsPath);
			GameObject orbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefabPath);
			AudioClip placeholder = AssetDatabase.LoadAssetAtPath<AudioClip>(PlaceholderLinePath);
			CharacterPortraitSettings cardSettings =
				AssetDatabase.LoadAssetAtPath<CharacterPortraitSettings>(CharacterPortraitBuilder.SettingsPath);
			Sprite cardPanel = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterPortraitBuilder.PanelSpritePath);
			CharacterPortraitSet[] portraitSets = CharacterPortraitTestSceneBuilder.LoadPortraitSets();
			TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
			if (menuPrefab == null || adminSettings == null || orbPrefab == null || placeholder == null
				|| cardSettings == null || cardPanel == null || portraitSets == null || font == null)
			{
				Debug.LogError("[Experience] The scene needs the admin menu, the Narrator Orb, the portrait cards and the "
					+ "placeholder line: run DroneDDW > Admin Menu > Build Admin Menu Assets, Narrator Orb > Build Orb "
					+ "Assets and Characters > Build Portrait Assets first.");
				return;
			}

			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NeighborhoodScenePath) == null)
			{
				Debug.LogError($"[Experience] {NeighborhoodScenePath} is missing: run DroneDDW > Neighborhood > Build "
					+ "Neighborhood Scene first.");
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

			ExperienceSettings settings = LoadOrCreateSettings(placeholder, portraitSets.Length);
			ReadSeat(out float seatHeight, out float eyeHeight);
			Vector3 eyes = new(0f, seatHeight + eyeHeight, 0f);

			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			XROrigin origin = CopyMainSceneParts(scene, new Vector3(0f, seatHeight, 0f), out OVRPassthroughLayer passthrough,
				out GameObject questionnaireGhostHands);
			if (origin == null)
			{
				Debug.LogError($"[Experience] No XR rig found in {MainScenePath}; the scene needs it.");
				return;
			}

			FingerGrabRigSetup.AddToScene(scene);
			Camera headCamera = origin.Camera;
			Transform head = headCamera.transform;
			Transform trackingSpace = origin.CameraFloorOffsetObject != null
				? origin.CameraFloorOffsetObject.transform
				: head.parent;

			ExperienceProp light = CreateLight();
			SessionControl session = CreateSession(origin, adminSettings);
			TicketScanner scanner = CreateTicketScanner();
			PlaceAdminMenu(menuPrefab, scene, session, head, scanner);

			NarratorOrbController orb = PlaceOrb(orbPrefab, scene, head, eyes + OrbHomeOffset, out ExperienceProp orbProp);
			NeighborhoodTableParts table = NeighborhoodTableBuilder.BuildPractice(head, trackingSpace);
			if (table.Root == null)
			{
				Debug.LogError("[Experience] The practice table couldn't be built (see above).");
				return;
			}

			ExperienceProp tableProp = table.Root.AddComponent<ExperienceProp>();
			CharacterCard[] cards = CreateCards(portraitSets, cardSettings, cardPanel, eyes, out ExperienceProp cardsProp);
			NoticePanel notice = CreateNotice(head, font);
			ViewFade fade = CreateFade(head);

			GameObject experience = new("Experience");
			Transform anchors = new GameObject("Orb Spots").transform;
			anchors.SetParent(experience.transform, false);
			Transform orbHome = CreateAnchor("Home", anchors, eyes + OrbHomeOffset);
			Transform orbCharacters = CreateAnchor("While The Characters Speak", anchors, eyes + OrbCharactersOffset);
			Transform orbNeighborhood = CreateAnchor("In The Neighborhood", anchors, eyes + OrbNeighborhoodOffset);

			HeadsetPresence presence = experience.AddComponent<HeadsetPresence>();
			presence.Configure(CreateButtonAction("Headset Fallback", "<Keyboard>/space", "PrimaryButton"));
			EnvironmentSwitch environment = experience.AddComponent<EnvironmentSwitch>();
			environment.Configure(fade, passthrough, headCamera, trackingSpace, new[] { tableProp, cardsProp },
				new[] { light });

			HandAlignmentCondition handCheck = CreateHandCheck(experience.transform, head, trackingSpace,
				questionnaireGhostHands);
			PracticeChoice practice = experience.AddComponent<PracticeChoice>();
			practice.Configure(tableProp, table.Slider, table.LockButton, table.SliderDriver as NeighborhoodSliderHandDriver,
				table.Hint, orb);

			ExperienceDirector director = experience.AddComponent<ExperienceDirector>();
			director.Configure(settings, session, presence, environment, notice);
			director.ConfigureNarrator(orb, orbProp, orbHome, orbNeighborhood, orbCharacters);
			director.ConfigureSteps(handCheck, practice, cardsProp, cards);
			experience.AddComponent<ExperienceDebugKeys>().Configure(director);

			// The session's props start hidden; the director shows each in its part
			orbProp.Hide();
			tableProp.Hide();
			cardsProp.Hide();

			EnsureFolder(ScenePath.Substring(0, ScenePath.LastIndexOf('/')));
			EditorSceneManager.SaveScene(scene, ScenePath);
			EditorBuildSettings.scenes = new[]
			{
				new EditorBuildSettingsScene(ScenePath, true),
				new EditorBuildSettingsScene(NeighborhoodScenePath, true),
			};
			Debug.Log($"[Experience] Built {ScenePath}; the build's scenes are now Experience and Neighborhood.");
		}

		// The settings asset, made once with the placeholder line everywhere; never overwritten after
		private static ExperienceSettings LoadOrCreateSettings(AudioClip placeholder, int characterCount)
		{
			ExperienceSettings settings = AssetDatabase.LoadAssetAtPath<ExperienceSettings>(SettingsPath);
			if (settings != null)
			{
				return settings;
			}

			EnsureFolder(SettingsPath.Substring(0, SettingsPath.LastIndexOf('/')));
			settings = ScriptableObject.CreateInstance<ExperienceSettings>();
			settings.UsePlaceholder(placeholder, characterCount);
			AssetDatabase.CreateAsset(settings, SettingsPath);
			AssetDatabase.SaveAssets();
			Debug.Log($"[Experience] Made {SettingsPath} with {placeholder.name} for every line.");
			return settings;
		}

		// The Neighborhood's seat and eye heights from its layout, so the rig sits where the street's rig does
		internal static void ReadSeat(out float seatHeight, out float eyeHeight)
		{
			seatHeight = DefaultSeatHeight;
			eyeHeight = DefaultEyeHeight;
			string root = NeighborhoodAssetUtility.FindRootFolder();
			TextAsset json = string.IsNullOrEmpty(root)
				? null
				: AssetDatabase.LoadAssetAtPath<TextAsset>(
					$"{root}/{NeighborhoodAssetUtility.LayoutsFolder}/{NeighborhoodLayoutFile}");
			if (json == null)
			{
				Debug.LogWarning("[Experience] Couldn't read the Neighborhood's layout; using the default seat height.");
				return;
			}

			NeighborhoodLayout layout = JsonUtility.FromJson<NeighborhoodLayout>(json.text);
			seatHeight = layout.SeatHeight;
			eyeHeight = layout.EyeHeight;
		}

		// The main scene's XR rig (at the seat, facing +Z) and its passthrough root, with the Interaction Tests hand
		// visuals; also a copy of the Questionnaire's ghost hands, when it has them. Copies: rebuild after changing them
		// there.
		internal static XROrigin CopyMainSceneParts(Scene scene, Vector3 seat, out OVRPassthroughLayer passthrough,
			out GameObject ghostHands)
		{
			passthrough = null;
			ghostHands = null;
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
					if (isRig || isPassthrough)
					{
						GameObject copy = CopyToScene(rootObject, scene);
						if (isRig)
						{
							copy.transform.SetPositionAndRotation(seat, Quaternion.identity);
							rig = copy.GetComponent<XROrigin>();
							SetActiveInRig(copy.transform, LeftHandVisualName, true);
							SetActiveInRig(copy.transform, HandVisualizerName, false);
						}
						else
						{
							passthrough = copy.GetComponentInChildren<OVRPassthroughLayer>(true);
						}
					}

					Transform ghosts = FindDeep(rootObject.transform, GhostHandsName);
					if (ghosts != null && ghostHands == null)
					{
						ghostHands = CopyToScene(ghosts.gameObject, scene);
					}
				}

				return rig;
			}
			finally
			{
				EditorSceneManager.CloseScene(source, true);
			}
		}

		private static GameObject CopyToScene(GameObject original, Scene scene)
		{
			GameObject copy = Object.Instantiate(original);
			copy.name = original.name;
			copy.transform.SetParent(null, true);
			SceneManager.MoveGameObjectToScene(copy, scene);
			return copy;
		}

		private static ExperienceProp CreateLight()
		{
			GameObject lightObject = new("Passthrough Light");
			Light light = lightObject.AddComponent<Light>();
			light.type = LightType.Directional;
			lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
			return lightObject.AddComponent<ExperienceProp>();
		}

		// Check-in on; the Player Spot at the seat, facing the table
		private static SessionControl CreateSession(XROrigin origin, AdminMenuSettings adminSettings)
		{
			PlayerSpot spot = new GameObject("Player Spot").AddComponent<PlayerSpot>();
			SessionControl session = new GameObject("Session Control").AddComponent<SessionControl>();
			session.Configure(origin, spot);
			session.ConfigureSessions(adminSettings, true);
			return session;
		}

		// Passthrough Camera Access on the left camera, off until the admin menu's check-in switches it on
		private static TicketScanner CreateTicketScanner()
		{
			GameObject scannerObject = new("Ticket Scanner");
			PassthroughCameraAccess cameraAccess = scannerObject.AddComponent<PassthroughCameraAccess>();
			cameraAccess.CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
			cameraAccess.RequestedResolution = CameraResolution;
			cameraAccess.enabled = false;
			CameraTicketScanner scanner = scannerObject.AddComponent<CameraTicketScanner>();
			scanner.Configure(cameraAccess);
			return scanner;
		}

		private static void PlaceAdminMenu(GameObject menuPrefab, Scene scene, SessionControl session, Transform head,
			TicketScanner scanner)
		{
			AdminMenu menu = ((GameObject)PrefabUtility.InstantiatePrefab(menuPrefab, scene)).GetComponent<AdminMenu>();
			menu.ConfigureScene(session, head);
			menu.ConfigureTicketScanner(scanner);
			menu.ConfigureTestSave(false);
			PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
			PrefabUtility.RecordPrefabInstancePropertyModifications(menu.GetComponent<AdminMenuPlacement>());
			PrefabUtility.RecordPrefabInstancePropertyModifications(menu.Dimmer);
		}

		private static NarratorOrbController PlaceOrb(GameObject orbPrefab, Scene scene, Transform head, Vector3 position,
			out ExperienceProp orbProp)
		{
			GameObject orbObject = (GameObject)PrefabUtility.InstantiatePrefab(orbPrefab, scene);
			orbObject.transform.SetPositionAndRotation(position,
				Quaternion.LookRotation(new Vector3(0f, head.position.y, 0f) - position));
			NarratorOrbController orb = orbObject.GetComponent<NarratorOrbController>();
			orb.ConfigureHead(head);
			PrefabUtility.RecordPrefabInstancePropertyModifications(orb);
			orbProp = orbObject.AddComponent<ExperienceProp>();
			return orb;
		}

		// Grandma, student and worker in an arc behind the table, facing the seat, under one root
		private static CharacterCard[] CreateCards(CharacterPortraitSet[] portraitSets, CharacterPortraitSettings settings,
			Sprite panel, Vector3 eyes, out ExperienceProp cardsProp)
		{
			GameObject root = new("Characters");
			cardsProp = root.AddComponent<ExperienceProp>();
			CharacterCard[] cards = new CharacterCard[portraitSets.Length];
			for (int card = 0; card < cards.Length; card++)
			{
				float angle = (card - (cards.Length - 1) * 0.5f) * CardSpreadDegrees * Mathf.Deg2Rad;
				Vector3 direction = new(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
				Vector3 position = eyes + direction * CardDistanceMeters + Vector3.up * CardHeightAboveEyesMeters;
				Pose pose = new(position, Quaternion.LookRotation(direction, Vector3.up));
				cards[card] = CharacterPortraitTestSceneBuilder.CreateCard(portraitSets[card], settings, panel,
					(float)card / cards.Length, pose, false);
				cards[card].transform.SetParent(root.transform, true);
			}

			return cards;
		}

		// The hand check with the Questionnaire's ghost hands, their tutorial components swapped for the ported ones;
		// fresh XR Hands models when the Questionnaire has none
		private static HandAlignmentCondition CreateHandCheck(Transform parent, Transform head, Transform trackingSpace,
			GameObject questionnaireGhostHands)
		{
			GameObject checkObject = new("Hand Check");
			checkObject.transform.SetParent(parent, false);
			AudioSource audioSource = checkObject.AddComponent<AudioSource>();
			audioSource.playOnAwake = false;
			audioSource.spatialBlend = 0f;

			GhostHand[] ghostHands;
			GameObject ghostRoot;
			if (questionnaireGhostHands != null)
			{
				ghostRoot = questionnaireGhostHands;
				ghostHands = PortGhostHands(ghostRoot);
			}
			else
			{
				ghostRoot = new GameObject(GhostHandsName);
				ghostHands = CreateGhostHands(ghostRoot.transform);
			}

			if (ghostHands.Length == 0)
			{
				Object.DestroyImmediate(ghostRoot);
				ghostRoot = new GameObject(GhostHandsName);
				ghostHands = CreateGhostHands(ghostRoot.transform);
			}

			HandAlignmentCondition handCheck = checkObject.AddComponent<HandAlignmentCondition>();
			handCheck.Configure(head, trackingSpace, ghostRoot.transform, ghostHands, audioSource,
				AssetDatabase.LoadAssetAtPath<AudioClip>(LockInClipPath));
			return handCheck;
		}

		// The tutorial's GhostHand components (in the default assembly, so read by name) become the experience's, with
		// the same hand, palm and renderer; its hint target goes
		private static GhostHand[] PortGhostHands(GameObject ghostRoot)
		{
			List<GhostHand> ghostHands = new();
			foreach (MonoBehaviour component in ghostRoot.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (component == null)
				{
					continue;
				}

				string typeName = component.GetType().Name;
				if (typeName == "HintTarget")
				{
					Object.DestroyImmediate(component);
					continue;
				}

				if (typeName != "GhostHand")
				{
					continue;
				}

				SerializedObject tutorialHand = new(component);
				Handedness handedness = (Handedness)tutorialHand.FindProperty("_handedness").intValue;
				Transform palm = tutorialHand.FindProperty("_palm").objectReferenceValue as Transform;
				Renderer handRenderer = tutorialHand.FindProperty("_renderer").objectReferenceValue as Renderer;
				GameObject host = component.gameObject;
				Object.DestroyImmediate(component);
				if (palm == null || handRenderer == null)
				{
					continue;
				}

				GhostHand ghostHand = host.AddComponent<GhostHand>();
				ghostHand.Configure(handedness, palm, handRenderer);
				ghostHands.Add(ghostHand);
			}

			return ghostHands.ToArray();
		}

		// Both XR Hands models in the ghost material, side by side, palms down
		private static GhostHand[] CreateGhostHands(Transform root)
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
			return new[]
			{
				CreateGhostHand(root, LeftGhostModelPath, Handedness.Left, "L_Palm", -0.1f, material),
				CreateGhostHand(root, RightGhostModelPath, Handedness.Right, "R_Palm", 0.1f, material),
			};
		}

		private static GhostHand CreateGhostHand(Transform root, string modelPath, Handedness handedness, string palmName,
			float x, Material material)
		{
			GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
			model.transform.SetParent(root, false);
			model.transform.localPosition = new Vector3(x, 0f, 0f);
			SkinnedMeshRenderer handRenderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
			handRenderer.sharedMaterial = material;
			GhostHand ghostHand = model.AddComponent<GhostHand>();
			ghostHand.Configure(handedness, FindDeep(model.transform, palmName), handRenderer);
			return ghostHand;
		}

		// A dark rounded-off panel with one line of text, hidden until the director shows it
		private static NoticePanel CreateNotice(Transform head, TMP_FontAsset font)
		{
			GameObject noticeObject = new("Notice", typeof(RectTransform));
			RectTransform noticeRect = (RectTransform)noticeObject.transform;
			Canvas canvas = noticeObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.WorldSpace;
			canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
				| AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
			noticeRect.sizeDelta = new Vector2(NoticeWidth, NoticeHeight);
			noticeRect.localScale = Vector3.one * MetersPerMillimeter;

			Image background = CreateUiPart<Image>("Background", noticeRect);
			Stretch(background.rectTransform, 0f);
			background.color = NoticeBackground;
			background.raycastTarget = false;

			TextMeshProUGUI text = CreateUiPart<TextMeshProUGUI>("Text", noticeRect);
			Stretch(text.rectTransform, NoticePadding);
			text.font = font;
			text.fontSize = NoticeFontSize;
			text.alignment = TextAlignmentOptions.Center;
			text.textWrappingMode = TextWrappingModes.Normal;
			text.color = Color.white;
			text.raycastTarget = false;

			NoticePanel notice = noticeObject.AddComponent<NoticePanel>();
			notice.Configure(head, text);
			noticeObject.SetActive(false);
			return notice;
		}

		// A black quad just in front of the camera, drawn over everything but the admin menu
		private static ViewFade CreateFade(Transform head)
		{
			GameObject fadeObject = new("View Fade");
			fadeObject.transform.SetParent(head, false);
			fadeObject.transform.localPosition = new Vector3(0f, 0f, FadeDistanceMeters);
			fadeObject.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
			MeshRenderer fadeRenderer = fadeObject.AddComponent<MeshRenderer>();
			fadeRenderer.sharedMaterial = LoadOrCreateFadeMaterial();
			fadeRenderer.shadowCastingMode = ShadowCastingMode.Off;
			fadeRenderer.receiveShadows = false;
			fadeRenderer.lightProbeUsage = LightProbeUsage.Off;
			fadeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			fadeRenderer.enabled = false;
			ViewFade fade = fadeObject.AddComponent<ViewFade>();
			fade.Configure(fadeRenderer);
			return fade;
		}

		// URP Unlit, transparent black that writes full alpha (so at black it covers passthrough too), not depth tested
		private static Material LoadOrCreateFadeMaterial()
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
			Material material = AssetDatabase.LoadAssetAtPath<Material>(FadeMaterialPath);
			if (material == null)
			{
				EnsureFolder(FadeMaterialPath.Substring(0, FadeMaterialPath.LastIndexOf('/')));
				material = new Material(shader) { name = "View Fade" };
				AssetDatabase.CreateAsset(material, FadeMaterialPath);
			}

			material.shader = shader;
			material.SetFloat("_Surface", 1f);
			material.SetFloat("_Blend", 0f);
			material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
			material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
			material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
			material.SetFloat("_ZWrite", 0f);
			if (material.HasProperty("_ZTest"))
			{
				material.SetFloat("_ZTest", (float)CompareFunction.Always);
			}

			material.SetColor("_BaseColor", Color.black);
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Transparent");
			material.SetShaderPassEnabled("DepthOnly", false);
			material.SetShaderPassEnabled("SHADOWCASTER", false);
			material.SetShaderPassEnabled("MOTIONVECTORS", false);

			// URP recomputes the queue as Transparent (3000) plus the queue offset whenever it validates the material
			material.SetFloat("_QueueOffset", FadeRenderQueue - (int)RenderQueue.Transparent);
			material.renderQueue = FadeRenderQueue;
			EditorUtility.SetDirty(material);
			return material;
		}

		// A key and the same button on either controller (A / X for the primary one)
		private static InputAction CreateButtonAction(string name, string keyPath, string controllerUsage)
		{
			InputAction action = new(name, InputActionType.Button);
			action.AddBinding(keyPath);
			action.AddBinding($"<XRController>{{LeftHand}}/{{{controllerUsage}}}");
			action.AddBinding($"<XRController>{{RightHand}}/{{{controllerUsage}}}");
			return action;
		}

		private static Transform CreateAnchor(string name, Transform parent, Vector3 position)
		{
			Transform anchor = new GameObject(name).transform;
			anchor.SetParent(parent, false);
			anchor.position = position;
			return anchor;
		}

		private static void SetActiveInRig(Transform rig, string objectName, bool isActive)
		{
			Transform target = FindDeep(rig, objectName);
			if (target == null)
			{
				Debug.LogWarning($"[Experience] The XR rig has no {objectName}, so its hand visuals stay as in the main "
					+ "scene.");
				return;
			}

			target.gameObject.SetActive(isActive);
		}

		private static Transform FindDeep(Transform parent, string name)
		{
			if (parent.name == name)
			{
				return parent;
			}

			foreach (Transform child in parent)
			{
				Transform found = FindDeep(child, name);
				if (found != null)
				{
					return found;
				}
			}

			return null;
		}

		private static T CreateUiPart<T>(string name, Transform parent) where T : Component
		{
			GameObject part = new(name, typeof(RectTransform));
			part.transform.SetParent(parent, false);
			return part.AddComponent<T>();
		}

		private static void Stretch(RectTransform rect, float inset)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = new Vector2(inset, inset);
			rect.offsetMax = new Vector2(-inset, -inset);
		}

		private static void EnsureFolder(string folder)
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
