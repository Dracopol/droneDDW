using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Import
{
    /// <summary>
    /// Import settings for the street files (names starting with Street_):
    /// - Street_Shared.fbx: static (batching, occludee, reflection probes, contributes GI) with lightmap UVs.
    /// - Street_Elements, Street_Miniatures, Street_Table: they move between versions, so not static.
    /// - Street_Grandma, Street_Worker, Street_Student: Generic rig built from the file, bones kept as objects
    ///   (props and the leash hang on them), every clip looping and named without the "Rig|" prefix.
    /// - Street_Palette: point filtered and uncompressed; Street_LightPool: linear mask; Street_Sky_Dusk: no mips,
    ///   wraps round horizontally.
    /// Colliders come from the "collider" custom property the export writes (the table and the slider handle).
    /// Street_Shared's wall tops are raised to meet the roofs (NeighborhoodEaveGaps).
    /// </summary>
    internal sealed class NeighborhoodModelPostprocessor : AssetPostprocessor
    {
        // Raise it whenever what this does to the files changes, so every machine imports them again
        public override uint GetVersion()
        {
            return 3;
        }

        #region Fields

        private const string Prefix = "Street_";
        private const string ModelExtension = ".fbx";
        private const string SharedModel = "Street_Shared";
        private const string PaletteTexture = "Street_Palette";
        private const string LightPoolTexture = "Street_LightPool";
        private const string SkyTexturePrefix = "Street_Sky_";
        private const string CharacterTexture = "Street_Characters";
        private const string ColliderKey = "collider";
        private const string BoxShape = "box";
        private const char TakeSeparator = '|';

        private static readonly string[] CharacterModels = { "Street_Grandma", "Street_Worker", "Street_Student" };

        private const StaticEditorFlags SharedStaticFlags = StaticEditorFlags.ContributeGI
            | StaticEditorFlags.BatchingStatic
            | StaticEditorFlags.OccludeeStatic
            | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.ReflectionProbeStatic;

        private readonly List<GameObject> _pendingBoxes = new List<GameObject>();

        #endregion

        #region Properties

        private string FileName => Path.GetFileNameWithoutExtension(assetPath);
        private bool IsNeighborhoodModel => IsNeighborhoodModelPath(assetPath);
        private bool IsCharacter => IsNeighborhoodModel && Array.IndexOf(CharacterModels, FileName) >= 0;

        #endregion

        #region Internal

        internal static bool IsNeighborhoodModelPath(string path)
        {
            return path.EndsWith(ModelExtension, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(path).StartsWith(Prefix, StringComparison.Ordinal);
        }

        internal static bool IsCharacterModelName(string fileName)
        {
            return Array.IndexOf(CharacterModels, fileName) >= 0;
        }

        /// <summary>"Grandma_Rig|Pose_Nature" -> "Pose_Nature".</summary>
        internal static string CleanClipName(string takeName)
        {
            int separator = takeName.LastIndexOf(TakeSeparator);
            return separator >= 0 ? takeName.Substring(separator + 1) : takeName;
        }

        #endregion

        #region Private

        private void OnPreprocessModel()
        {
            if (!IsNeighborhoodModel)
            {
                return;
            }

            ModelImporter importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.generateSecondaryUV = FileName == SharedModel;
            if (IsCharacter)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                importer.animationRotationError = 0.1f;
                importer.animationPositionError = 0.1f;
                importer.animationScaleError = 0.1f;
                return;
            }

            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsCharacter)
            {
                return;
            }

            ModelImporter importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = CleanClipName(clip.takeName);
                clip.loopTime = true;
                clip.loopPose = false;
            }

            importer.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            string fileName = FileName;
            if (!fileName.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            if (fileName == PaletteTexture)
            {
                // Every face samples the centre of a flat colour swatch, so no filtering or mips.
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = true;
                importer.maxTextureSize = 256;
            }
            else if (fileName == LightPoolTexture)
            {
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 128;
            }
            else if (fileName.StartsWith(SkyTexturePrefix, StringComparison.Ordinal))
            {
                // The panoramic skybox samples one 2D image; mips would show a seam where it wraps.
                importer.mipmapEnabled = false;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            else if (fileName == CharacterTexture)
            {
                importer.maxTextureSize = 1024;
            }
        }

        private void OnPostprocessGameObjectWithUserProperties(GameObject gameObject, string[] propertyNames, object[] values)
        {
            if (!IsNeighborhoodModel)
            {
                return;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (string.Equals(propertyNames[i], ColliderKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Convert.ToString(values[i], CultureInfo.InvariantCulture), BoxShape, StringComparison.OrdinalIgnoreCase))
                {
                    _pendingBoxes.Add(gameObject);
                }
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsNeighborhoodModel)
            {
                return;
            }

            foreach (GameObject target in _pendingBoxes)
            {
                // Import-time setup: nothing is serialized yet, so components are looked up here.
                if (target.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
                {
                    BoxCollider box = target.AddComponent<BoxCollider>();
                    box.center = meshFilter.sharedMesh.bounds.center;
                    box.size = meshFilter.sharedMesh.bounds.size;
                }
            }

            _pendingBoxes.Clear();
            if (FileName != SharedModel)
            {
                return;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, SharedStaticFlags);
            }

            int closed = 0;
            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh != null)
                {
                    closed += NeighborhoodEaveGaps.Close(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix);
                }
            }

            Debug.Log($"[Neighborhood] {SharedModel}: raised {closed} wall corners to meet the roofs above them.");
        }

        #endregion
    }
}
