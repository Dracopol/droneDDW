using System.Collections.Generic;
using DroneDDW.Parks.Levels;
using DroneDDW.Parks.Table;
using TMPro;
using UnityEditor;
using UnityEngine;
#if PARKS_XRI
using UnityEngine.XR.Interaction.Toolkit;
#if PARKS_XRI_3
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#endif
#endif

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// The floating board both park scenes share: Park_Levels_Rig's five-detent slider on a board that follows the head,
	/// pushed by any part of a hand and grabbed once the fingers close, with its sounds, lights, level name label and the
	/// testing view of the push spheres and knob boxes, all tuned by the shared push settings. The park keeps the rig's
	/// miniature on a board the size of the old table; the slider tuning scene drops the miniature and keeps only a strip
	/// under the slider, in the same place relative to the head.
	/// </summary>
	internal static class ParkBoardBuilder
	{
		#region Entities

		internal readonly struct Board
		{
			public Board(ParkLevelSlider slider, Transform modelRoot, Transform viewCone, Behaviour spheresView)
			{
				Slider = slider;
				ModelRoot = modelRoot;
				ViewCone = viewCone;
				SpheresView = spheresView;
			}

			public ParkLevelSlider Slider { get; }

			/// <summary>Where the miniatures go (null on a slider-only board).</summary>
			public Transform ModelRoot { get; }

			public Transform ViewCone { get; }
			public Behaviour SpheresView { get; }
		}

		#endregion

		#region Fields

		internal const string RigModelName = "Park_Levels_Rig";

		private const string BoardName = "Park_Board";
		private const string TuningObjectName = "Slider Tuning";
		private const string TableName = "Rig_Table";
		private const string PlinthName = "Rig_Plinth";
		private const string SliderName = "Rig_Slider";
		private const string HandleName = "Rig_Handle";
		private const string ModelRootName = "Rig_ModelRoot";
		private const string ViewConeName = "Rig_ViewCone";
		private const string NotchPrefix = "Rig_Detent_";
		private const string NatureGlyphName = "Rig_Glyph_Nature";
		private const string EfficiencyGlyphName = "Rig_Glyph_Efficiency";
		private const string PressClipFileName = "Button Press.wav";
		private const string TouchClipFileName = "Button Touch.wav";
		private const string ReleaseClipFileName = "Button Release.wav";
		private const string LockInClipFileName = "Lock In.wav";
		private const string ScrapeClipFileName = "Slider Scrape.wav";

		// The board's top: below and ahead of the eyes, turned with the head
		private const float BoardDistance = 0.75f;
		private const float BoardDrop = 0.48f;
		private const float BoardThickness = 0.03f;
		private const float StripMargin = 0.04f;
		private const float SliderHalfTravel = 0.3f;
		private const float SoundMinDistance = 0.3f;
		private const float SoundMaxDistance = 10f;
		private const float LabelFontSize = 0.13f;

		private static readonly string[] RigParts =
		{
			TableName, PlinthName, SliderName, HandleName, ModelRootName, ViewConeName,
		};

		private static readonly Vector3 LabelOffsetMeters = new Vector3(0f, 0.1f, 0.06f);
		private static readonly Vector2 LabelSizeMeters = new Vector2(0.3f, 0.05f);

		#endregion

		#region Internal

		/// <summary>
		/// Builds the board from the rig model, in front of the seat until the head takes over. Slider-only drops the
		/// miniature and plinth and shrinks the board to a strip under the slider. Returns the parts the scene wires up.
		/// </summary>
		internal static Board Build(GameObject rigModel, bool isSliderOnly, Vector3 seatPosition, int startLevel,
			Transform head, Transform trackingSpace, ParkLevelController controller, ParkPushSettings settings,
			bool isShowingSpheres)
		{
			GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(rigModel);
			PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

			Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
			foreach (string partName in RigParts)
			{
				Transform part = ParkAssetUtility.FindDeep(rig.transform, partName);
				if (part == null)
				{
					Debug.LogError($"[Parks] {RigModelName}.fbx has no {partName}; the board is incomplete.");
					return default;
				}

				parts.Add(partName, part);
			}

			Transform slider = parts[SliderName];
			Transform board = ReplaceTableWithBoard(rig.transform, parts[TableName], slider, isSliderOnly, seatPosition);
			if (board == null)
			{
				return default;
			}

			Transform modelRoot = parts[ModelRootName];
			Transform viewCone = parts[ViewConeName];
			if (isSliderOnly)
			{
				Object.DestroyImmediate(parts[PlinthName].gameObject);
				Object.DestroyImmediate(modelRoot.gameObject);
				modelRoot = null;
				viewCone = null;
			}

			Transform handle = parts[HandleName];
			ParkLevelSlider levelSlider = slider.gameObject.AddComponent<ParkLevelSlider>();
			levelSlider.Configure(handle, -SliderHalfTravel, SliderHalfTravel);
			Vector3 handlePosition = handle.localPosition;
			handlePosition.x = Mathf.Lerp(-SliderHalfTravel, SliderHalfTravel, ParkLevels.ToBlend(startLevel));
			handle.localPosition = handlePosition;
			if (handle.TryGetComponent(out BoxCollider handleCollider))
			{
				// The grab box, which the hand driver keeps on the settings' values while playing
				ParkPushSettings values = ParkPushSettings.OrDefaults(settings);
				handleCollider.center = values.GrabBoxCenter;
				handleCollider.size = values.GrabBoxSize;
			}

			Behaviour spheresView = AddHandInput(levelSlider, handle.gameObject, trackingSpace, settings, isShowingSpheres);
			AddSliderFeedback(levelSlider, rig.transform, modelRoot, controller, head, startLevel);
			ParkBoardFollow follow = board.gameObject.AddComponent<ParkBoardFollow>();
			follow.Configure(head, levelSlider, new Vector3(0f, -BoardDrop, BoardDistance));
			return new Board(levelSlider, modelRoot, viewCone, spheresView);
		}

		/// <summary>
		/// The scene's "Slider Tuning" object, which shows the shared push settings in place with a switch for the
		/// spheres and boxes.
		/// </summary>
		internal static void AddTuningObject(ParkPushSettings settings, Behaviour spheresView)
		{
			ParkSliderTuning tuning = new GameObject(TuningObjectName).AddComponent<ParkSliderTuning>();
			tuning.Configure(settings, spheresView);
		}

		#endregion

		#region Private

		/// <summary>
		/// Swaps the rig's table (top and legs) for a slab, flush with the old top, under a new root whose origin is the
		/// middle of the old top: the point the follow keeps in front of the head. The slab has the table's footprint,
		/// or is only a strip under the slider. The board starts where the table top was, in front of the seat.
		/// </summary>
		private static Transform ReplaceTableWithBoard(Transform rig, Transform table, Transform slider, bool isSliderOnly,
			Vector3 seatPosition)
		{
			if (!table.TryGetComponent(out MeshRenderer tableRenderer) || !table.TryGetComponent(out MeshFilter tableFilter))
			{
				Debug.LogError($"[Parks] {RigModelName}.fbx's {TableName} has no mesh to size the board from.");
				return null;
			}

			// The rig sits at the origin, so its world bounds are in rig space
			Bounds tableBounds = tableRenderer.bounds;
			Bounds slabBounds = isSliderOnly ? GetStripBounds(slider) : tableBounds;
			Vector3 top = new Vector3(tableBounds.center.x, tableBounds.max.y, tableBounds.center.z);
			Transform board = new GameObject(BoardName).transform;
			board.position = top;
			rig.SetParent(board, true);

			GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
			slab.name = "Board";
			Object.DestroyImmediate(slab.GetComponent<Collider>());
			slab.transform.SetParent(board, false);
			slab.transform.localPosition = new Vector3(slabBounds.center.x - top.x, -BoardThickness * 0.5f,
				slabBounds.center.z - top.z);
			slab.transform.localScale = new Vector3(slabBounds.size.x, BoardThickness, slabBounds.size.z);
			MeshFilter slabMesh = slab.GetComponent<MeshFilter>();
			slabMesh.sharedMesh = CreateSwatchMesh(slabMesh.sharedMesh, tableFilter.sharedMesh);
			slab.GetComponent<MeshRenderer>().sharedMaterial = tableRenderer.sharedMaterial;

			Object.DestroyImmediate(table.gameObject);
			board.position = seatPosition + new Vector3(0f, 0f, BoardDistance) + top;
			return board;
		}

		/// <summary>The slider's footprint (track, notches, knob and end glyphs) with a margin around it.</summary>
		private static Bounds GetStripBounds(Transform slider)
		{
			Bounds bounds = new Bounds(slider.position, Vector3.zero);
			foreach (Renderer renderer in slider.GetComponentsInChildren<Renderer>())
			{
				bounds.Encapsulate(renderer.bounds);
			}

			bounds.Expand(new Vector3(StripMargin * 2f, 0f, StripMargin * 2f));
			return bounds;
		}

		/// <summary>
		/// A copy of the box mesh painted with the table top's palette swatch: every UV points at the UV of the
		/// table's highest vertex, so the board keeps the table's colour from the shared palette texture.
		/// </summary>
		private static Mesh CreateSwatchMesh(Mesh box, Mesh table)
		{
			Vector3[] tableVertices = table.vertices;
			Vector2[] tableUVs = table.uv;
			Vector2 swatch = Vector2.zero;
			float highest = float.NegativeInfinity;
			for (int i = 0; i < tableVertices.Length && i < tableUVs.Length; i++)
			{
				if (tableVertices[i].y > highest)
				{
					highest = tableVertices[i].y;
					swatch = tableUVs[i];
				}
			}

			Mesh mesh = Object.Instantiate(box);
			mesh.name = "Park_Board_Slab";
			Vector2[] uvs = new Vector2[mesh.vertexCount];
			for (int i = 0; i < uvs.Length; i++)
			{
				uvs[i] = swatch;
			}

			mesh.uv = uvs;
			return mesh;
		}

		/// <summary>
		/// Hands push the handle by default and grab it once the fingers close: the interactable is what the finger
		/// grab selects, the hand bones are what pushes, and the driver combines both into one drag. Returns the testing
		/// view of the spheres and boxes, or null without XR input.
		/// </summary>
		private static Behaviour AddHandInput(ParkLevelSlider slider, GameObject handle, Transform trackingSpace,
			ParkPushSettings settings, bool isShowingSpheres)
		{
#if PARKS_XRI
			if (!handle.TryGetComponent(out MeshFilter handleMesh))
			{
				Debug.LogError($"[Parks] {RigModelName}.fbx's {HandleName} has no mesh, so the hands can't push it.");
			}

			XRSimpleInteractable interactable = handle.AddComponent<XRSimpleInteractable>();
			ParkHandBones handBones = slider.gameObject.AddComponent<ParkHandBones>();
			handBones.Configure(trackingSpace, settings);
			ParkSliderHandDriver driver = slider.gameObject.AddComponent<ParkSliderHandDriver>();
			driver.Configure(slider, interactable, handleMesh, handBones, settings);
			return AddHandSpheresView(driver, isShowingSpheres);
#else
			Debug.LogWarning("[Parks] XR Interaction Toolkit is not installed, so the slider has no XR input yet. "
				+ "Install it and run this menu again, or call ParkLevelSlider.BeginDrag / DragTo / EndDrag from your own input.");
			return null;
#endif
		}

#if PARKS_XRI
		/// <summary>
		/// The testing view of the spheres the hands push with and the knob's push and grab boxes, on the slider. The
		/// "Slider Tuning" object switches it on and off.
		/// </summary>
		private static Behaviour AddHandSpheresView(ParkSliderHandDriver driver, bool isShowingSpheres)
		{
			Material material = ParkAssetUtility.CreateHandSpheresMaterial();
			ParkHandSpheresView view = driver.gameObject.AddComponent<ParkHandSpheresView>();
			view.Configure(driver, GetBuiltInMesh(PrimitiveType.Sphere), GetBuiltInMesh(PrimitiveType.Cube), material);
			view.enabled = isShowingSpheres;
			return view;
		}

		private static Mesh GetBuiltInMesh(PrimitiveType type)
		{
			GameObject primitive = GameObject.CreatePrimitive(type);
			Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
			Object.DestroyImmediate(primitive);
			return mesh;
		}
#endif

		/// <summary>
		/// What the slider gives back, since hands feel no haptics: sounds from the knob, the glow material on the
		/// knob, notches and end glyphs with the lights that drive it, and the level name label.
		/// </summary>
		private static void AddSliderFeedback(ParkLevelSlider slider, Transform rig, Transform modelRoot,
			ParkLevelController controller, Transform head, int startLevel)
		{
			AddSliderSounds(slider, controller);

			Renderer knob = slider.Handle.GetComponent<Renderer>();
			Renderer[] notches = new Renderer[ParkLevels.Count];
			for (int detent = 0; detent < notches.Length; detent++)
			{
				notches[detent] = FindRenderer(rig, NotchPrefix + detent);
			}

			Renderer natureGlyph = FindRenderer(rig, NatureGlyphName);
			Renderer efficiencyGlyph = FindRenderer(rig, EfficiencyGlyphName);
			Material glow = ParkAssetUtility.CreateGlowMaterial();
			if (glow != null)
			{
				SetMaterial(knob, glow);
				SetMaterial(natureGlyph, glow);
				SetMaterial(efficiencyGlyph, glow);
				foreach (Renderer notch in notches)
				{
					SetMaterial(notch, glow);
				}
			}

			ParkSliderVisuals visuals = slider.gameObject.AddComponent<ParkSliderVisuals>();
			visuals.Configure(slider, controller, knob, notches, natureGlyph, efficiencyGlyph, modelRoot);
			AddLevelLabel(slider, head, startLevel);
		}

		/// <summary>Four 3D sources on the knob, so every slider sound comes from where the hand is.</summary>
		private static void AddSliderSounds(ParkLevelSlider slider, ParkLevelController controller)
		{
			GameObject soundsObject = new GameObject("Slider Sounds");
			Transform sounds = soundsObject.transform;
			sounds.SetParent(slider.Handle, false);
			ParkSliderSounds sliderSounds = soundsObject.AddComponent<ParkSliderSounds>();
			sliderSounds.Configure(slider, controller, CreateSoundSource("Notch", sounds),
				CreateSoundSource("Hand", sounds), CreateSoundSource("Commit", sounds),
				CreateSoundSource("Scrape", sounds));

			// The grab is the press click played lower
			AudioClip press = LoadSoundEffect(PressClipFileName);
			sliderSounds.ConfigureClips(press, LoadSoundEffect(TouchClipFileName), press,
				LoadSoundEffect(ReleaseClipFileName), LoadSoundEffect(LockInClipFileName),
				LoadSoundEffect(ScrapeClipFileName));
		}

		private static AudioSource CreateSoundSource(string name, Transform parent)
		{
			GameObject sourceObject = new GameObject(name);
			sourceObject.transform.SetParent(parent, false);
			AudioSource source = sourceObject.AddComponent<AudioSource>();
			source.playOnAwake = false;
			source.spatialBlend = 1f;
			source.rolloffMode = AudioRolloffMode.Logarithmic;
			source.minDistance = SoundMinDistance;
			source.maxDistance = SoundMaxDistance;
			source.dopplerLevel = 0f;
			return source;
		}

		private static AudioClip LoadSoundEffect(string fileName)
		{
			string path = $"{ParkAssetUtility.SoundEffectsFolder}/{fileName}";
			AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
			if (clip == null)
			{
				Debug.LogWarning($"[Parks] No sound at {path}, so that slider sound stays silent.");
			}

			return clip;
		}

		private static void AddLevelLabel(ParkLevelSlider slider, Transform head, int startLevel)
		{
			GameObject labelObject = new GameObject("Level Label");
			labelObject.transform.SetParent(slider.transform, false);
			labelObject.transform.localPosition = slider.Handle.localPosition + LabelOffsetMeters;

			// The text sits at the empty's origin, where the batch-mode save quirk that zeroes label positions can't
			// move it; the label component moves the empty.
			GameObject textObject = new GameObject("Text");
			textObject.transform.SetParent(labelObject.transform, false);
			TextMeshPro text = textObject.AddComponent<TextMeshPro>();
			text.rectTransform.sizeDelta = LabelSizeMeters;
			text.fontSize = LabelFontSize;
			text.alignment = TextAlignmentOptions.Center;
			text.text = ParkLevels.GetName(startLevel);

			ParkLevelLabel label = labelObject.AddComponent<ParkLevelLabel>();
			label.Configure(slider, text, head);
		}

		private static Renderer FindRenderer(Transform root, string name)
		{
			Transform part = ParkAssetUtility.FindDeep(root, name);
			if (part != null && part.TryGetComponent(out Renderer renderer))
			{
				return renderer;
			}

			Debug.LogWarning($"[Parks] {RigModelName}.fbx has no {name} mesh, so it won't light up.");
			return null;
		}

		private static void SetMaterial(Renderer renderer, Material material)
		{
			if (renderer != null)
			{
				renderer.sharedMaterial = material;
			}
		}

		#endregion
	}
}
