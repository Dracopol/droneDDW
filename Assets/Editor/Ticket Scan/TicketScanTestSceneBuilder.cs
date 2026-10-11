using System.IO;
using DroneDDW.TicketScan.TestScene;
using Meta.XR;
using Meta.XR.MRUtilityKit;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DroneDDW.TicketScan.Editor
{
	// Menu: DroneDDW > Ticket Scan > Create Camera Ticket Scan Test Scene, and Create Tracker Ticket Scan Test Scene.
	// Each builds one variant of the ticket scan test in Scenes/Ticket Scan, to compare on the headset how well each
	// reads the participants' tickets (QR codes printed white on purple, 26 mm wide, holding a survey link with the
	// participant number as its code parameter):
	// - Camera Ticket Scan Test: the headset camera's own images (Meta's Passthrough Camera Access), decoded with ZXing
	// - Tracker Ticket Scan Test: Horizon OS's QR code tracking through MRUK, with its scene loading and world lock off
	// Both get a copy of the main scene's XR rig and passthrough layer (both ways need passthrough on), the panel that
	// shows what is read, its sounds and the test controls (TicketScanTestControls). Expected quirks:
	// - in the tracker scene MRUK logs an error that OVRCameraRig is missing: the rig is XRI, and QR tracking works
	//   without one
	// - over Link, Meta says QR tracking may only work on the first Play; restarting Link or the headset helps
	// - the camera scene needs Meta Horizon Link v85+ in the Editor, or a Quest 3 / 3S on Horizon OS v74+
	// Rebuilding replaces the scene.
	internal static class TicketScanTestSceneBuilder
	{
		#region Entities

		private enum ScanVariant
		{
			Camera,
			Tracker,
		}

		#endregion

		#region Fields

		internal const string CameraScenePath = "Assets/Scenes/Ticket Scan/Camera Ticket Scan Test.unity";
		internal const string TrackerScenePath = "Assets/Scenes/Ticket Scan/Tracker Ticket Scan Test.unity";

		private const string CameraMenuPath = "DroneDDW/Ticket Scan/Create Camera Ticket Scan Test Scene";
		private const string TrackerMenuPath = "DroneDDW/Ticket Scan/Create Tracker Ticket Scan Test Scene";
		private const string DialogTitle = "Ticket scan test scene";
		private const string MainScenePath = "Assets/Scenes/Questionnaire.unity";
		private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
		private const string RegularFontPath = "Assets/Content/Fonts/Inter/Inter Regular SDF.asset";
		private const string SemiBoldFontPath = "Assets/Content/Fonts/Inter/Inter SemiBold SDF.asset";
		private const string TicketClipPath = "Assets/Content/Audio/SFX/Lock In.wav";
		private const string OtherCodeClipPath = "Assets/Content/Audio/SFX/Stamp Rejected.wav";

		// Where the panel waits until the headset reports its first pose, relative to the rig
		private const float SeatedEyeHeight = 1.2f;

		// The panel: one unit is a millimetre
		private const float UnitMeters = 0.001f;
		private const float PanelWidth = 460f;
		private const float PanelHeight = 340f;
		private const float Padding = 14f;
		private const float ContentWidth = PanelWidth - 2f * Padding;
		private const float PreviewTop = 60f;
		private const float PreviewSize = 170f;
		private const float ColumnLeft = Padding + PreviewSize + 16f;
		private const float ColumnWidth = PanelWidth - ColumnLeft - Padding;

		private const float TitleSize = 18f;
		private const float BodySize = 10.5f;
		private const float SmallSize = 9f;
		private const float PayloadSize = 8.5f;
		private const float NumberMinSize = 22f;
		private const float NumberMaxSize = 84f;

		private const string Instruction = "Hold a ticket 15–25 cm in front of your face, code towards you · "
			+ "Space / A: clear · R / B: recentre";

		private static readonly Color BackgroundColor = new(0.07f, 0.08f, 0.1f, 0.9f);
		private static readonly Color PreviewBoxColor = new(0.13f, 0.14f, 0.17f, 1f);
		private static readonly Color TextColor = new(0.95f, 0.96f, 0.97f);
		private static readonly Color MutedColor = new(0.68f, 0.71f, 0.76f);

		// The camera at its largest square size; it takes the next smaller size it has otherwise
		private static readonly Vector2Int CameraResolution = new(1280, 1280);

		#endregion

		#region Private

		[MenuItem(CameraMenuPath)]
		private static void CreateCameraScene()
		{
			Create(ScanVariant.Camera);
		}

		[MenuItem(TrackerMenuPath)]
		private static void CreateTrackerScene()
		{
			Create(ScanVariant.Tracker);
		}

		private static void Create(ScanVariant variant)
		{
			TMP_FontAsset regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
			TMP_FontAsset semiBoldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiBoldFontPath);
			AudioClip ticketClip = AssetDatabase.LoadAssetAtPath<AudioClip>(TicketClipPath);
			AudioClip otherCodeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(OtherCodeClipPath);
			if (regularFont == null || semiBoldFont == null || ticketClip == null || otherCodeClip == null)
			{
				Debug.LogError("[Ticket Scan] The test scene needs the Inter font assets (DroneDDW > Admin Menu > "
					+ $"Build Admin Menu Assets makes them) and the sounds {TicketClipPath} and {OtherCodeClipPath}.");
				return;
			}

			string scenePath = variant == ScanVariant.Camera ? CameraScenePath : TrackerScenePath;
			bool isReplacing = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null;
			if (!Application.isBatchMode && isReplacing
				&& !EditorUtility.DisplayDialog(DialogTitle, $"Replace {scenePath}?", "Replace", "Cancel"))
			{
				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			XROrigin origin = CopyMainSceneRig(scene);
			Transform head;
			Transform trackingSpace;
			if (origin != null)
			{
				head = origin.Camera.transform;
				trackingSpace = origin.CameraFloorOffsetObject != null
					? origin.CameraFloorOffsetObject.transform
					: head.parent;
			}
			else
			{
				Debug.LogWarning($"[Ticket Scan] No XR rig found in {MainScenePath}: the scene gets a plain head "
					+ "point, so it only works as a layout check.");
				head = new GameObject("Head").transform;
				head.position = new Vector3(0f, SeatedEyeHeight, 0f);
				trackingSpace = null;
			}

			CreateLight();
			TicketScanner scanner = variant == ScanVariant.Camera
				? CreateCameraScanner()
				: CreateTrackerScanner(head, trackingSpace);
			TicketScanPanel panel = CreatePanel(variant, regularFont, semiBoldFont);
			CreateControls(scanner, panel, head, ticketClip, otherCodeClip);

			EnsureFolder(scenePath.Substring(0, scenePath.LastIndexOf('/')));
			EditorSceneManager.SaveScene(scene, scenePath);
			WarnAboutMissingSetup(variant);
			Debug.Log($"[Ticket Scan] Built {scenePath}.");
		}

		// Passthrough Camera Access on the left camera, and the scanner reading it
		private static TicketScanner CreateCameraScanner()
		{
			GameObject scannerObject = new("Camera Ticket Scanner");
			PassthroughCameraAccess cameraAccess = scannerObject.AddComponent<PassthroughCameraAccess>();
			cameraAccess.CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
			cameraAccess.RequestedResolution = CameraResolution;
			CameraTicketScanner scanner = scannerObject.AddComponent<CameraTicketScanner>();
			scanner.Configure(cameraAccess);
			return scanner;
		}

		// MRUK with QR code tracking on and everything else this test doesn't need off: loading the room's scene model
		// (which would ask for a room scan) and world locking (which moves an OVRCameraRig the scene doesn't have)
		private static TicketScanner CreateTrackerScanner(Transform head, Transform trackingSpace)
		{
			MRUK mruk = new GameObject("MRUK").AddComponent<MRUK>();
			mruk.EnableWorldLock = false;
			mruk.SceneSettings ??= new MRUK.MRUKSettings();
			mruk.SceneSettings.LoadSceneOnStartup = false;
			mruk.SceneSettings.TrackerConfiguration = new OVRAnchor.TrackerConfiguration
			{
				QRCodeTrackingEnabled = true,
			};

			TrackerTicketScanner scanner = new GameObject("Tracker Ticket Scanner")
				.AddComponent<TrackerTicketScanner>();
			scanner.Configure(mruk, head, trackingSpace);
			return scanner;
		}

		// The panel, where the controls put it until the headset reports its first pose: in front of a seated head at
		// the rig's origin
		private static TicketScanPanel CreatePanel(ScanVariant variant, TMP_FontAsset regularFont,
			TMP_FontAsset semiBoldFont)
		{
			GameObject panelObject = new("Ticket Scan Panel", typeof(RectTransform));
			RectTransform panelRect = (RectTransform)panelObject.transform;
			Canvas canvas = panelObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.WorldSpace;
			canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
				| AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
			panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
			panelRect.localScale = Vector3.one * UnitMeters;
			Vector3 eye = new(0f, SeatedEyeHeight, 0f);
			Vector3 position = eye + Vector3.forward * TicketScanTestControls.PanelDistanceMeters
				+ Vector3.down * TicketScanTestControls.PanelDropMeters;
			panelRect.SetPositionAndRotation(position, Quaternion.LookRotation(position - eye, Vector3.up));

			CreateImage("Background", panelRect, BackgroundColor);
			bool isCamera = variant == ScanVariant.Camera;
			CreateText(panelRect, "Title", isCamera ? "Camera ticket scan" : "Tracker ticket scan", Padding, 10f,
				ContentWidth, 26f, semiBoldFont, TitleSize, TextColor, TextAlignmentOptions.TopLeft, false);
			CreateText(panelRect, "Instruction", Instruction, Padding, 36f, ContentWidth, 18f, regularFont, BodySize,
				MutedColor, TextAlignmentOptions.TopLeft, false);

			RectTransform previewBox = CreateRect("Preview Box", panelRect);
			Place(previewBox, Padding, PreviewTop, PreviewSize, PreviewSize);
			CreateImage("Box", previewBox, PreviewBoxColor);
			RawImage preview = null;
			if (isCamera)
			{
				RectTransform previewRect = CreateRect("Camera Image", previewBox);
				previewRect.sizeDelta = new Vector2(PreviewSize, PreviewSize);
				preview = previewRect.gameObject.AddComponent<RawImage>();
				preview.raycastTarget = false;

				// Shown once the camera plays: an image without a texture would draw white
				preview.enabled = false;
			}
			else
			{
				CreateText(previewBox, "No Camera Image", "The system tracker shares no camera image", 12f, 0f,
					PreviewSize - 24f, PreviewSize, regularFont, BodySize, MutedColor, TextAlignmentOptions.Center,
					true);
			}

			TextMeshProUGUI number = CreateText(panelRect, "Number", "---", ColumnLeft, PreviewTop, ColumnWidth, 92f,
				semiBoldFont, NumberMaxSize, TextColor, TextAlignmentOptions.Left, false);
			number.enableAutoSizing = true;
			number.fontSizeMin = NumberMinSize;
			number.fontSizeMax = NumberMaxSize;
			TextMeshProUGUI details = CreateText(panelRect, "Details", string.Empty, ColumnLeft, 156f, ColumnWidth, 30f,
				regularFont, BodySize, TextColor, TextAlignmentOptions.TopLeft, true);
			TextMeshProUGUI payload = CreateText(panelRect, "Payload", string.Empty, ColumnLeft, 188f, ColumnWidth, 26f,
				regularFont, PayloadSize, MutedColor, TextAlignmentOptions.TopLeft, true);
			TextMeshProUGUI firstRead = CreateText(panelRect, "First Read", string.Empty, ColumnLeft, 214f,
				ColumnWidth, 18f, regularFont, BodySize, TextColor, TextAlignmentOptions.TopLeft, false);
			TextMeshProUGUI status = CreateText(panelRect, "Status", string.Empty, Padding, 240f, ContentWidth, 26f,
				regularFont, SmallSize, MutedColor, TextAlignmentOptions.TopLeft, true);
			TextMeshProUGUI log = CreateText(panelRect, "Log", string.Empty, Padding, 270f, ContentWidth, 60f,
				regularFont, SmallSize, TextColor, TextAlignmentOptions.TopLeft, true);

			TicketScanPanel panel = panelObject.AddComponent<TicketScanPanel>();
			panel.Configure(number, details, payload, firstRead, status, log, preview,
				new Vector2(PreviewSize, PreviewSize));
			return panel;
		}

		// The test controls with a 2D sound source and their buttons: Space or A / X clears, R or B / Y recentres
		private static void CreateControls(TicketScanner scanner, TicketScanPanel panel, Transform head,
			AudioClip ticketClip, AudioClip otherCodeClip)
		{
			GameObject controlsObject = new("Ticket Scan Test Controls");
			AudioSource audioSource = controlsObject.AddComponent<AudioSource>();
			audioSource.playOnAwake = false;
			audioSource.spatialBlend = 0f;
			TicketScanTestControls controls = controlsObject.AddComponent<TicketScanTestControls>();
			controls.Configure(scanner, panel, head, audioSource, ticketClip, otherCodeClip,
				CreateButtonAction("Clear", "<Keyboard>/space", "PrimaryButton"),
				CreateButtonAction("Recenter", "<Keyboard>/r", "SecondaryButton"));
		}

		// A key and the same button on either controller (A / X for the primary, B / Y for the secondary button)
		private static InputAction CreateButtonAction(string name, string keyPath, string controllerUsage)
		{
			InputAction action = new(name, InputActionType.Button);
			action.AddBinding(keyPath);
			action.AddBinding($"<XRController>{{LeftHand}}/{{{controllerUsage}}}");
			action.AddBinding($"<XRController>{{RightHand}}/{{{controllerUsage}}}");
			return action;
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

		// The scene can't ask for its permission on the headset unless the app's manifest declares it, and Meta's
		// project settings must allow the feature
		private static void WarnAboutMissingSetup(ScanVariant variant)
		{
			bool isCamera = variant == ScanVariant.Camera;
			string permission = isCamera
				? OVRPermissionsRequester.PassthroughCameraAccessPermission
				: OVRPermissionsRequester.ScenePermission;
			string manifest = File.Exists(ManifestPath) ? File.ReadAllText(ManifestPath) : string.Empty;
			if (!manifest.Contains(permission))
			{
				Debug.LogWarning($"[Ticket Scan] {ManifestPath} doesn't declare {permission}, so the scene can't ask "
					+ "for it on the headset.");
			}

			OVRProjectConfig config = OVRProjectConfig.CachedProjectConfig;
			if (config == null)
			{
				return;
			}

			if (isCamera && !config.isPassthroughCameraAccessEnabled)
			{
				Debug.LogWarning("[Ticket Scan] Passthrough Camera Access is off in Meta's project settings (OVR "
					+ "Manager > Quest Features).");
			}

			if (!isCamera && config.sceneSupport == OVRProjectConfig.FeatureSupport.None)
			{
				Debug.LogWarning("[Ticket Scan] Scene Support is off in Meta's project settings (OVR Manager > Quest "
					+ "Features), and QR code tracking needs it.");
			}
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

		// A plain image filling its parent
		private static void CreateImage(string name, RectTransform parent, Color color)
		{
			RectTransform rect = CreateRect(name, parent);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.sizeDelta = Vector2.zero;
			Image image = rect.gameObject.AddComponent<Image>();
			image.color = color;
			image.raycastTarget = false;
		}

		private static TextMeshProUGUI CreateText(RectTransform parent, string name, string text, float left, float top,
			float width, float height, TMP_FontAsset font, float fontSize, Color color, TextAlignmentOptions alignment,
			bool isWrapped)
		{
			RectTransform rect = CreateRect(name, parent);
			Place(rect, left, top, width, height);
			TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
			label.font = font;
			label.fontSize = fontSize;
			label.color = color;
			label.alignment = alignment;
			label.textWrappingMode = isWrapped ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
			label.overflowMode = isWrapped ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;

			// Codes' text is shown as read, so a < in it mustn't turn into a tag
			label.richText = false;
			label.raycastTarget = false;
			label.text = text;
			return label;
		}

		private static RectTransform CreateRect(string name, Transform parent)
		{
			GameObject rectObject = new(name, typeof(RectTransform));
			RectTransform rect = (RectTransform)rectObject.transform;
			rect.SetParent(parent, false);
			return rect;
		}

		// From the parent's top-left corner, y down
		private static void Place(RectTransform rect, float left, float top, float width, float height)
		{
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(width, height);
			rect.anchoredPosition = new Vector2(left + width * 0.5f, -(top + height * 0.5f));
		}

		#endregion
	}
}
