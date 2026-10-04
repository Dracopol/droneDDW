using DroneDDW.NarratorOrb.Rendering;
using DroneDDW.NarratorOrb.Settings;
using DroneDDW.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DroneDDW.NarratorOrb.Editor
{
	// Menu: DroneDDW > Narrator Orb > Build Orb Assets. Safe to run again: every asset keeps its GUID.
	// 1. Imports the test narration (Narrator_TestSample.wav) as PCM, Decompress On Load, at its own rate, so its
	//    samples match the preview's
	// 2. Creates the settings asset if it is missing (never overwrites it)
	// 3. Creates the layout asset if it is missing and bakes its meshes (at the layout's particle count scale)
	// 4. Creates the four materials if they are missing
	// 5. Builds the Narrator Orb prefab: the controller, the voice with its live tap, and the four parts
	internal static class NarratorOrbAssetBuilder
	{
		#region Fields

		internal const string SampleClipPath = "Assets/Content/Audio/Voice/Narrator Orb/Narrator_TestSample.wav";
		internal const string SettingsPath = "Assets/Content/Data/Narrator Orb/Narrator Orb Settings.asset";
		internal const string LayoutPath = "Assets/Content/Models/Narrator Orb/Narrator Orb Layout.asset";
		internal const string PrefabPath = "Assets/Content/Prefabs/Narrator Orb/Narrator Orb.prefab";

		private const string MenuPath = "DroneDDW/Narrator Orb/Build Orb Assets";
		private const string DialogTitle = "Narrator Orb";
		private const string MaterialsFolder = "Assets/Content/Materials/Narrator Orb";
		private const string ShadersFolder = "Assets/Content/Shaders/Narrator Orb";
		private const string CullProperty = "_Cull";

		#endregion

		#region Internal

		// Regenerates the layout's meshes in place, so everything that uses them keeps its references
		internal static void BakeLayout(NarratorOrbLayout layout)
		{
			NarratorOrbParticleSet particles = NarratorOrbParticleGenerator.Generate();
			Mesh particleMesh = MeshOrNew(layout, layout.Particles, "Narrator Orb Particles");
			Mesh haloMesh = MeshOrNew(layout, layout.Halos, "Narrator Orb Halos");
			Mesh innerMesh = MeshOrNew(layout, layout.InnerGlow, "Narrator Orb Inner Glow");
			Mesh ringMesh = MeshOrNew(layout, layout.RippleRings, "Narrator Orb Ripple Rings");

			int count = NarratorOrbMeshBaker.BakeParticles(particleMesh, particles, layout.ParticleCountScale);
			NarratorOrbMeshBaker.BakeShells(haloMesh, NarratorOrbMeshBaker.HaloShells);
			NarratorOrbMeshBaker.BakeShells(innerMesh, new[] { NarratorOrbMeshBaker.InnerShell });
			NarratorOrbMeshBaker.BakeSphere(ringMesh);
			layout.SetMeshes(particleMesh, haloMesh, innerMesh, ringMesh);
			layout.SetBakeResult(count, layout.ParticleCountScale);

			EditorUtility.SetDirty(particleMesh);
			EditorUtility.SetDirty(haloMesh);
			EditorUtility.SetDirty(innerMesh);
			EditorUtility.SetDirty(ringMesh);
			EditorUtility.SetDirty(layout);
			AssetDatabase.SaveAssets();
			Debug.Log($"[Narrator Orb] Baked {count:N0} of {NarratorOrbParticleGenerator.TotalCount:N0} particles "
				+ $"({particleMesh.vertexCount:N0} vertices) into {LayoutPath}.", layout);
		}

		// Creates the folders of an asset path that don't exist yet
		internal static void EnsureFolder(string folder)
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

		#region Private

		[MenuItem(MenuPath)]
		private static void Build()
		{
			try
			{
				EditorUtility.DisplayProgressBar(DialogTitle, "Importing the sample narration", 0.1f);
				ImportSampleClip();

				EditorUtility.DisplayProgressBar(DialogTitle, "Settings", 0.2f);
				NarratorOrbSettings settings = LoadOrCreate<NarratorOrbSettings>(SettingsPath);

				EditorUtility.DisplayProgressBar(DialogTitle, "Baking the particle layout", 0.3f);
				NarratorOrbLayout layout = LoadOrCreate<NarratorOrbLayout>(LayoutPath);
				BakeLayout(layout);

				EditorUtility.DisplayProgressBar(DialogTitle, "Materials", 0.7f);
				Material particles = LoadOrCreateMaterial("Narrator Orb Particles", "Narrator Orb Particles.shader",
					null);
				Material halos = LoadOrCreateMaterial("Narrator Orb Halos", "Narrator Orb Glow.shader", CullMode.Front);
				Material innerGlow = LoadOrCreateMaterial("Narrator Orb Inner Glow", "Narrator Orb Glow.shader",
					CullMode.Back);
				Material rippleRings = LoadOrCreateMaterial("Narrator Orb Ripple Rings",
					"Narrator Orb Ripple Rings.shader", null);
				if (particles == null || halos == null || innerGlow == null || rippleRings == null)
				{
					return;
				}

				EditorUtility.DisplayProgressBar(DialogTitle, "Prefab", 0.85f);
				BuildPrefab(settings, layout, particles, halos, innerGlow, rippleRings);
				AssetDatabase.SaveAssets();
				Debug.Log($"[Narrator Orb] Built the orb's assets and {PrefabPath}.");
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		// PCM at the clip's own rate, decompressed on load: the samples the analysis reads match the preview's exactly
		private static void ImportSampleClip()
		{
			if (AssetImporter.GetAtPath(SampleClipPath) is not AudioImporter importer)
			{
				Debug.LogWarning($"[Narrator Orb] There is no sample narration at {SampleClipPath}; the test scene and "
					+ "the analysis test need it.");
				return;
			}

			AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;
			bool isChanged = sampleSettings.loadType != AudioClipLoadType.DecompressOnLoad
				|| sampleSettings.compressionFormat != AudioCompressionFormat.PCM
				|| sampleSettings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate
				|| !sampleSettings.preloadAudioData
				|| importer.forceToMono;
			foreach (string platform in new[] { "Android", "Standalone" })
			{
				if (importer.ContainsSampleSettingsOverride(platform))
				{
					importer.ClearSampleSettingOverride(platform);
					isChanged = true;
				}
			}

			if (!isChanged)
			{
				return;
			}

			sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
			sampleSettings.compressionFormat = AudioCompressionFormat.PCM;
			sampleSettings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
			sampleSettings.preloadAudioData = true;
			importer.defaultSampleSettings = sampleSettings;
			importer.forceToMono = false;
			importer.SaveAndReimport();
		}

		private static T LoadOrCreate<T>(string path) where T : ScriptableObject
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(path);
			if (asset != null)
			{
				return asset;
			}

			EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
			asset = ScriptableObject.CreateInstance<T>();
			AssetDatabase.CreateAsset(asset, path);
			return asset;
		}

		// The layout's mesh, or a new one added to the layout's file
		private static Mesh MeshOrNew(NarratorOrbLayout layout, Mesh existing, string name)
		{
			if (existing != null)
			{
				return existing;
			}

			Mesh mesh = new() { name = name };
			AssetDatabase.AddObjectToAsset(mesh, layout);
			return mesh;
		}

		private static Material LoadOrCreateMaterial(string name, string shaderFileName, CullMode? cull)
		{
			string shaderPath = $"{ShadersFolder}/{shaderFileName}";
			Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
			if (shader == null)
			{
				Debug.LogError($"[Narrator Orb] The shader {shaderPath} is missing, so the materials can't be made.");
				return null;
			}

			string path = $"{MaterialsFolder}/{name}.mat";
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				EnsureFolder(MaterialsFolder);
				material = new Material(shader) { name = name };
				AssetDatabase.CreateAsset(material, path);
			}
			else if (material.shader != shader)
			{
				material.shader = shader;
			}

			if (cull.HasValue)
			{
				material.SetFloat(CullProperty, (float)cull.Value);
			}

			EditorUtility.SetDirty(material);
			return material;
		}

		// Built in a preview scene, so the open scene isn't touched
		private static void BuildPrefab(NarratorOrbSettings settings, NarratorOrbLayout layout, Material particles,
			Material halos, Material innerGlow, Material rippleRings)
		{
			Scene previewScene = EditorSceneManager.NewPreviewScene();
			try
			{
				GameObject root = ObjectFactory.CreateGameObject(previewScene, HideFlags.None, "Narrator Orb");
				AudioSource voice = root.AddComponent<AudioSource>();
				voice.playOnAwake = false;
				voice.spatialBlend = 1f;
				voice.dopplerLevel = 0f;
				VoiceLiveTap liveTap = root.AddComponent<VoiceLiveTap>();
				NarratorOrbController controller = root.AddComponent<NarratorOrbController>();
				controller.Configure(settings, voice, liveTap,
					AddPart(previewScene, root.transform, "Particles", layout.Particles, particles),
					AddPart(previewScene, root.transform, "Halos", layout.Halos, halos),
					AddPart(previewScene, root.transform, "Inner Glow", layout.InnerGlow, innerGlow),
					AddPart(previewScene, root.transform, "Ripple Rings", layout.RippleRings, rippleRings));

				EnsureFolder(PrefabPath.Substring(0, PrefabPath.LastIndexOf('/')));
				PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
			}
			finally
			{
				EditorSceneManager.ClosePreviewScene(previewScene);
			}
		}

		private static MeshRenderer AddPart(Scene scene, Transform parent, string name, Mesh mesh, Material material)
		{
			GameObject part = ObjectFactory.CreateGameObject(scene, HideFlags.None, name);
			part.transform.SetParent(parent, false);
			part.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer renderer = part.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = material;
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			renderer.receiveShadows = false;
			renderer.lightProbeUsage = LightProbeUsage.Off;
			renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
			renderer.allowOcclusionWhenDynamic = false;
			return renderer;
		}

		#endregion
	}
}
