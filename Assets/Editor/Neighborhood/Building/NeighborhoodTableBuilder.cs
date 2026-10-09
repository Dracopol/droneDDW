using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Table;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;
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
    /// root, and the three-notch slider, pushed by any part of a hand and grabbed once the fingers close
    /// (the same input as the park's slider, tuned by the push settings).
    /// </summary>
    internal static class NeighborhoodTableBuilder
    {
        #region Fields

        private const string TableModel = "Street_Table";
        private const string MiniaturesModel = "Street_Miniatures";
        private const string MiniaturePrefix = "Miniature_";

        private static readonly string[] RigParts = { "Rig_Slider", "Rig_Handle", "Rig_ModelRoot", "Rig_ViewCone" };
        private static readonly string[] VersionNames = { "Nature", "Balanced", "Efficiency" };

        #endregion

        #region Internal

        internal static NeighborhoodTableParts Build(string root, NeighborhoodLayout layout, Transform streetRoot, Transform head,
            Transform trackingSpace, NeighborhoodPushSettings settings, NeighborhoodVersion start)
        {
            GameObject rigModel = NeighborhoodAssetUtility.LoadModel(root, TableModel);
            GameObject miniatureModel = NeighborhoodAssetUtility.LoadModel(root, MiniaturesModel);
            if (rigModel == null || miniatureModel == null)
            {
                Debug.LogError($"[Neighborhood] {TableModel}.fbx or {MiniaturesModel}.fbx is missing; no table.");
                return new NeighborhoodTableParts(null, null);
            }

            GameObject rig = NeighborhoodBuildUtility.InstantiateUnpacked(rigModel, null, "Neighborhood_Table");
            rig.transform.SetPositionAndRotation(layout.Table.Position, Quaternion.identity);
            Transform[] parts = new Transform[RigParts.Length];
            for (int i = 0; i < RigParts.Length; i++)
            {
                parts[i] = NeighborhoodBuildUtility.FindDeep(rig.transform, RigParts[i]);
                if (parts[i] == null)
                {
                    Debug.LogError($"[Neighborhood] {TableModel}.fbx has no {RigParts[i]}; the table is incomplete.");
                    return new NeighborhoodTableParts(null, null);
                }
            }

            Transform slider = parts[0];
            Transform handle = parts[1];
            Transform modelRoot = parts[2];
            GameObject[] miniatures = AttachMiniatures(miniatureModel, modelRoot);

            NeighborhoodModelPreview preview = modelRoot.gameObject.AddComponent<NeighborhoodModelPreview>();
            preview.Configure(miniatures, parts[3], streetRoot, head);
            preview.ShowVersion(start);

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

            AddHandInput(versionSlider, handle.gameObject, trackingSpace, settings);
            return new NeighborhoodTableParts(versionSlider, preview);
        }

        #endregion

        #region Private

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
        /// grab selects, the hand bones are what pushes, and the driver combines both into one drag.
        /// </summary>
        private static void AddHandInput(NeighborhoodSlider slider, GameObject handle, Transform trackingSpace,
            NeighborhoodPushSettings settings)
        {
#if NEIGHBORHOOD_XRI
            if (!handle.TryGetComponent(out MeshFilter handleMesh))
            {
                Debug.LogError($"[Neighborhood] {TableModel}.fbx's Rig_Handle has no mesh, so the hands can't push it.");
            }

            XRSimpleInteractable interactable = handle.AddComponent<XRSimpleInteractable>();
            NeighborhoodHandBones handBones = slider.gameObject.AddComponent<NeighborhoodHandBones>();
            handBones.Configure(trackingSpace, settings);
            NeighborhoodSliderHandDriver driver = slider.gameObject.AddComponent<NeighborhoodSliderHandDriver>();
            driver.Configure(slider, interactable, handleMesh, handBones, settings);
#else
            Debug.LogWarning("[Neighborhood] XR Interaction Toolkit is not installed, so the slider has no XR input yet. "
                + "Install it and build again, or call NeighborhoodSlider.BeginDrag / DragTo / EndDrag from your own input.");
#endif
        }

        #endregion
    }
}
