using DroneDDW.Characters.TestScene;
using DroneDDW.Voice;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DroneDDW.Characters.Editor
{
	// Menu: DroneDDW > Characters > Create Portrait Test Scene, which builds
	// Scenes/Characters/Character Portraits Test.unity:
	// - a copy of the main scene's XR rig and passthrough layer, so it starts in passthrough like the tutorial
	// - grandma, student and worker as floating cards in an arc in front of the rig's camera, each with its own 3D
	//   voice: a rounded dark panel with a border in the character's colour, the portrait floating just in front of it,
	//   and a name and emotion label
	// - the test controls, which give the test line to one card at a time (keys: see CharacterCardTestControls)
	// Run Build Portrait Assets first. Rebuilding replaces the scene.
	internal static class CharacterPortraitTestSceneBuilder
	{
		#region Fields

		internal const string ScenePath = "Assets/Scenes/Characters/Character Portraits Test.unity";
		internal const string LineClipPath = "Assets/Content/Audio/Voice/Narrator Orb/Narrator_TestSample.wav";

		private const string MenuPath = "DroneDDW/Characters/Create Portrait Test Scene";
		private const string DialogTitle = "Character portraits test scene";
		private const string MainScenePath = "Assets/Scenes/Questionnaire.unity";
		private const float SeatedEyeHeight = 1.2f;

		// The card's canvas is scaled so that one unit is a millimetre; the sizes below are in millimetres
		private const float MetersPerUnit = 0.001f;
		private const float CardWidth = 320f;
		private const float CardHeight = 300f;
		private const float CardCornerRadius = 20f;
		private const float BorderWidth = 5f;

		// Between the portrait and the card's top and side edges
		private const float PortraitMargin = 12f;

		// How far the portrait floats in front of the panel
		private const float PortraitDepth = 15f;

		private const float LabelHeight = 44f;
		private const float LabelBottom = 10f;
		private const float LabelFontSize = 26f;

		// The voices play at full volume up to a metre away, so the speaker sounds close
		private const float VoiceMinDistance = 1f;
		private const float VoiceMaxDistance = 20f;

		// Opaque, so the cards stay solid over passthrough
		private static readonly Color32 PanelColor = new(0x0E, 0x14, 0x35, 0xFF);

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Create()
		{
			CharacterPortraitSettings settings =
				AssetDatabase.LoadAssetAtPath<CharacterPortraitSettings>(CharacterPortraitBuilder.SettingsPath);
			Sprite panel = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterPortraitBuilder.PanelSpritePath);
			CharacterPortraitSet[] portraitSets = LoadPortraitSets();
			if (settings == null || panel == null || portraitSets == null)
			{
				Debug.LogError("[Characters] The test scene needs the portrait sets, the card panel and the settings: "
					+ "run DroneDDW > Characters > Build Portrait Assets first.");
				return;
			}

			AudioClip line = AssetDatabase.LoadAssetAtPath<AudioClip>(LineClipPath);
			if (line == null)
			{
				Debug.LogError($"[Characters] The test scene needs the test line, {LineClipPath}.");
				return;
			}

			if (line.loadType != AudioClipLoadType.DecompressOnLoad)
			{
				Debug.LogWarning($"[Characters] {LineClipPath} isn't Decompress On Load, so the cards will follow its "
					+ "live output instead of reading it ahead. DroneDDW > Narrator Orb > Build Orb Assets imports it "
					+ "for reading ahead.");
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
			Transform head = CopyMainSceneRig(scene);
			if (head == null)
			{
				Debug.LogWarning($"[Characters] No XR rig found in {MainScenePath}, so the cards face a plain head "
					+ "point and no camera draws them.");
				head = new GameObject("Head").transform;
				head.position = new Vector3(0f, SeatedEyeHeight, 0f);
			}

			// Where the editor's head looks; in Play mode the controls re-place the cards at the headset's first pose
			Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
			if (flatForward.sqrMagnitude < 0.01f)
			{
				flatForward = Vector3.forward;
			}

			CharacterCard[] cards = new CharacterCard[portraitSets.Length];
			for (int card = 0; card < cards.Length; card++)
			{
				Pose pose = CharacterCardTestControls.GetCardPose(head.position, flatForward, card, cards.Length,
					settings);
				cards[card] = CreateCard(portraitSets[card], settings, panel, (float)card / cards.Length, pose);
			}

			GameObject controls = new("Character Card Test Controls");
			controls.AddComponent<CharacterCardTestControls>().Configure(cards, settings, line, head);

			CharacterPortraitBuilder.EnsureFolder(ScenePath.Substring(0, ScenePath.LastIndexOf('/')));
			EditorSceneManager.SaveScene(scene, ScenePath);
			Debug.Log($"[Characters] Built {ScenePath}: {cards.Length} cards {settings.CardDistanceMeters} m in front "
				+ "of the rig.");
		}

		// The sets in the builder's order (the cards' order, left to right), or null when one is missing
		internal static CharacterPortraitSet[] LoadPortraitSets()
		{
			CharacterPortraitSet[] sets = new CharacterPortraitSet[CharacterPortraitBuilder.Characters.Length];
			for (int character = 0; character < sets.Length; character++)
			{
				string path = CharacterPortraitBuilder.GetSetPath(CharacterPortraitBuilder.Characters[character].Name);
				sets[character] = AssetDatabase.LoadAssetAtPath<CharacterPortraitSet>(path);
				if (sets[character] == null)
				{
					return null;
				}
			}

			return sets;
		}

		// Copies the main scene's XR rig (the root with an XR Origin) to the origin, and the root holding its
		// passthrough layer. It is a copy: rebuild the scene after changing the rig there. Returns the rig's camera.
		private static Transform CopyMainSceneRig(Scene scene)
		{
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
			{
				return null;
			}

			Scene source = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
			try
			{
				Transform head = null;
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
						head = copy.GetComponent<XROrigin>().Camera.transform;
					}
				}

				return head;
			}
			finally
			{
				EditorSceneManager.CloseScene(source, true);
			}
		}

		// The card's root holds the voice; its canvas (the body) holds the parts, which the card animates. The name and
		// emotion label is for the test scene (it shows the keys' effect); the experience's cards go without it
		internal static CharacterCard CreateCard(CharacterPortraitSet portraits, CharacterPortraitSettings settings,
			Sprite panel, float hoverPhase, Pose pose, bool hasLabel = true)
		{
			GameObject root = new($"{portraits.DisplayName} Card");
			root.transform.SetPositionAndRotation(pose.position, pose.rotation);
			AudioSource voice = root.AddComponent<AudioSource>();
			voice.playOnAwake = false;
			voice.loop = false;
			voice.spatialBlend = 1f;
			voice.dopplerLevel = 0f;
			voice.rolloffMode = AudioRolloffMode.Logarithmic;
			voice.minDistance = VoiceMinDistance;
			voice.maxDistance = VoiceMaxDistance;
			VoiceLiveTap liveTap = root.AddComponent<VoiceLiveTap>();

			// The card plays the person's own recording, at their level from Voice Levels
			VoiceLevels levels = AssetDatabase.LoadAssetAtPath<VoiceLevels>(VoiceLevels.AssetPath);
			if (levels != null && System.Enum.TryParse(portraits.DisplayName, out VoiceRole role))
			{
				root.AddComponent<VoiceGain>().Configure(levels, role);
			}

			RectTransform body = CreateBody(root.transform);
			Image border = CreatePanelImage("Border", body, panel, portraits.AccentColor, 0f, CardCornerRadius);
			CreatePanelImage("Panel", body, panel, PanelColor, BorderWidth, CardCornerRadius - BorderWidth);
			RawImage portrait = CreatePortrait(body, portraits);
			RawImage mouth = CreateUiPart<RawImage>("Mouth", portrait.rectTransform);
			mouth.raycastTarget = false;
			mouth.enabled = false;
			TextMeshProUGUI label = hasLabel ? CreateLabel(body, portraits) : null;

			CharacterCard card = root.AddComponent<CharacterCard>();
			card.Configure(portraits, settings, voice, liveTap, hoverPhase, body, portrait, mouth, border, label);
			return card;
		}

		private static RectTransform CreateBody(Transform parent)
		{
			GameObject bodyObject = new("Body", typeof(RectTransform));
			RectTransform body = (RectTransform)bodyObject.transform;
			body.SetParent(parent, false);
			Canvas canvas = bodyObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.WorldSpace;
			canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
				| AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
			body.sizeDelta = new Vector2(CardWidth, CardHeight);
			body.localPosition = Vector3.zero;
			body.localRotation = Quaternion.identity;
			body.localScale = Vector3.one * MetersPerUnit;
			return body;
		}

		// A rounded panel filling the card, inset from its edges. The sprite has as many pixels per unit as the canvas
		// (100), so the multiplier sets the corner radius: the sprite's radius in pixels over the multiplier.
		private static Image CreatePanelImage(string name, RectTransform body, Sprite panel, Color color, float inset,
			float cornerRadius)
		{
			Image image = CreateUiPart<Image>(name, body);
			RectTransform rect = image.rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = new Vector2(inset, inset);
			rect.offsetMax = new Vector2(-inset, -inset);
			image.sprite = panel;
			image.type = Image.Type.Sliced;
			image.pixelsPerUnitMultiplier = CharacterPortraitBuilder.PanelCornerRadius / cornerRadius;
			image.color = color;
			image.raycastTarget = false;
			return image;
		}

		// Hangs from the card's top edge with its pivot on its own bottom edge, which the syllable bounce stretches
		// from. Shows the closed natural portrait until Play mode.
		private static RawImage CreatePortrait(RectTransform body, CharacterPortraitSet portraits)
		{
			RawImage portrait = CreateUiPart<RawImage>("Portrait", body);
			RectTransform rect = portrait.rectTransform;
			float width = CardWidth - 2f * PortraitMargin;
			float height = width / CharacterPortraitBuilder.CropAspect;
			rect.anchorMin = new Vector2(0.5f, 1f);
			rect.anchorMax = new Vector2(0.5f, 1f);
			rect.pivot = new Vector2(0.5f, 0f);
			rect.sizeDelta = new Vector2(width, height);
			rect.anchoredPosition3D = new Vector3(0f, -PortraitMargin - height, -PortraitDepth);
			portrait.texture = portraits.GetPortrait(CharacterEmotion.Natural, false);
			portrait.uvRect = portraits.CropRect;
			portrait.raycastTarget = false;
			return portrait;
		}

		private static TextMeshProUGUI CreateLabel(RectTransform body, CharacterPortraitSet portraits)
		{
			TextMeshProUGUI label = CreateUiPart<TextMeshProUGUI>("Label", body);
			RectTransform rect = label.rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = new Vector2(1f, 0f);
			rect.pivot = new Vector2(0.5f, 0f);
			rect.sizeDelta = new Vector2(-2f * PortraitMargin, LabelHeight);
			rect.anchoredPosition = new Vector2(0f, LabelBottom);
			label.fontSize = LabelFontSize;
			label.alignment = TextAlignmentOptions.Center;
			label.color = Color.white;
			label.raycastTarget = false;
			label.text = CharacterCard.GetLabel(portraits, CharacterEmotion.Natural);
			return label;
		}

		private static T CreateUiPart<T>(string name, Transform parent) where T : Component
		{
			GameObject part = new(name, typeof(RectTransform));
			part.transform.SetParent(parent, false);
			return part.AddComponent<T>();
		}

		#endregion
	}
}
