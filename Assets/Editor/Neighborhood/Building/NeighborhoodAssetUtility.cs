using System.Collections.Generic;
using System.IO;
using DroneDDW.Neighborhood.Editor.Import;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Table;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>
    /// Asset steps for the street builder: locating the Neighborhood content folder, making the materials (palette, glow,
    /// characters, leash, light pools, sky) and remapping the FBX materials onto them.
    /// Running it again updates the existing assets, so their GUIDs stay the same.
    /// </summary>
    internal static class NeighborhoodAssetUtility
    {
        #region Fields

        internal const string ModelsFolder = "Models";
        internal const string LayoutsFolder = "Layouts";
        internal const string TexturesFolder = "Textures";
        internal const string MaterialsFolder = "Materials";
        internal const string ScenesParentFolder = "Assets/Scenes";
        internal const string ScenesFolder = "Neighborhood";
        internal const string AnimationFolder = "Animation";
        internal const string VoiceFolder = "Voice";
        internal const string DataFolder = "Data";
        internal const string PaletteFileName = "Street_Palette.png";

        private const string PushSettingsFileName = "Neighborhood Push Settings.asset";
        private const string PaletteSearch = "Street_Palette t:Texture2D";
        private const string LightPoolTextureName = "Street_LightPool.png";
        private const string CharacterTextureName = "Street_Characters.png";
        private const string PalettePrefix = "Street_Mat_";
        private const string LightPoolPrefix = "Street_LightPool_";
        private const string LeashMaterialName = "Street_Mat_Leash";
        private const string SkyMaterialName = "Street_Sky_Dusk";
        private const string UrpLitShader = "Universal Render Pipeline/Lit";
        private const string BuiltInLitShader = "Standard";
        private const string LightPoolShader = "DroneDDW/Neighborhood Light Pool";
        private const string PanoramicSkyShader = "Skybox/Panoramic";
        private const string WaterKey = "Water";
        private const string GlassGlowKey = "GlassGlow";
        private const float MatteSmoothness = 0.1f;
        private const float WaterSmoothness = 0.85f;
        private const float GlassSmoothness = 0.6f;

        private static readonly string[] PaletteKeys =
        {
            "Terrain", "Paths", "Vegetation", "Props", "Structures", WaterKey, "Emissive", GlassGlowKey, "NightGlow",
        };

        private static readonly Color LeashColor = new Color(0.62f, 0.13f, 0.11f);

        #endregion

        #region Internal

        /// <summary>The Neighborhood content folder (Content/Neighborhood), found through the palette texture so it can live anywhere under Assets.</summary>
        internal static string FindRootFolder()
        {
            foreach (string guid in AssetDatabase.FindAssets(PaletteSearch))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) != PaletteFileName)
                {
                    continue;
                }

                string root = Path.GetDirectoryName(Path.GetDirectoryName(path));
                return string.IsNullOrEmpty(root) ? null : root.Replace('\\', '/');
            }

            return null;
        }

        internal static string EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }

            return path;
        }

        /// <summary>
        /// The slider's push settings (Data/Neighborhood Push Settings.asset under the root): created with the defaults
        /// the first time, never overwritten after, so tuning survives every rebuild.
        /// </summary>
        internal static NeighborhoodPushSettings LoadOrCreatePushSettings(string root)
        {
            string path = $"{EnsureFolder(root, DataFolder)}/{PushSettingsFileName}";
            NeighborhoodPushSettings settings = AssetDatabase.LoadAssetAtPath<NeighborhoodPushSettings>(path);
            if (settings != null)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<NeighborhoodPushSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            return settings;
        }

        internal static GameObject LoadModel(string root, string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(root, fileName));
        }

        internal static string ModelPath(string root, string fileName)
        {
            return $"{root}/{ModelsFolder}/{fileName}.fbx";
        }

        internal static NeighborhoodMaterialSet CreateMaterials(string root, NeighborhoodLayout layout)
        {
            string folder = EnsureFolder(root, MaterialsFolder);
            string textures = $"{root}/{TexturesFolder}";
            Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textures}/{PaletteFileName}");
            Texture2D characterTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textures}/{CharacterTextureName}");
            Texture2D poolTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textures}/{LightPoolTextureName}");
            Texture2D sky = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textures}/{layout.Dusk.SkyTexture}.png");
            Shader lit = FindLitShader();
            NeighborhoodMaterialSet set = new NeighborhoodMaterialSet();

            NeighborhoodLayoutDusk dusk = layout.Dusk;
            foreach (string key in PaletteKeys)
            {
                Material material = LoadOrCreate(folder, PalettePrefix + key, lit);
                material.mainTexture = palette;
                material.color = Color.white;
                float smoothness = key == WaterKey ? WaterSmoothness : key == GlassGlowKey ? GlassSmoothness : MatteSmoothness;
                SetFloatIfPresent(material, "_Smoothness", smoothness);
                SetFloatIfPresent(material, "_Glossiness", smoothness);
                SetFloatIfPresent(material, "_Metallic", 0f);
                SetEmission(material, palette, EmissionFor(key, dusk));
                EditorUtility.SetDirty(material);
                set.ByName.Add(material.name, material);
            }

            foreach (NeighborhoodLayoutMaterial entry in layout.CharacterMaterials)
            {
                Material material = LoadOrCreate(folder, entry.Name, lit);
                material.mainTexture = entry.IsTextured ? characterTexture : null;
                material.color = entry.IsTextured ? Color.white : entry.Color;
                SetFloatIfPresent(material, "_Smoothness", entry.Smoothness);
                SetFloatIfPresent(material, "_Glossiness", entry.Smoothness);
                SetFloatIfPresent(material, "_Metallic", 0f);
                SetEmission(material, null, 0f);
                EditorUtility.SetDirty(material);
                set.ByName.Add(material.name, material);
            }

            Material leash = LoadOrCreate(folder, LeashMaterialName, lit);
            leash.color = LeashColor;
            SetFloatIfPresent(leash, "_Smoothness", 0.3f);
            EditorUtility.SetDirty(leash);
            set.Leash = leash;

            Shader poolShader = Shader.Find(LightPoolShader);
            if (poolShader == null)
            {
                Debug.LogWarning($"[Neighborhood] Shader {LightPoolShader} not found; the light pools are skipped.");
            }
            else
            {
                foreach (NeighborhoodLayoutLight light in layout.Lights)
                {
                    if (light.PoolIntensity <= 0f || set.LightPools.ContainsKey(light.Kind))
                    {
                        continue;
                    }

                    Material pool = LoadOrCreate(folder, LightPoolPrefix + light.Kind, poolShader);
                    pool.mainTexture = poolTexture;
                    pool.enableInstancing = true;
                    // _Color is a Vector property in the shader, so this linear value reaches it unchanged.
                    Color linear = light.Color.linear * light.PoolIntensity;
                    pool.SetVector("_Color", new Vector4(linear.r, linear.g, linear.b, 1f));
                    EditorUtility.SetDirty(pool);
                    set.LightPools.Add(light.Kind, pool);
                }
            }

            Shader skyShader = Shader.Find(PanoramicSkyShader);
            if (skyShader != null && sky != null)
            {
                Material skybox = LoadOrCreate(folder, SkyMaterialName, skyShader);
                skybox.SetTexture("_MainTex", sky);
                SetFloatIfPresent(skybox, "_Exposure", 1f);
                SetFloatIfPresent(skybox, "_Rotation", 0f);
                EditorUtility.SetDirty(skybox);
                set.Skybox = skybox;
            }

            AssetDatabase.SaveAssets();
            return set;
        }

        /// <summary>Points every Street_ FBX at the builder's materials and reimports it.</summary>
        internal static void RemapModels(string root, NeighborhoodMaterialSet set)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { $"{root}/{ModelsFolder}" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!NeighborhoodModelPostprocessor.IsNeighborhoodModelPath(path) || !(AssetImporter.GetAtPath(path) is ModelImporter importer))
                {
                    continue;
                }

                foreach (KeyValuePair<string, Material> pair in set.ByName)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
                }

                // Reimporting also runs NeighborhoodModelPostprocessor, so rigs, clips and static flags are current.
                importer.SaveAndReimport();
            }
        }

        #endregion

        #region Private

        private static Shader FindLitShader()
        {
            Shader shader = Shader.Find(UrpLitShader);
            if (shader != null)
            {
                return shader;
            }

            Debug.LogWarning("[Neighborhood] URP Lit shader not found, using the built-in Standard shader.");
            return Shader.Find(BuiltInLitShader);
        }

        private static float EmissionFor(string key, NeighborhoodLayoutDusk dusk)
        {
            switch (key)
            {
                case "Emissive":
                    return dusk.Emissive;
                case GlassGlowKey:
                    return dusk.GlassGlow;
                case "NightGlow":
                    return dusk.NightGlow;
                default:
                    return 0f;
            }
        }

        private static Material LoadOrCreate(string folder, string name, Shader shader)
        {
            string path = $"{folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        /// <summary>The palette colour itself glows (emission map = palette) at the given strength.</summary>
        private static void SetEmission(Material material, Texture2D map, float strength)
        {
            if (!material.HasProperty("_EmissionColor"))
            {
                return;
            }

            if (strength <= 0f)
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                return;
            }

            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", map);
            material.SetColor("_EmissionColor", ToMaterialColor(new Color(strength, strength, strength, 1f)));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        /// <summary>
        /// Material Color properties are read as gamma and converted to linear when the project renders in linear
        /// space, so a linear strength is passed as its gamma equivalent there.
        /// </summary>
        private static Color ToMaterialColor(Color linear)
        {
            return PlayerSettings.colorSpace == ColorSpace.Linear ? linear.gamma : linear;
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        #endregion
    }
}
