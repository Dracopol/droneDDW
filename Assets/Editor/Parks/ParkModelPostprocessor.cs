using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Import
{
	/// <summary>
	/// Prepares the park FBX files on import: lightmap UVs, static flags, and colliders / light-probe
	/// lighting taken from the custom properties the Blender build script writes on each object
	/// (collider = box | mesh | capsule, col_radius, col_height, gi = probes).
	/// Files named Park_Levels_* (the level kit, the table rig and the miniatures) move at runtime,
	/// so they get colliders but no static flags. Also sets point filtering on both palette textures
	/// and streamed Vorbis on the Park_Ambience_* loops.
	/// </summary>
	internal sealed class ParkModelPostprocessor : AssetPostprocessor
	{
		#region Entities

		private readonly struct ColliderRequest
		{
			public ColliderRequest(string shape, float radius, float height, bool useLightProbes)
			{
				Shape = shape;
				Radius = radius;
				Height = height;
				UseLightProbes = useLightProbes;
			}

			public string Shape { get; }
			public float Radius { get; }
			public float Height { get; }
			public bool UseLightProbes { get; }
		}

		#endregion

		#region Fields

		private const string ModelPrefix = "Park_";
		private const string DynamicModelPrefix = "Park_Levels_";
		private const string ModelExtension = ".fbx";
		private const string PalettePrefix = "Park_Palette";
		private const string PaletteExtension = ".png";
		private const string AmbiencePrefix = "Park_Ambience_";
		private const string AndroidPlatform = "Android";
		private const float AmbienceQuality = 0.7f;
		private const string ColliderKey = "collider";
		private const string RadiusKey = "col_radius";
		private const string HeightKey = "col_height";
		private const string GlobalIlluminationKey = "gi";
		private const string LightProbesValue = "probes";
		private const string BoxShape = "box";
		private const string MeshShape = "mesh";
		private const string CapsuleShape = "capsule";

		private const StaticEditorFlags ParkStaticFlags = StaticEditorFlags.ContributeGI
			| StaticEditorFlags.BatchingStatic
			| StaticEditorFlags.OccludeeStatic
			| StaticEditorFlags.ReflectionProbeStatic;

		private readonly Dictionary<GameObject, ColliderRequest> _pendingRequests = new Dictionary<GameObject, ColliderRequest>();

		#endregion

		#region Properties

		private bool IsParkModel => IsParkModelPath(assetPath);

		#endregion

		#region Internal

		internal static bool IsParkModelPath(string path)
		{
			return path.EndsWith(ModelExtension, StringComparison.OrdinalIgnoreCase)
				&& Path.GetFileName(path).StartsWith(ModelPrefix, StringComparison.Ordinal);
		}

		/// <summary>Level park pieces animate between levels, so they must not be static.</summary>
		internal static bool IsDynamicModelPath(string path)
		{
			return IsParkModelPath(path) && Path.GetFileName(path).StartsWith(DynamicModelPrefix, StringComparison.Ordinal);
		}

		internal static bool IsPalettePath(string path)
		{
			string fileName = Path.GetFileName(path);
			return fileName.StartsWith(PalettePrefix, StringComparison.Ordinal)
				&& fileName.EndsWith(PaletteExtension, StringComparison.OrdinalIgnoreCase);
		}

		internal static bool IsAmbiencePath(string path)
		{
			return Path.GetFileName(path).StartsWith(AmbiencePrefix, StringComparison.Ordinal);
		}

		#endregion

		#region Private

		private void OnPreprocessModel()
		{
			if (!IsParkModel)
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
			importer.importAnimation = false;
			importer.animationType = ModelImporterAnimationType.None;
			importer.importNormals = ModelImporterNormals.Import;
			importer.meshCompression = ModelImporterMeshCompression.Off;
			importer.isReadable = false;
			importer.addCollider = false;
			importer.generateSecondaryUV = true;
		}

		private void OnPreprocessTexture()
		{
			if (!IsPalettePath(assetPath))
			{
				return;
			}

			// Every face samples the centre of a flat colour swatch, so no filtering or mips.
			TextureImporter importer = (TextureImporter)assetImporter;
			importer.mipmapEnabled = false;
			importer.filterMode = FilterMode.Point;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.sRGBTexture = true;
			importer.maxTextureSize = 256;
		}

		private void OnPreprocessAudio()
		{
			if (!IsAmbiencePath(assetPath))
			{
				return;
			}

			// Minute-long stereo loops: stream and decode them as they play instead of holding them in memory.
			AudioImporter importer = (AudioImporter)assetImporter;
			importer.forceToMono = false;
			AudioImporterSampleSettings settings = importer.defaultSampleSettings;
			settings.loadType = AudioClipLoadType.Streaming;
			settings.compressionFormat = AudioCompressionFormat.Vorbis;
			settings.quality = AmbienceQuality;
			importer.defaultSampleSettings = settings;

			// The Quest builds for Android; without an override there it uses the settings above.
			importer.ClearSampleSettingOverride(AndroidPlatform);
		}

		private void OnPostprocessGameObjectWithUserProperties(GameObject gameObject, string[] propertyNames, object[] values)
		{
			if (!IsParkModel)
			{
				return;
			}

			ColliderRequest request = ReadRequest(propertyNames, values);
			if (TryApplyRequest(gameObject, request))
			{
				return;
			}

			// Mesh components not attached yet: finish in OnPostprocessModel.
			_pendingRequests[gameObject] = request;
		}

		private void OnPostprocessModel(GameObject root)
		{
			if (!IsParkModel)
			{
				return;
			}

			foreach (KeyValuePair<GameObject, ColliderRequest> pending in _pendingRequests)
			{
				if (!TryApplyRequest(pending.Key, pending.Value))
				{
					Debug.LogWarning($"[Parks] {assetPath}: no mesh on '{pending.Key.name}', collider skipped.");
				}
			}

			_pendingRequests.Clear();
			if (IsDynamicModelPath(assetPath))
			{
				return;
			}

			foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
			{
				GameObjectUtility.SetStaticEditorFlags(child.gameObject, ParkStaticFlags);
			}
		}

		private static ColliderRequest ReadRequest(string[] propertyNames, object[] values)
		{
			string shape = string.Empty;
			float radius = 0f;
			float height = 0f;
			bool useLightProbes = false;
			for (int i = 0; i < propertyNames.Length; i++)
			{
				string key = propertyNames[i];
				object value = values[i];
				if (string.Equals(key, ColliderKey, StringComparison.OrdinalIgnoreCase))
				{
					shape = Convert.ToString(value, CultureInfo.InvariantCulture).Trim().ToLowerInvariant();
				}
				else if (string.Equals(key, RadiusKey, StringComparison.OrdinalIgnoreCase))
				{
					radius = Convert.ToSingle(value, CultureInfo.InvariantCulture);
				}
				else if (string.Equals(key, HeightKey, StringComparison.OrdinalIgnoreCase))
				{
					height = Convert.ToSingle(value, CultureInfo.InvariantCulture);
				}
				else if (string.Equals(key, GlobalIlluminationKey, StringComparison.OrdinalIgnoreCase))
				{
					useLightProbes = string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), LightProbesValue,
						StringComparison.OrdinalIgnoreCase);
				}
			}

			return new ColliderRequest(shape, radius, height, useLightProbes);
		}

		private static bool TryApplyRequest(GameObject target, ColliderRequest request)
		{
			// Import-time setup: there is nothing to serialize yet, so components are looked up here.
			if (!target.TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
			{
				return false;
			}

			Mesh mesh = meshFilter.sharedMesh;
			switch (request.Shape)
			{
				case BoxShape:
					BoxCollider box = target.AddComponent<BoxCollider>();
					box.center = mesh.bounds.center;
					box.size = mesh.bounds.size;
					break;
				case MeshShape:
					MeshCollider meshCollider = target.AddComponent<MeshCollider>();
					meshCollider.sharedMesh = mesh;
					break;
				case CapsuleShape:
					CapsuleCollider capsule = target.AddComponent<CapsuleCollider>();
					float height = Mathf.Max(request.Height, request.Radius * 2f);
					capsule.direction = 1;
					capsule.radius = request.Radius;
					capsule.height = height;
					capsule.center = new Vector3(0f, height * 0.5f, 0f);
					break;
			}

			if (request.UseLightProbes && target.TryGetComponent(out MeshRenderer meshRenderer))
			{
				meshRenderer.receiveGI = ReceiveGI.LightProbes;
			}

			return true;
		}

		#endregion
	}
}
