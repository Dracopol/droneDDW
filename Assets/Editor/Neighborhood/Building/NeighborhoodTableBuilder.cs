using System.Collections.Generic;
using System.Linq;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Table;
using DroneDDW.Neighborhood.Versions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if NEIGHBORHOOD_XRI
using UnityEngine.XR.Interaction.Toolkit;
#if NEIGHBORHOOD_XRI_3
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#endif
#endif

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>
    /// The table in front of the seat: the rig from Street_Table.fbx, the three miniatures under its model
    /// root, the three-notch slider with a bigger handle, pushed by any part of a hand (the park's slider input, tuned
    /// by the push settings, with its finger grab switched off), and the lock-in button, a push button held down
    /// to lock the choice in: in the middle in front of the slider on a deeper top, in the table's right corner or on a
    /// stand beside the table (<see cref="NeighborhoodLockButtonPlacement"/>), made in the table's low-poly palette style.
    /// With the button in front the table stands where the table settings say. The experience also builds a practice
    /// table with it: the same table, slider and button in the same place, without the miniatures, for the passthrough
    /// part before the Neighborhood.
    /// </summary>
    internal static class NeighborhoodTableBuilder
    {
        #region Fields

        /// <summary>Where the Neighborhood and the experience put the button: on the table, in front of the slider.</summary>
        internal const NeighborhoodLockButtonPlacement DefaultPlacement = NeighborhoodLockButtonPlacement.TableFront;

        private const string TableModel = "Street_Table";
        private const string MiniaturesModel = "Street_Miniatures";
        private const string MiniaturePrefix = "Miniature_";
        private const string LayoutFileName = "Street_Layout.json";
        private const string TablePartName = "Rig_Table";
        private const string TrackPartName = "Rig_Track";
        private const string GlyphPrefix = "Rig_Glyph_";
        private const string GeneratedFolder = "Generated";
        private const string ButtonMeshPrefix = "Street_LockButton";

        // The button: a slate collar on the surface, the cap rising out of it by its travel, and a ring of segments on the
        // collar's top around the cap. Pushed all the way down, the cap's top stays a little above the collar's.
        private const int ButtonSides = 12;
        private const float CollarHeightMeters = 0.012f;
        private const float CapAboveCollarMeters = 0.002f;
        private const float CapInsetMeters = 0.01f;
        private const float CapDepthInCollarMeters = 0.01f;
        private const int RingSegmentCount = 16;
        private const float RingSegmentGapDegrees = 3f;
        private const int RingSegmentSteps = 2;
        private const float RingMarginMeters = 0.002f;
        private const float RingLiftMeters = 0.0008f;

        // In the corner: as big as fits between the slider's end and the table's edge, with this much room on either side
        private const float CornerClearanceMeters = 0.01f;
        private const float CornerMaxCollarRadiusMeters = 0.044f;
        private const float MinCollarRadiusMeters = 0.025f;
        private const float CornerTravelMeters = 0.015f;

        // On the stand: the Interaction Tests buttons' 12 cm cap and 2 cm travel, on a stand like the table (its 4 cm top,
        // its 5 cm legs) a little beyond the table's right edge
        private const float StandCollarRadiusMeters = 0.07f;
        private const float StandTravelMeters = 0.02f;
        private const float StandGapMeters = 0.03f;
        private const float StandTopHalfWidthMeters = 0.09f;
        private const float StandTopThicknessMeters = 0.04f;
        private const float StandLegWidthMeters = 0.05f;

        // The handle, bigger than the model's (4.4 cm becomes 7 cm, still inside the 7.5 cm deep track). It is pushed, and
        // closing the fingers on it grabs it (the finger grab takes over from the push)
        private const float HandleScale = 1.6f;
        private const bool IsGrabEnabled = true;

        // In front of the slider: the stand's button, this far from the slider's near side and from the table's near edge.
        // The top is made deeper for it, as thick as the model's, on legs as thick as the model's and as far in from its
        // corners.
        private const float FrontGapMeters = 0.05f;
        private const float FrontEdgeMarginMeters = 0.04f;
        private const float TableTopThicknessMeters = 0.04f;
        private const float TableLegWidthMeters = 0.05f;
        private const float TableLegInsetMeters = 0.03f;
        private const string TableMeshPrefix = "Street_Table_Front";

        // The check mark on the cap, as fractions of the cap's radius: a short stroke down to the corner, a long one up
        private const float CheckStrokeWidth = 0.2f;
        private const float CheckHeightMeters = 0.0015f;
        private static readonly Vector2 CheckStart = new Vector2(-0.42f, 0.02f);
        private static readonly Vector2 CheckCorner = new Vector2(-0.12f, -0.3f);
        private static readonly Vector2 CheckEnd = new Vector2(0.46f, 0.3f);

        // The countdown floats this far above the resting cap, as big as fits its box
        private const float CountdownLiftMeters = 0.09f;
        private const float CountdownBoxMeters = 0.1f;
        private const float CountdownMaxFontSize = 1.2f;

        private static readonly Color CountdownColor = new Color(0.95f, 0.76f, 0.19f);

        // The arrows beside the practice slider's handle
        private const float HintArrowRadiusMeters = 0.011f;
        private const float HintArrowHeightMeters = 0.004f;
        private const float HintArrowOffsetMeters = 0.048f;
        private const float HintArrowLiftMeters = 0.0045f;

        private const string SemiBoldFontPath = "Assets/Content/Fonts/Inter/Inter SemiBold SDF.asset";
        private const string DetentPrefix = "Rig_Detent_";
        private const float ButtonSoundMinDistance = 0.5f;
        private const float ButtonSoundMaxDistance = 10f;
        private const string SoundsFolder = "Assets/Content/Audio/SFX";
        private const string TouchClipPath = SoundsFolder + "/Button Touch.wav";
        private const string PressClipPath = SoundsFolder + "/Button Press.wav";
        private const string ReleaseClipPath = SoundsFolder + "/Button Release.wav";
        private const string LockInClipPath = SoundsFolder + "/Lock In.wav";
        private const string NotchClipPath = SoundsFolder + "/Slider Notch.wav";

        // The countdown's ticks for 1, 2 and 3 seconds left, rising in pitch towards the lock-in
        private static readonly string[] CountdownClipPaths =
        {
            SoundsFolder + "/Lock Tick 1.wav", SoundsFolder + "/Lock Tick 2.wav", SoundsFolder + "/Lock Tick 3.wav",
        };

        private static readonly string[] RigParts = { "Rig_Slider", "Rig_Handle", "Rig_ModelRoot", "Rig_ViewCone" };
        private static readonly string[] VersionNames = { "Nature", "Balanced", "Efficiency" };

        #endregion

        #region Internal

        internal static NeighborhoodTableParts Build(string root, NeighborhoodLayout layout, Transform streetRoot, Transform head,
            Transform trackingSpace, NeighborhoodPushSettings settings, NeighborhoodVersion start,
            NeighborhoodLockButtonPlacement placement = DefaultPlacement)
        {
            return BuildTable(root, layout, streetRoot, head, trackingSpace, settings, start, true, placement);
        }

        /// <summary>
        /// The experience's practice table: where the Neighborhood's table stands, its slider on the middle notch, with
        /// the lock-in button and without the miniatures. Empty parts when the Neighborhood content is missing.
        /// </summary>
        internal static NeighborhoodTableParts BuildPractice(Transform head, Transform trackingSpace,
            NeighborhoodLockButtonPlacement placement = DefaultPlacement)
        {
            return BuildOnItsOwn(head, trackingSpace, false, placement);
        }

        /// <summary>
        /// The Neighborhood's whole table, miniatures included, without the street: for the table tuning scene. Empty
        /// parts when the Neighborhood content is missing.
        /// </summary>
        internal static NeighborhoodTableParts BuildForTuning(Transform head, Transform trackingSpace)
        {
            return BuildOnItsOwn(head, trackingSpace, true, DefaultPlacement);
        }

        /// <summary>
        /// The testing view of the spheres the hands push with and the knob's push and grab boxes, on the slider: for
        /// tuning the push settings. Only the table tuning scene adds it.
        /// </summary>
        internal static void AddHandSpheresView(NeighborhoodTableParts table)
        {
#if NEIGHBORHOOD_XRI
            if (table.SliderDriver is not NeighborhoodSliderHandDriver driver)
            {
                return;
            }

            NeighborhoodHandSpheresView view = driver.gameObject.AddComponent<NeighborhoodHandSpheresView>();
            view.Configure(driver, GetBuiltInMesh(PrimitiveType.Sphere), GetBuiltInMesh(PrimitiveType.Cube),
                NeighborhoodAssetUtility.LoadOrCreateHandSpheresMaterial(NeighborhoodAssetUtility.FindRootFolder()));
#endif
        }

        #endregion

        #region Private

        private static Mesh GetBuiltInMesh(PrimitiveType type)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);
            return mesh;
        }

        // A table outside the Neighborhood scene, reading the layout and push settings itself, on the middle notch
        private static NeighborhoodTableParts BuildOnItsOwn(Transform head, Transform trackingSpace, bool hasMiniatures,
            NeighborhoodLockButtonPlacement placement)
        {
            string root = NeighborhoodAssetUtility.FindRootFolder();
            TextAsset json = string.IsNullOrEmpty(root)
                ? null
                : AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{NeighborhoodAssetUtility.LayoutsFolder}/{LayoutFileName}");
            if (json == null)
            {
                Debug.LogError($"[Neighborhood] The table needs the Neighborhood content and {LayoutFileName}.");
                return default;
            }

            NeighborhoodLayout layout = JsonUtility.FromJson<NeighborhoodLayout>(json.text);
            NeighborhoodPushSettings settings = NeighborhoodAssetUtility.LoadOrCreatePushSettings(root);
            return BuildTable(root, layout, null, head, trackingSpace, settings, NeighborhoodVersion.Balanced,
                hasMiniatures, placement);
        }

        private static NeighborhoodTableParts BuildTable(string root, NeighborhoodLayout layout, Transform streetRoot,
            Transform head, Transform trackingSpace, NeighborhoodPushSettings settings, NeighborhoodVersion start,
            bool hasMiniatures, NeighborhoodLockButtonPlacement placement)
        {
            GameObject rigModel = NeighborhoodAssetUtility.LoadModel(root, TableModel);
            GameObject miniatureModel = NeighborhoodAssetUtility.LoadModel(root, MiniaturesModel);
            if (rigModel == null || (hasMiniatures && miniatureModel == null))
            {
                Debug.LogError($"[Neighborhood] {TableModel}.fbx or {MiniaturesModel}.fbx is missing; no table.");
                return default;
            }

            GameObject rig = NeighborhoodBuildUtility.InstantiateUnpacked(rigModel, null,
                hasMiniatures ? "Neighborhood_Table" : "Practice_Table");
            rig.transform.SetPositionAndRotation(layout.Table.Position, Quaternion.identity);
            Transform[] parts = new Transform[RigParts.Length];
            for (int i = 0; i < RigParts.Length; i++)
            {
                parts[i] = NeighborhoodBuildUtility.FindDeep(rig.transform, RigParts[i]);
                if (parts[i] == null)
                {
                    Debug.LogError($"[Neighborhood] {TableModel}.fbx has no {RigParts[i]}; the table is incomplete.");
                    return default;
                }
            }

            Transform slider = parts[0];
            Transform handle = parts[1];
            Transform modelRoot = parts[2];
            NeighborhoodModelPreview preview = null;
            if (hasMiniatures)
            {
                GameObject[] miniatures = AttachMiniatures(miniatureModel, modelRoot);
                preview = modelRoot.gameObject.AddComponent<NeighborhoodModelPreview>();
                preview.Configure(miniatures, parts[3], streetRoot, head);
                preview.ShowVersion(start);
            }
            else
            {
                // No street to preview yet: the practice table has only the slider and the button
                modelRoot.gameObject.SetActive(false);
                parts[3].gameObject.SetActive(false);
            }

            float halfTravel = layout.Table.SliderHalfTravel;
            NeighborhoodSlider versionSlider = slider.gameObject.AddComponent<NeighborhoodSlider>();
            versionSlider.Configure(handle, -halfTravel, halfTravel, NeighborhoodVersions.Count);
            Vector3 handlePosition = handle.localPosition;
            handlePosition.x = Mathf.Lerp(-halfTravel, halfTravel, NeighborhoodVersions.ToBlend(start));
            handle.localPosition = handlePosition;
            ScaleHandle(handle);
            if (handle.TryGetComponent(out BoxCollider handleCollider))
            {
                // The grab box, which the hand driver keeps on the settings' values while playing
                NeighborhoodPushSettings values = NeighborhoodPushSettings.OrDefaults(settings);
                handleCollider.center = values.GrabBoxCenter;
                handleCollider.size = values.GrabBoxSize;
            }

            NeighborhoodHandBones handBones = slider.gameObject.AddComponent<NeighborhoodHandBones>();
            handBones.Configure(trackingSpace, settings);
            Component driver = AddHandInput(versionSlider, handle.gameObject, handBones, settings);
            Material highlight = NeighborhoodAssetUtility.LoadOrCreateHighlightMaterial(root);
            Material props = NeighborhoodAssetUtility.LoadPropsMaterial(root);
            AddSliderLook(versionSlider, handle, driver as Behaviour, highlight);
            NeighborhoodSliderHint hint = hasMiniatures ? null : AddSliderHint(root, handle, props);
            NeighborhoodLockButton lockButton = AddLockButton(root, rig.transform, versionSlider, handBones, settings,
                placement, head, new Vector3(0f, layout.SeatHeight, 0f), highlight, props);
            return new NeighborhoodTableParts(rig, versionSlider, preview, lockButton, handBones, driver, hint);
        }

        private static GameObject[] AttachMiniatures(GameObject miniatureModel, Transform modelRoot)
        {
            GameObject[] miniatures = new GameObject[VersionNames.Length];
            GameObject container = NeighborhoodBuildUtility.InstantiateUnpacked(miniatureModel, null, MiniaturesModel);
            for (int i = 0; i < VersionNames.Length; i++)
            {
                Transform miniature = NeighborhoodBuildUtility.FindDeep(container.transform, MiniaturePrefix + VersionNames[i]);
                if (miniature == null)
                {
                    Debug.LogError($"[Neighborhood] {MiniaturesModel}.fbx has no {MiniaturePrefix}{VersionNames[i]}.");
                    continue;
                }

                // The model root carries the miniature scale; the miniatures are authored in street metres.
                miniature.SetParent(modelRoot, false);
                miniature.localPosition = Vector3.zero;
                miniature.localRotation = Quaternion.identity;
                miniature.localScale = Vector3.one;
                miniatures[i] = miniature.gameObject;
            }

            Object.DestroyImmediate(container);
            return miniatures;
        }

        /// <summary>
        /// Hands push the handle by default and grab it once the fingers close: the interactable is what the finger
        /// grab selects, the hand bones are what pushes, and the driver combines both into one drag. Returns the driver.
        /// </summary>
        private static Component AddHandInput(NeighborhoodSlider slider, GameObject handle, NeighborhoodHandBones handBones,
            NeighborhoodPushSettings settings)
        {
#if NEIGHBORHOOD_XRI
            if (!handle.TryGetComponent(out MeshFilter handleMesh))
            {
                Debug.LogError($"[Neighborhood] {TableModel}.fbx's Rig_Handle has no mesh, so the hands can't push it.");
            }

            XRSimpleInteractable interactable = handle.AddComponent<XRSimpleInteractable>();
            NeighborhoodSliderHandDriver driver = slider.gameObject.AddComponent<NeighborhoodSliderHandDriver>();
            driver.Configure(slider, interactable, handleMesh, handBones, settings, IsGrabEnabled);
            return driver;
#else
            Debug.LogWarning("[Neighborhood] XR Interaction Toolkit is not installed, so the slider has no XR input yet. "
                + "Install it and build again, or call NeighborhoodSlider.BeginDrag / DragTo / EndDrag from your own input.");
            return null;
#endif
        }

        /// <summary>
        /// The slider's look: its handle and notch markers drawn with the highlight material, so they can glow, and a
        /// sound for the notches it passes.
        /// </summary>
        private static void AddSliderLook(NeighborhoodSlider slider, Transform handle, Behaviour handInput,
            Material highlight)
        {
            Renderer handleRenderer = handle.GetComponent<Renderer>();
            List<Renderer> notches = new List<Renderer>();
            foreach (Transform part in slider.GetComponentsInChildren<Transform>(true))
            {
                if (part.name.StartsWith(DetentPrefix) && part.TryGetComponent(out Renderer notch))
                {
                    notches.Add(notch);
                }
            }

            // In the slider's notch order: left to right along its axis
            notches.Sort((first, second) =>
                slider.transform.InverseTransformPoint(first.transform.position).x
                    .CompareTo(slider.transform.InverseTransformPoint(second.transform.position).x));
            foreach (Renderer part in notches.Append(handleRenderer))
            {
                if (part != null && highlight != null)
                {
                    part.sharedMaterial = highlight;
                }
            }

            NeighborhoodSliderLook look = slider.gameObject.AddComponent<NeighborhoodSliderLook>();
            look.Configure(slider, handInput, handleRenderer, notches.ToArray(), CreateAudioSource(slider.gameObject),
                AssetDatabase.LoadAssetAtPath<AudioClip>(NotchClipPath));
        }

        /// <summary>The handle made bigger than the model's, its underside kept on the track.</summary>
        private static void ScaleHandle(Transform handle)
        {
            Vector3 position = handle.localPosition;
            if (handle.TryGetComponent(out MeshFilter mesh) && mesh.sharedMesh != null)
            {
                position.y += mesh.sharedMesh.bounds.min.y * handle.localScale.y * (1f - HandleScale);
            }

            handle.localScale *= HandleScale;
            handle.localPosition = position;
        }

        /// <summary>Two orange arrows either side of the handle, pointing outwards, hidden until the experience shows them.</summary>
        private static NeighborhoodSliderHint AddSliderHint(string root, Transform handle, Material material)
        {
            Mesh arrowMesh = NeighborhoodLowPolyMeshes.Save(NeighborhoodLowPolyMeshes.CreatePrism("Arrow", 3,
                HintArrowRadiusMeters, 0f, HintArrowHeightMeters, NeighborhoodLowPolyMeshes.Orange,
                NeighborhoodLowPolyMeshes.Orange, false), $"{GetGeneratedFolder(root)}/Street_SliderHint_Arrow.asset");
            GameObject hintObject = new GameObject("Slider Hint");
            hintObject.transform.SetParent(handle, false);

            // The prism's point faces -Z; turned a quarter either way it points left or right along the slider
            Transform[] arrows =
            {
                CreatePart("Arrow Left", hintObject.transform, arrowMesh, material).transform,
                CreatePart("Arrow Right", hintObject.transform, arrowMesh, material).transform,
            };
            arrows[0].SetLocalPositionAndRotation(new Vector3(-HintArrowOffsetMeters, HintArrowLiftMeters, 0f),
                Quaternion.Euler(0f, 90f, 0f));
            arrows[1].SetLocalPositionAndRotation(new Vector3(HintArrowOffsetMeters, HintArrowLiftMeters, 0f),
                Quaternion.Euler(0f, -90f, 0f));
            NeighborhoodSliderHint hint = hintObject.AddComponent<NeighborhoodSliderHint>();
            hint.Configure(arrows);
            hintObject.SetActive(false);
            return hint;
        }

        /// <summary>
        /// The lock-in button: in the middle in front of the slider, on a top made deeper for it, with the table moved where
        /// its settings say (from the seat); or on the slider's line, right of its end, in the table's right corner or on a
        /// stand beside the table. A slate collar, the cap a press pushes down rising out of it with a check mark on top, a ring of segments
        /// on the collar around the cap that lights up while the button is held, the countdown above it, and its own sounds;
        /// low-poly meshes in the table's palette. The table's top, track and end glyphs are measured from the model.
        /// </summary>
        private static NeighborhoodLockButton AddLockButton(string root, Transform table, NeighborhoodSlider slider,
            NeighborhoodHandBones handBones, NeighborhoodPushSettings settings, NeighborhoodLockButtonPlacement placement,
            Transform head, Vector3 seat, Material highlight, Material props)
        {
            if (!TryGetBounds(table, NeighborhoodBuildUtility.FindDeep(table, TablePartName), out Bounds tableBounds)
                || !TryGetBounds(table, NeighborhoodBuildUtility.FindDeep(table, TrackPartName), out Bounds trackBounds))
            {
                Debug.LogError($"[Neighborhood] {TableModel}.fbx has no {TablePartName} or {TrackPartName} mesh to place "
                    + "the lock-in button by; no button.");
                return null;
            }

            if (props == null)
            {
                Debug.LogError("[Neighborhood] Street_Mat_Props is missing, so the lock-in button has no material: build the "
                    + "Neighborhood scene once first.");
            }

            // The slider's track and the glyphs at its ends, which the button must stay clear of
            List<Bounds> sliderParts = new List<Bounds> { trackBounds };
            foreach (Transform part in slider.GetComponentsInChildren<Transform>(true))
            {
                if (part.name.StartsWith(GlyphPrefix) && TryGetBounds(table, part, out Bounds glyphBounds))
                {
                    sliderParts.Add(glyphBounds);
                }
            }

            float sliderEndX = trackBounds.max.x;
            foreach (Bounds part in sliderParts)
            {
                sliderEndX = Mathf.Max(sliderEndX, part.max.x);
            }

            float lineZ = table.InverseTransformPoint(slider.transform.position).z;
            float surfaceY = tableBounds.max.y;
            string folder = GetGeneratedFolder(root);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiBoldFontPath);
            float collarRadius;
            float travel;
            Vector3 center;
            if (placement == NeighborhoodLockButtonPlacement.Stand)
            {
                collarRadius = StandCollarRadiusMeters;
                travel = StandTravelMeters;
                center = new Vector3(tableBounds.max.x + StandGapMeters + StandTopHalfWidthMeters, surfaceY, lineZ);

                // Turned to face the seat, the stand's square top reaches further across: it moves out by as much
                float yawRadians = FaceSeat(table, head, center, lineZ).eulerAngles.y * Mathf.Deg2Rad;
                float reachMeters = StandTopHalfWidthMeters * (Mathf.Abs(Mathf.Cos(yawRadians)) + Mathf.Abs(Mathf.Sin(yawRadians)));
                center.x = tableBounds.max.x + StandGapMeters + reachMeters;
                AddStand(folder, table, new Vector3(center.x, tableBounds.min.y, lineZ), FaceSeat(table, head, center, lineZ),
                    surfaceY - tableBounds.min.y, props);
            }
            else if (placement == NeighborhoodLockButtonPlacement.TableFront)
            {
                collarRadius = StandCollarRadiusMeters;
                travel = StandTravelMeters;
                float sliderNearZ = sliderParts.Min(part => part.min.z);
                center = new Vector3(trackBounds.center.x, surfaceY, sliderNearZ - FrontGapMeters - collarRadius);
                tableBounds = BuildFrontTable(root, folder, table, tableBounds,
                    center.z - collarRadius - FrontEdgeMarginMeters, seat);
            }
            else
            {
                float roomMeters = tableBounds.max.x - sliderEndX;
                collarRadius = Mathf.Min(CornerMaxCollarRadiusMeters, roomMeters * 0.5f - CornerClearanceMeters);
                if (collarRadius < MinCollarRadiusMeters)
                {
                    Debug.LogWarning($"[Neighborhood] Only {roomMeters * 100f:0.#} cm between the slider's end and the "
                        + "table's edge: the lock-in button is made smaller than fits there.");
                    collarRadius = MinCollarRadiusMeters;
                }

                travel = CornerTravelMeters;
                center = new Vector3((sliderEndX + tableBounds.max.x) * 0.5f, surfaceY, lineZ);
            }

            CheckFootprint(center, collarRadius, sliderParts,
                placement == NeighborhoodLockButtonPlacement.Stand ? (Bounds?)null : tableBounds);

            GameObject buttonObject = new GameObject("Lock Button");
            Transform button = buttonObject.transform;
            button.SetParent(table, false);
            button.localPosition = center;

            // Facing the seat, so the check mark reads upright and the ring fills clockwise from the far side
            button.localRotation = FaceSeat(table, head, center, lineZ);

            float capRadius = collarRadius - CapInsetMeters;
            float pressedTop = CollarHeightMeters + CapAboveCollarMeters;
            string meshPrefix = $"{folder}/{ButtonMeshPrefix}_{placement}";
            Mesh collarMesh = NeighborhoodLowPolyMeshes.Save(NeighborhoodLowPolyMeshes.CreatePrism("Collar", ButtonSides,
                collarRadius, 0f, CollarHeightMeters, NeighborhoodLowPolyMeshes.Slate, NeighborhoodLowPolyMeshes.Slate,
                false), $"{meshPrefix}_Collar.asset");
            Mesh capMesh = NeighborhoodLowPolyMeshes.Save(NeighborhoodLowPolyMeshes.CreatePrism("Cap", ButtonSides,
                capRadius, -(travel + CapAboveCollarMeters + CapDepthInCollarMeters), 0f, NeighborhoodLowPolyMeshes.White,
                NeighborhoodLowPolyMeshes.White, false), $"{meshPrefix}_Cap.asset");
            Mesh segmentMesh = NeighborhoodLowPolyMeshes.Save(NeighborhoodLowPolyMeshes.CreateRingSegment("Ring Segment",
                capRadius + RingMarginMeters, collarRadius - RingMarginMeters,
                360f / RingSegmentCount - RingSegmentGapDegrees, RingSegmentSteps, CollarHeightMeters + RingLiftMeters,
                NeighborhoodLowPolyMeshes.White), $"{meshPrefix}_RingSegment.asset");

            CreatePart("Collar", button, collarMesh, props);
            MeshRenderer capRenderer = CreatePart("Cap", button, capMesh, highlight);
            capRenderer.transform.localPosition = new Vector3(0f, pressedTop + travel, 0f);
            AddCheckMark(capRenderer.transform, capRadius, meshPrefix, props);

            // Lit clockwise as seen from the seat, from the side furthest away
            Transform ring = new GameObject("Ring").transform;
            ring.SetParent(button, false);
            Renderer[] segments = new Renderer[RingSegmentCount];
            for (int segment = 0; segment < RingSegmentCount; segment++)
            {
                MeshRenderer segmentRenderer = CreatePart($"Segment {segment + 1:00}", ring, segmentMesh, highlight);
                segmentRenderer.transform.localRotation = Quaternion.Euler(0f, (segment + 0.5f) * 360f / RingSegmentCount, 0f);
                segmentRenderer.shadowCastingMode = ShadowCastingMode.Off;
                segments[segment] = segmentRenderer;
            }

            TextMeshPro countdown = CreateText("Countdown", button, font, string.Empty,
                new Vector2(CountdownBoxMeters, CountdownBoxMeters), CountdownMaxFontSize);
            PlaceText(countdown, Vector3.up * (pressedTop + travel + CountdownLiftMeters));
            countdown.color = CountdownColor;

            NeighborhoodLockButton lockButton = buttonObject.AddComponent<NeighborhoodLockButton>();
            lockButton.Configure(slider, handBones, settings, capRenderer.transform, capRenderer, segments, countdown,
                CreateAudioSource(buttonObject));
            AudioClip[] countdownClips = new AudioClip[CountdownClipPaths.Length];
            for (int clip = 0; clip < countdownClips.Length; clip++)
            {
                countdownClips[clip] = AssetDatabase.LoadAssetAtPath<AudioClip>(CountdownClipPaths[clip]);
            }

            lockButton.ConfigureSounds(AssetDatabase.LoadAssetAtPath<AudioClip>(TouchClipPath),
                AssetDatabase.LoadAssetAtPath<AudioClip>(PressClipPath), AssetDatabase.LoadAssetAtPath<AudioClip>(ReleaseClipPath),
                AssetDatabase.LoadAssetAtPath<AudioClip>(LockInClipPath), countdownClips);
            lockButton.ConfigureShape(capRadius, pressedTop, travel);
            return lockButton;
        }

        /// <summary>
        /// A turn about the vertical whose back (-Z) faces the seat, where the rig's head is; straight along the table
        /// when there is no head.
        /// </summary>
        private static Quaternion FaceSeat(Transform table, Transform head, Vector3 position, float lineZ)
        {
            Vector3 seat = head != null ? table.InverseTransformPoint(head.position) : new Vector3(position.x, 0f, lineZ - 1f);
            Vector3 toSeat = Vector3.ProjectOnPlane(seat - position, Vector3.up);
            return toSeat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(-toSeat) : Quaternion.identity;
        }

        /// <summary>A slate check mark lying on the cap's top, readable from the seat: two flat strokes.</summary>
        private static void AddCheckMark(Transform cap, float capRadius, string meshPrefix, Material material)
        {
            (string Name, Vector2 From, Vector2 To)[] strokes =
            {
                ("Check Short", CheckStart, CheckCorner),
                ("Check Long", CheckCorner, CheckEnd),
            };
            float halfWidth = CheckStrokeWidth * capRadius * 0.5f;
            foreach ((string name, Vector2 from, Vector2 to) in strokes)
            {
                Vector3 start = new Vector3(from.x, 0f, from.y) * capRadius;
                Vector3 end = new Vector3(to.x, 0f, to.y) * capRadius;

                // Each stroke runs a little past its ends so the two meet in a closed corner
                float halfLength = Vector3.Distance(start, end) * 0.5f + halfWidth;
                (Vector3 Min, Vector3 Max)[] box =
                {
                    (new Vector3(-halfWidth, 0f, -halfLength), new Vector3(halfWidth, CheckHeightMeters, halfLength)),
                };
                Mesh mesh = NeighborhoodLowPolyMeshes.Save(NeighborhoodLowPolyMeshes.CreateBoxes(name, box,
                    NeighborhoodLowPolyMeshes.Slate, NeighborhoodLowPolyMeshes.Slate),
                    $"{meshPrefix}_{name.Replace(" ", string.Empty)}.asset");
                MeshRenderer stroke = CreatePart(name, cap, mesh, material);
                stroke.transform.SetLocalPositionAndRotation((start + end) * 0.5f, Quaternion.LookRotation(end - start));
                stroke.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// A stand for the button in the table's style: one leg as thick as the table's and a top as thick as the table's,
        /// wood with a cream top, from the floor up to the table's height.
        /// </summary>
        private static void AddStand(string folder, Transform table, Vector3 footCenter, Quaternion facing,
            float heightMeters, Material material)
        {
            float legHalf = StandLegWidthMeters * 0.5f;
            float topBottom = heightMeters - StandTopThicknessMeters;
            (Vector3 Min, Vector3 Max)[] boxes =
            {
                (new Vector3(-legHalf, 0f, -legHalf), new Vector3(legHalf, topBottom, legHalf)),
                (new Vector3(-StandTopHalfWidthMeters, topBottom, -StandTopHalfWidthMeters),
                    new Vector3(StandTopHalfWidthMeters, heightMeters, StandTopHalfWidthMeters)),
            };

            Mesh mesh = NeighborhoodLowPolyMeshes.Save(NeighborhoodLowPolyMeshes.CreateBoxes("Stand", boxes,
                NeighborhoodLowPolyMeshes.Wood, NeighborhoodLowPolyMeshes.Cream), $"{folder}/{ButtonMeshPrefix}_Stand.asset");
            MeshRenderer stand = CreatePart("Lock Button Stand", table, mesh, material);
            stand.transform.SetLocalPositionAndRotation(footCenter, facing);
        }

        /// <summary>
        /// The model's top made deeper towards the seat, down to <paramref name="nearZ"/>, in its colours: the same width,
        /// far edge, height and thickness, drawn by Rig_Table itself. Its legs are a mesh of their own, one metre high, that
        /// the table's placement stretches down to the floor; the placement then moves the table where the settings say.
        /// Returns the new top's bounds in the table's space.
        /// </summary>
        private static Bounds BuildFrontTable(string root, string folder, Transform table, Bounds modelTop, float nearZ,
            Vector3 seat)
        {
            Transform top = NeighborhoodBuildUtility.FindDeep(table, TablePartName);
            Vector3 min = new Vector3(modelTop.min.x, modelTop.max.y - TableTopThicknessMeters, nearZ);
            Vector3 max = new Vector3(modelTop.max.x, modelTop.max.y, modelTop.max.z);
            Mesh topMesh = NeighborhoodLowPolyMeshes.Save(NeighborhoodLowPolyMeshes.CreateBoxes("Table Top",
                new[] { (min, max) }, NeighborhoodLowPolyMeshes.Wood, NeighborhoodLowPolyMeshes.Cream),
                $"{folder}/{TableMeshPrefix}_Top.asset");

            // The generated meshes are in the table's space, so the part sits on the table's origin
            top.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            top.localScale = Vector3.one;
            top.GetComponent<MeshFilter>().sharedMesh = topMesh;

            float[] legX = { min.x + TableLegInsetMeters, max.x - TableLegInsetMeters - TableLegWidthMeters };
            float[] legZ = { min.z + TableLegInsetMeters, max.z - TableLegInsetMeters - TableLegWidthMeters };
            List<(Vector3 Min, Vector3 Max)> legs = new List<(Vector3 Min, Vector3 Max)>();
            foreach (float x in legX)
            {
                foreach (float z in legZ)
                {
                    legs.Add((new Vector3(x, 0f, z), new Vector3(x + TableLegWidthMeters, 1f, z + TableLegWidthMeters)));
                }
            }

            Mesh legsMesh = NeighborhoodLowPolyMeshes.Save(NeighborhoodLowPolyMeshes.CreateBoxes("Table Legs", legs,
                NeighborhoodLowPolyMeshes.Wood, NeighborhoodLowPolyMeshes.Wood), $"{folder}/{TableMeshPrefix}_Legs.asset");
            MeshRenderer legsRenderer = CreatePart("Rig_Table_Legs", top, legsMesh,
                top.GetComponent<MeshRenderer>().sharedMaterial);

            NeighborhoodTablePlacement placement = table.gameObject.AddComponent<NeighborhoodTablePlacement>();
            placement.Configure(NeighborhoodAssetUtility.LoadOrCreateTableSettings(root), seat, nearZ, max.y,
                TableTopThicknessMeters, legsRenderer.transform);
            placement.Apply();
            return new Bounds((min + max) * 0.5f, max - min);
        }

        /// <summary>World-space text in a box, as big as fits it (up to the given size), centred.</summary>
        private static TextMeshPro CreateText(string name, Transform parent, TMP_FontAsset font, string text, Vector2 size,
            float maxFontSize)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            TextMeshPro textMesh = textObject.AddComponent<TextMeshPro>();
            textMesh.rectTransform.sizeDelta = size;
            if (font != null)
            {
                textMesh.font = font;
            }

            textMesh.text = text;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.textWrappingMode = TextWrappingModes.NoWrap;
            textMesh.enableAutoSizing = true;
            textMesh.fontSizeMin = 0.01f;
            textMesh.fontSizeMax = maxFontSize;
            return textMesh;
        }

        /// <summary>
        /// Puts a label at a local position through its anchored position, which is what the scene stores for it. Set
        /// through localPosition alone, the empty countdown came out of a batch build back at its parent's origin,
        /// inside the button.
        /// </summary>
        private static void PlaceText(TextMeshPro text, Vector3 localPosition)
        {
            text.rectTransform.anchoredPosition3D = localPosition;
            text.rectTransform.localPosition = localPosition;
        }

        private static AudioSource CreateAudioSource(GameObject host)
        {
            AudioSource audio = host.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.dopplerLevel = 0f;
            audio.minDistance = ButtonSoundMinDistance;
            audio.maxDistance = ButtonSoundMaxDistance;
            return audio;
        }

        private static string GetGeneratedFolder(string root)
        {
            return NeighborhoodAssetUtility.EnsureFolder(
                NeighborhoodAssetUtility.EnsureFolder(root, NeighborhoodAssetUtility.ModelsFolder), GeneratedFolder);
        }

        /// <summary>A part of the button: the mesh, drawn with the material, no collider (the hands are tested in code).</summary>
        private static MeshRenderer CreatePart(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>A part's mesh bounds in another transform's space (e.g. the table's), around all eight corners.</summary>
        private static bool TryGetBounds(Transform space, Transform part, out Bounds bounds)
        {
            bounds = default;
            if (part == null || !part.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
            {
                return false;
            }

            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 toSpace = space.worldToLocalMatrix * part.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = toSpace.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                if (corner == 0)
                {
                    bounds = new Bounds(point, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }

            return true;
        }

        /// <summary>
        /// Logs an error when the button's collar would overlap the slider's track or end glyphs or, on the table, stick
        /// out over its edge.
        /// </summary>
        private static void CheckFootprint(Vector3 center, float radius, List<Bounds> sliderParts, Bounds? surface)
        {
            foreach (Bounds part in sliderParts)
            {
                float dx = Mathf.Max(part.min.x - center.x, 0f, center.x - part.max.x);
                float dz = Mathf.Max(part.min.z - center.z, 0f, center.z - part.max.z);
                if (dx * dx + dz * dz < radius * radius)
                {
                    Debug.LogError($"[Neighborhood] The lock-in button (at x {center.x:0.###}, {radius * 200f:0.#} cm wide) "
                        + $"would overlap the slider, which reaches x {part.max.x:0.###}.");
                }
            }

            if (surface is Bounds top && (center.x - radius < top.min.x || center.x + radius > top.max.x
                || center.z - radius < top.min.z || center.z + radius > top.max.z))
            {
                Debug.LogError("[Neighborhood] The lock-in button would stick out over the table's edge.");
            }
        }

        #endregion
    }
}
