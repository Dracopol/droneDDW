using System.Collections.Generic;
using DroneDDW.Admin;
using DroneDDW.Admin.Editor;
using DroneDDW.Admin.Sessions;
using DroneDDW.Characters;
using DroneDDW.Characters.Editor;
using DroneDDW.FingerGrab.Editor;
using DroneDDW.NarratorOrb;
using DroneDDW.Neighborhood.Characters;
using DroneDDW.Neighborhood.Editor.Building;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Table;
using DroneDDW.Neighborhood.Voice;
using DroneDDW.TicketScan;
using DroneDDW.Voice;
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
	// - Session Control with check-in on, the Player Spot at the seat facing the table and setting the eye height, the
	//   admin menu, and the ticket scanner the menu's check-in screen switches on (Passthrough Camera Access, off
	//   otherwise)
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
		internal const string OrbPrefabPath = "Assets/Content/Prefabs/Narrator Orb/Narrator Orb.prefab";
		private const string PlaceholderLinePath = "Assets/Content/Audio/Voice/Narrator Orb/Narrator_TestSample.wav";
		private const string GhostMaterialPath = "Assets/Content/Materials/Ghost Hand.mat";
		private const string LeftGhostModelPath = "Assets/Samples/XR Hands/1.7.3/HandVisualizer/Models/LeftHand.fbx";
		private const string RightGhostModelPath = "Assets/Samples/XR Hands/1.7.3/HandVisualizer/Models/RightHand.fbx";
		private const string LockInClipPath = "Assets/Content/Audio/SFX/Lock In.wav";
		private const string FadeMaterialPath = "Assets/Content/Materials/Experience/View Fade.mat";
		private const string SubtitlesPath = "Assets/Content/Data/Experience/Experience Subtitles.asset";
		private const string SubtitleTextMaterialPath = "Assets/Content/Materials/Experience/Subtitle Text.mat";
		private const string SubtitleBackgroundMaterialPath = "Assets/Content/Materials/Experience/Subtitle Background.mat";
		private const string SubtitleBackgroundShaderPath = "Assets/Content/Shaders/Experience/Subtitle Background.shader";
		private const string SubtitleTextShaderPath = "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader";
		private const string VoiceLibraryPath = "Assets/Content/Neighborhood/Voice/Neighborhood_VoiceLibrary.asset";

		// The subtitles: a wider, lower strip than the notices, in metres. Plain 3D text and a quad rather than a canvas
		// (a canvas forces depth testing), with materials that ignore depth and draw after everything else, the orb
		// included, but before the screen fade (3550) and the admin menu (3600)
		private const float SubtitleWidthMeters = 0.9f;
		private const float SubtitleHeightMeters = 0.14f;
		private const float SubtitlePaddingMeters = 0.018f;
		private const float SubtitleFontSize = 0.28f;
		private const float SubtitleDistanceMeters = 1f;
		private const float SubtitleDropMeters = 0.32f;
		private const int SubtitleRenderQueue = 3500;

		// The words of the orb's lines (English drafts), by clip name; corrections go in the Experience Subtitles asset
		private static readonly Dictionary<string, string> OrbLineTexts = new()
		{
			{ "Narrator_Welcome", "Hi, welcome! I'm your guide today. You don't need any experience with VR: I'll show you everything, step by step." },
			{ "Narrator_HandCheck_Prompt", "Do you see the see-through hands in front of you? Put your own hands in the same place, so they match." },
			{ "Narrator_HandCheck_Stuck", "Move your hands slowly until they line up with the see-through hands." },
			{ "Narrator_HandCheck_Success", "Perfect, I can see your hands." },
			{ "Narrator_Slider_Prompt", "On the table is a slider. Push it with your finger, or pinch and drag it, and watch what happens to me." },
			{ "Narrator_Slider_Stuck", "Try touching the handle of the slider on the table and moving it to the side." },
			{ "Narrator_Slider_Success", "Great! See how I change?" },
			{ "Narrator_PracticeLockIn_Prompt", "When you've chosen, press and hold the button next to the slider until the ring is full." },
			{ "Narrator_PracticeLockIn_Stuck", "Keep your finger on the button until the ring fills up." },
			{ "Narrator_PracticeLockIn_Success", "Locked in! This was only practice, so it doesn't count." },
			{ "Narrator_CharactersIntro", "Before we start, let me introduce some people from the neighborhood." },
			{ "Narrator_NeighborhoodWelcome", "This is their neighborhood. With the slider you can change how it looks. Take your time to look around." },
			{ "Narrator_NeighborhoodLockIn_Prompt", "When you've found the version you like best, hold the button to lock in your choice." },
			{ "Narrator_NeighborhoodLockIn_Stuck", "Choose with the slider, then hold the button until the ring is full." },
			{ "Narrator_NeighborhoodLockIn_Success", "Thank you! Your choice is saved." },
			{ "Narrator_Outro", "We're back in the room. Thanks for helping us think about the future of our neighborhoods." },
			{ "Narrator_Goodbye", "That's all! You can take off the headset now." },
		};
		private const string RegularFontPath = "Assets/Content/Fonts/Inter/Inter Regular SDF.asset";
		private const string NeighborhoodLayoutFile = "Street_Layout.json";
		private const string GhostHandsName = "Ghost Hands";
		private const string LeftHandVisualName = "LeftHandQuestVisual";
		private const string HandVisualizerName = "Hand Visualizer";
		private const string NearFarInteractorName = "Near-Far Interactor";

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
		internal static readonly Vector3 OrbHomeOffset = new(0f, -0.05f, 1f);
		private static readonly Vector3 OrbCharactersOffset = new(-0.5f, -0.22f, 0.8f);
		private static readonly Vector3 OrbNeighborhoodOffset = new(0.6f, 0.25f, 0.9f);

		// During the practice choice the orb floats above the lock-in button, a little further out so it isn't in front
		// of the cap
		private const float OrbButtonLiftMeters = 0.32f;
		private const float OrbButtonOutMeters = 0.1f;

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
			SessionControl session = CreateSession(origin, adminSettings, seatHeight, eyeHeight);
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
			NoticePanel subtitlePanel = CreateSubtitlePanel(head, font);
			SubtitleTable subtitleTable = LoadOrUpdateSubtitles(settings, portraitSets);
			ViewFade fade = CreateFade(head);

			GameObject experience = new("Experience");
			Transform anchors = new GameObject("Orb Spots").transform;
			anchors.SetParent(experience.transform, false);
			Transform orbHome = CreateAnchor("Home", anchors, eyes + OrbHomeOffset);
			Transform orbCharacters = CreateAnchor("While The Characters Speak", anchors, eyes + OrbCharactersOffset);
			Transform orbNeighborhood = CreateAnchor("In The Neighborhood", anchors, eyes + OrbNeighborhoodOffset);
			Transform orbButton = CreateAnchor("Above The Button", anchors, GetOrbButtonSpot(table));

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
			director.ConfigureNarrator(orb, orbProp, orbHome, orbNeighborhood, orbCharacters, orbButton);
			director.ConfigureSteps(handCheck, practice, cardsProp, cards);
			ExperienceSubtitles subtitles = experience.AddComponent<ExperienceSubtitles>();
			subtitles.Configure(subtitlePanel, subtitleTable, CreateSubtitleSpeakers(portraitSets));
			director.ConfigureSubtitles(subtitles);
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
							RemoveHandRays(copy.transform);
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

		// Check-in on; the Player Spot at the seat, facing the table, putting the eyes at the Neighborhood's eye height
		// whatever the real chair and the headset's floor
		private static SessionControl CreateSession(XROrigin origin, AdminMenuSettings adminSettings, float seatHeight,
			float eyeHeight)
		{
			PlayerSpot spot = new GameObject("Player Spot").AddComponent<PlayerSpot>();
			spot.transform.position = new Vector3(0f, seatHeight, 0f);
			spot.Configure(eyeHeight);
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

		internal static NarratorOrbController PlaceOrb(GameObject orbPrefab, Scene scene, Transform head, Vector3 position,
			out ExperienceProp orbProp)
		{
			GameObject orbObject = (GameObject)PrefabUtility.InstantiatePrefab(orbPrefab, scene);
			orbObject.transform.SetPositionAndRotation(position,
				Quaternion.LookRotation(new Vector3(0f, head.position.y, 0f) - position));
			NarratorOrbController orb = orbObject.GetComponent<NarratorOrbController>();
			orb.ConfigureHead(head);
			PrefabUtility.RecordPrefabInstancePropertyModifications(orb);

			// The narrator's level from Voice Levels, set against the characters'
			VoiceLevels levels = AssetDatabase.LoadAssetAtPath<VoiceLevels>(VoiceLevels.AssetPath);
			if (levels != null && orb.Voice != null)
			{
				orb.Voice.gameObject.AddComponent<VoiceGain>().Configure(levels, VoiceRole.Narrator);
			}
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

		// The subtitle strip: like a notice, but wider and lower, and never covered: a quad and 3D text whose materials
		// ignore depth and draw after the world (the table, the orb), under the fade and the admin menu
		private static NoticePanel CreateSubtitlePanel(Transform head, TMP_FontAsset font)
		{
			GameObject panelObject = new("Subtitles");

			GameObject backgroundObject = new("Background");
			backgroundObject.transform.SetParent(panelObject.transform, false);
			backgroundObject.transform.localScale = new Vector3(SubtitleWidthMeters, SubtitleHeightMeters, 1f);
			backgroundObject.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
			MeshRenderer background = backgroundObject.AddComponent<MeshRenderer>();
			background.sharedMaterial = LoadOrCreateSubtitleBackground();
			background.shadowCastingMode = ShadowCastingMode.Off;
			background.receiveShadows = false;
			background.lightProbeUsage = LightProbeUsage.Off;
			background.reflectionProbeUsage = ReflectionProbeUsage.Off;

			GameObject textObject = new("Text", typeof(RectTransform));
			textObject.transform.SetParent(panelObject.transform, false);
			TextMeshPro text = textObject.AddComponent<TextMeshPro>();
			text.rectTransform.sizeDelta = new Vector2(SubtitleWidthMeters - 2f * SubtitlePaddingMeters,
				SubtitleHeightMeters - 2f * SubtitlePaddingMeters);

			// A hair in front of the quad, so the text is drawn over it
			text.rectTransform.anchoredPosition3D = new Vector3(0f, 0f, -0.001f);
			text.rectTransform.localPosition = new Vector3(0f, 0f, -0.001f);
			if (font != null)
			{
				text.font = font;
				text.fontSharedMaterial = LoadOrCreateSubtitleText(font);
			}

			text.fontSize = SubtitleFontSize;
			text.alignment = TextAlignmentOptions.Center;
			text.textWrappingMode = TextWrappingModes.Normal;
			text.richText = true;
			text.color = Color.white;

			NoticePanel panel = panelObject.AddComponent<NoticePanel>();
			panel.Configure(head, text);
			panel.ConfigurePlacement(SubtitleDistanceMeters, SubtitleDropMeters);
			panelObject.SetActive(false);
			return panel;
		}

		// See-through dark like the notices, from a shader that ignores depth (URP's Unlit can't be told to)
		private static Material LoadOrCreateSubtitleBackground()
		{
			Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(SubtitleBackgroundShaderPath);
			Material material = LoadOrCreateMaterialAt(SubtitleBackgroundMaterialPath, shader);
			material.shader = shader;
			material.shaderKeywords = new string[0];
			material.SetColor("_Color", NoticeBackground);
			material.renderQueue = SubtitleRenderQueue;
			EditorUtility.SetDirty(material);
			return material;
		}

		// The font's own atlas and look on TextMesh Pro's overlay shader, which ignores depth, drawn just after the background
		private static Material LoadOrCreateSubtitleText(TMP_FontAsset font)
		{
			Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(SubtitleTextShaderPath);
			Material material = LoadOrCreateMaterialAt(SubtitleTextMaterialPath, shader);
			material.shader = shader;
			material.CopyPropertiesFromMaterial(font.material);
			material.renderQueue = SubtitleRenderQueue + 1;
			EditorUtility.SetDirty(material);
			return material;
		}

		private static Material LoadOrCreateMaterialAt(string path, Shader shader)
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material != null)
			{
				return material;
			}

			EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
			material = new Material(shader);
			AssetDatabase.CreateAsset(material, path);
			return material;
		}

		// The words of every line the experience says itself: the orb's (from the drafts above) and the cards' (from the
		// Neighborhood's voice library, by clip). Lines already in the asset keep their words.
		private static SubtitleTable LoadOrUpdateSubtitles(ExperienceSettings settings, CharacterPortraitSet[] portraitSets)
		{
			SubtitleTable table = AssetDatabase.LoadAssetAtPath<SubtitleTable>(SubtitlesPath);
			if (table == null)
			{
				EnsureFolder(SubtitlesPath.Substring(0, SubtitlesPath.LastIndexOf('/')));
				table = ScriptableObject.CreateInstance<SubtitleTable>();
				AssetDatabase.CreateAsset(table, SubtitlesPath);
			}

			List<AudioClip> orbLines = new()
			{
				settings.Welcome, settings.CharactersIntro, settings.NeighborhoodWelcome, settings.Outro, settings.Goodbye,
			};
			foreach (GuidedStepLines lines in new[]
				{ settings.HandCheck, settings.Slider, settings.PracticeLockIn, settings.NeighborhoodLockIn })
			{
				orbLines.Add(lines.Prompt);
				orbLines.Add(lines.Stuck);
				orbLines.Add(lines.Success);
			}

			foreach (AudioClip clip in orbLines)
			{
				if (clip != null && OrbLineTexts.TryGetValue(clip.name, out string text))
				{
					table.AddIfMissing(new SubtitleEntry(clip, string.Empty, Color.white, text));
				}
			}

			NeighborhoodVoiceLibrary library = AssetDatabase.LoadAssetAtPath<NeighborhoodVoiceLibrary>(VoiceLibraryPath);
			for (int card = 0; card < portraitSets.Length; card++)
			{
				AudioClip clip = settings.GetCharacterLine(card);
				string text = FindLineText(library, clip);
				if (text != null)
				{
					table.AddIfMissing(new SubtitleEntry(clip, portraitSets[card].DisplayName, portraitSets[card].AccentColor, text));
				}
			}

			EditorUtility.SetDirty(table);
			AssetDatabase.SaveAssets();
			return table;
		}

		private static string FindLineText(NeighborhoodVoiceLibrary library, AudioClip clip)
		{
			if (library == null || clip == null)
			{
				return null;
			}

			foreach (NeighborhoodVoiceLine line in library.Lines)
			{
				if (line != null && line.Clip == clip)
				{
					return line.Text;
				}
			}

			return null;
		}

		// The people in the Neighborhood go by their card's name and colour
		private static SubtitleSpeaker[] CreateSubtitleSpeakers(CharacterPortraitSet[] portraitSets)
		{
			List<SubtitleSpeaker> speakers = new();
			foreach (CharacterPortraitSet portraits in portraitSets)
			{
				if (System.Enum.TryParse(portraits.DisplayName, out NeighborhoodCharacterId character))
				{
					speakers.Add(new SubtitleSpeaker(character, portraits.DisplayName, portraits.AccentColor));
				}
			}

			return speakers.ToArray();
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
		internal static InputAction CreateButtonAction(string name, string keyPath, string controllerUsage)
		{
			InputAction action = new(name, InputActionType.Button);
			action.AddBinding(keyPath);
			action.AddBinding($"<XRController>{{LeftHand}}/{{{controllerUsage}}}");
			action.AddBinding($"<XRController>{{RightHand}}/{{{controllerUsage}}}");
			return action;
		}

		// Above the practice table's lock-in button, moved out from the table's middle (to the participant's right)
		private static Vector3 GetOrbButtonSpot(NeighborhoodTableParts table)
		{
			Vector3 button = table.LockButton.transform.position;
			Vector3 outward = button - table.Root.transform.position;
			outward.y = 0f;
			return button + outward.normalized * OrbButtonOutMeters + Vector3.up * OrbButtonLiftMeters;
		}

		private static Transform CreateAnchor(string name, Transform parent, Vector3 position)
		{
			Transform anchor = new GameObject(name).transform;
			anchor.SetParent(parent, false);
			anchor.position = position;
			return anchor;
		}

		// The hands' far interactors draw rays from the palms; nothing here uses them (the tables read the hand bones, the
		// admin menu the controllers), so they go
		private static void RemoveHandRays(Transform rig)
		{
			int removed = 0;
			foreach (Transform part in rig.GetComponentsInChildren<Transform>(true))
			{
				if (part.name == NearFarInteractorName)
				{
					part.gameObject.SetActive(false);
					removed++;
				}
			}

			if (removed == 0)
			{
				Debug.LogWarning($"[Experience] The XR rig has no {NearFarInteractorName}, so no hand rays were switched off.");
			}
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
