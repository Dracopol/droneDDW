using System.Collections.Generic;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Table;
using DroneDDW.Neighborhood.Versions;
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
    /// root, the three-notch slider, pushed by any part of a hand and grabbed once the fingers close
    /// (the same input as the park's slider, tuned by the push settings), and the lock-in button right of the slider's
    /// end, a push button held down to lock the choice in: in the table's right corner or on a stand beside the table
    /// (<see cref="NeighborhoodLockButtonPlacement"/>), made in the table's low-poly palette style. The experience also
    /// builds a practice table with it: the same table, slider and button in the same place, without the miniatures, for
    /// the passthrough part before the Neighborhood.
    /// </summary>
    internal static class NeighborhoodTableBuilder
    {
        #region Fields

        /// <summary>Where the Neighborhood and the experience put the button, until the placement tests decide.</summary>
        internal const NeighborhoodLockButtonPlacement DefaultPlacement = NeighborhoodLockButtonPlacement.TableCorner;

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

        private const float ButtonSoundMinDistance = 0.5f;
        private const float ButtonSoundMaxDistance = 10f;
        private const string TouchClipPath = "Assets/Content/Audio/SFX/Button Touch.wav";
        private const string PressClipPath = "Assets/Content/Audio/SFX/Button Press.wav";
        private const string ReleaseClipPath = "Assets/Content/Audio/SFX/Button Release.wav";
        private const string LockInClipPath = "Assets/Content/Audio/SFX/Lock In.wav";

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
            string root = NeighborhoodAssetUtility.FindRootFolder();
            TextAsset json = string.IsNullOrEmpty(root)
                ? null
                : AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{NeighborhoodAssetUtility.LayoutsFolder}/{LayoutFileName}");
            if (json == null)
            {
                Debug.LogError($"[Neighborhood] The practice table needs the Neighborhood content and {LayoutFileName}.");
                return default;
            }

            NeighborhoodLayout layout = JsonUtility.FromJson<NeighborhoodLayout>(json.text);
            NeighborhoodPushSettings settings = NeighborhoodAssetUtility.LoadOrCreatePushSettings(root);
            return BuildTable(root, layout, null, head, trackingSpace, settings, NeighborhoodVersion.Balanced, false,
                placement);
        }

        #endregion

        #region Private

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
            NeighborhoodLockButton lockButton = AddLockButton(root, rig.transform, versionSlider, handBones, settings,
                placement);
            return new NeighborhoodTableParts(rig, versionSlider, preview, lockButton, handBones, driver);
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
            driver.Configure(slider, interactable, handleMesh, handBones, settings);
            return driver;
#else
            Debug.LogWarning("[Neighborhood] XR Interaction Toolkit is not installed, so the slider has no XR input yet. "
                + "Install it and build again, or call NeighborhoodSlider.BeginDrag / DragTo / EndDrag from your own input.");
            return null;
#endif
        }

        /// <summary>
        /// The lock-in button on the slider's line, right of its end: in the table's right corner, or on a stand beside the
        /// table. A slate collar, the cap a press pushes down rising out of it, a ring of segments on the collar around the
        /// cap that lights up while the button is held, and its own sounds; low-poly meshes in the table's palette, shaded
        /// with the table's own material. The table's top, track and end glyphs are measured from the model.
        /// </summary>
        private static NeighborhoodLockButton AddLockButton(string root, Transform table, NeighborhoodSlider slider,
            NeighborhoodHandBones handBones, NeighborhoodPushSettings settings, NeighborhoodLockButtonPlacement placement)
        {
            if (!TryGetBounds(table, NeighborhoodBuildUtility.FindDeep(table, TablePartName), out Bounds tableBounds)
                || !TryGetBounds(table, NeighborhoodBuildUtility.FindDeep(table, TrackPartName), out Bounds trackBounds))
            {
                Debug.LogError($"[Neighborhood] {TableModel}.fbx has no {TablePartName} or {TrackPartName} mesh to place "
                    + "the lock-in button by; no button.");
                return null;
            }

            Material material = NeighborhoodAssetUtility.LoadPropsMaterial(root);
            if (material == null)
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
            string folder = NeighborhoodAssetUtility.EnsureFolder(
                NeighborhoodAssetUtility.EnsureFolder(root, NeighborhoodAssetUtility.ModelsFolder), GeneratedFolder);
            float collarRadius;
            float travel;
            Vector3 center;
            if (placement == NeighborhoodLockButtonPlacement.Stand)
            {
                collarRadius = StandCollarRadiusMeters;
                travel = StandTravelMeters;
                center = new Vector3(tableBounds.max.x + StandGapMeters + StandTopHalfWidthMeters, surfaceY, lineZ);
                AddStand(folder, table, new Vector3(center.x, tableBounds.min.y, lineZ), surfaceY - tableBounds.min.y,
                    material);
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
                placement == NeighborhoodLockButtonPlacement.TableCorner ? tableBounds : (Bounds?)null);

            GameObject buttonObject = new GameObject("Lock Button");
            Transform button = buttonObject.transform;
            button.SetParent(table, false);
            button.localPosition = center;
            button.localRotation = Quaternion.identity;

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

            CreatePart("Collar", button, collarMesh, material);
            MeshRenderer capRenderer = CreatePart("Cap", button, capMesh, material);
            capRenderer.transform.localPosition = new Vector3(0f, pressedTop + travel, 0f);

            // Lit clockwise as seen from the seat, from the side furthest away
            Transform ring = new GameObject("Ring").transform;
            ring.SetParent(button, false);
            Renderer[] segments = new Renderer[RingSegmentCount];
            for (int segment = 0; segment < RingSegmentCount; segment++)
            {
                MeshRenderer segmentRenderer = CreatePart($"Segment {segment + 1:00}", ring, segmentMesh, material);
                segmentRenderer.transform.localRotation = Quaternion.Euler(0f, (segment + 0.5f) * 360f / RingSegmentCount, 0f);
                segmentRenderer.shadowCastingMode = ShadowCastingMode.Off;
                segments[segment] = segmentRenderer;
            }

            AudioSource audio = buttonObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.dopplerLevel = 0f;
            audio.minDistance = ButtonSoundMinDistance;
            audio.maxDistance = ButtonSoundMaxDistance;

            NeighborhoodLockButton lockButton = buttonObject.AddComponent<NeighborhoodLockButton>();
            lockButton.Configure(slider, handBones, settings, capRenderer.transform, capRenderer, segments, audio,
                AssetDatabase.LoadAssetAtPath<AudioClip>(TouchClipPath), AssetDatabase.LoadAssetAtPath<AudioClip>(PressClipPath),
                AssetDatabase.LoadAssetAtPath<AudioClip>(ReleaseClipPath), AssetDatabase.LoadAssetAtPath<AudioClip>(LockInClipPath));
            lockButton.ConfigureShape(capRadius, pressedTop, travel);
            return lockButton;
        }

        /// <summary>
        /// A stand for the button in the table's style: one leg as thick as the table's and a top as thick as the table's,
        /// wood with a cream top, from the floor up to the table's height.
        /// </summary>
        private static void AddStand(string folder, Transform table, Vector3 footCenter, float heightMeters, Material material)
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
            stand.transform.localPosition = footCenter;
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
