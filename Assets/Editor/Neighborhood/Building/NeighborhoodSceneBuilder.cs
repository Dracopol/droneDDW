using System.Collections.Generic;
using DroneDDW.Neighborhood.Audio;
using DroneDDW.Neighborhood.Characters;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Editor.Staging;
using DroneDDW.Neighborhood.Experience;
using DroneDDW.Neighborhood.Motion;
using DroneDDW.Neighborhood.Table;
using DroneDDW.Neighborhood.Versions;
using DroneDDW.Neighborhood.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>
    /// Menu: DroneDDW / Neighborhood / Build Neighborhood Scene.
    /// 1. Makes the materials (palette, glow, characters, leash, light pools, dusk sky) and reimports every
    ///    Street_ FBX with them; fills the voice library and links the WAVs found in the project.
    /// 2. Builds Scenes/Neighborhood/Neighborhood.unity: the shared street (static), every changing object tagged with its
    ///    versions, lamps with their lights and light pools, drones, robots and ducks that move, the three
    ///    people with their poses, faces, voices, props and the leash (moved with their props to where
    ///    Data/Neighborhood Staging.asset puts them, in front of the seat), the table with the three-notch slider,
    ///    the miniatures and the lock-in button, the project's XR rig at the seat, ambience, the dusk sky, sun, ambient
    ///    light and fog. The rig is saved switched off: NeighborhoodStandaloneRig switches it on only when the scene is
    ///    opened on its own, since the experience loads this scene on top of its own and brings its own rig.
    /// Safe to run again: materials and the voice library keep their GUIDs, the scene is rebuilt.
    /// </summary>
    internal static class NeighborhoodSceneBuilder
    {
        #region Fields

        private const string MenuPath = "DroneDDW/Neighborhood/Build Neighborhood Scene";
        private const string DialogTitle = "Neighborhood";
        private const string LayoutFileName = "Street_Layout.json";
        private const string SharedModel = "Street_Shared";
        private const string ElementsModel = "Street_Elements";
        private const string TableModel = "Street_Table";
        private const string MiniaturesModel = "Street_Miniatures";
        private const string SceneName = "Neighborhood";
        private const float PoolHeight = 0.075f;
        private const float CameraPitch = 8f;
        private const float CameraFar = 1100f;
        private const float SunShadowStrength = 0.75f;

        private static readonly Quaternion PoolRotation = Quaternion.Euler(90f, 0f, 0f);

        #endregion

        #region Private

        [MenuItem(MenuPath)]
        private static void Build()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Run(true);
            }
        }

        /// <summary>Everything the menu does. Returns the scene path, or null when the Neighborhood content folder is incomplete.</summary>
        internal static string Run(bool isInteractive)
        {
            string root = NeighborhoodAssetUtility.FindRootFolder();
            if (string.IsNullOrEmpty(root))
            {
                Report(isInteractive, $"Could not find {NeighborhoodAssetUtility.PaletteFileName}. Put the whole Neighborhood content folder (Models, Layouts, Textures) under Assets first.");
                return null;
            }

            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{NeighborhoodAssetUtility.LayoutsFolder}/{LayoutFileName}");
            if (json == null)
            {
                Report(isInteractive, $"{LayoutFileName} is missing from {root}/{NeighborhoodAssetUtility.LayoutsFolder}.");
                return null;
            }

            NeighborhoodLayout layout = JsonUtility.FromJson<NeighborhoodLayout>(json.text);
            string missing = FindMissingModels(root, layout);
            if (missing.Length > 0)
            {
                Report(isInteractive, $"These models are missing from {root}/{NeighborhoodAssetUtility.ModelsFolder}: {missing}");
                return null;
            }

            try
            {
                EditorUtility.DisplayProgressBar(DialogTitle, "Creating materials", 0.1f);
                NeighborhoodMaterialSet materials = NeighborhoodAssetUtility.CreateMaterials(root, layout);

                EditorUtility.DisplayProgressBar(DialogTitle, "Remapping and reimporting the FBX files", 0.25f);
                NeighborhoodAssetUtility.RemapModels(root, materials);

                EditorUtility.DisplayProgressBar(DialogTitle, "Linking the voice lines", 0.5f);
                NeighborhoodVoiceLibrary library = NeighborhoodVoiceLibraryBuilder.CreateOrUpdate(root, layout);

                EditorUtility.DisplayProgressBar(DialogTitle, "Building the scene", 0.6f);
                string scenePath = BuildScene(root, layout, materials, library);
                AssetDatabase.SaveAssets();
                if (isInteractive)
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                }

                return scenePath;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Checked before anything is built, so a missing file never leaves a half-built scene.</summary>
        private static string FindMissingModels(string root, NeighborhoodLayout layout)
        {
            List<string> names = new List<string> { SharedModel, ElementsModel, TableModel, MiniaturesModel };
            foreach (NeighborhoodLayoutCharacter character in layout.Characters)
            {
                names.Add(character.Model);
            }

            List<string> missing = new List<string>();
            foreach (string name in names)
            {
                if (NeighborhoodAssetUtility.LoadModel(root, name) == null)
                {
                    missing.Add(name + ".fbx");
                }
            }

            return string.Join(", ", missing);
        }

        private static void Report(bool isInteractive, string message)
        {
            if (isInteractive)
            {
                EditorUtility.DisplayDialog(DialogTitle, message, "OK");
            }
            else
            {
                Debug.LogError("[Neighborhood] " + message);
            }
        }

        private static string BuildScene(string root, NeighborhoodLayout layout, NeighborhoodMaterialSet materials, NeighborhoodVoiceLibrary library)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetUpLighting(layout.Dusk, materials.Skybox);

            GameObject seat = new GameObject("SeatAnchor");
            seat.transform.position = new Vector3(0f, layout.SeatHeight, 0f);
            Transform head = SetUpViewer(scene, layout, seat.transform, out Transform trackingSpace);
            GameObject street = new GameObject("Neighborhood");
            GameObject shared = NeighborhoodBuildUtility.InstantiateUnpacked(NeighborhoodAssetUtility.LoadModel(root, SharedModel),
                street.transform, "Shared");
            GameObject elementsRoot = NeighborhoodBuildUtility.InstantiateUnpacked(NeighborhoodAssetUtility.LoadModel(root, ElementsModel),
                street.transform, "Elements");
            Dictionary<string, Transform> nodes = NeighborhoodBuildUtility.IndexDeep(elementsRoot.transform);

            Dictionary<string, List<Light>> lightsByOwner = CreateLights(layout, nodes, materials, street.transform);
            NeighborhoodStaging staging = NeighborhoodAssetUtility.LoadOrCreateStaging(root);
            HashSet<string> removed = RemoveObjects(NeighborhoodStagingBuilder.CollectRemovedObjects(staging), nodes);
            List<NeighborhoodVersionElement> elements = new List<NeighborhoodVersionElement>();
            int missing = 0;
            int movers = 0;
            foreach (NeighborhoodLayoutElement entry in layout.Elements)
            {
                if (!nodes.TryGetValue(entry.Name, out Transform node))
                {
                    missing += removed.Contains(entry.Name) ? 0 : 1;
                    continue;
                }

                NeighborhoodVersionElement element = node.gameObject.AddComponent<NeighborhoodVersionElement>();
                Light[] lights = lightsByOwner.TryGetValue(entry.Name, out List<Light> owned) ? owned.ToArray() : null;
                // Props in a hand hang on a bone, whose axes are not the world's: they must grow and shrink, never sink.
                NeighborhoodTransitionStyle style = entry.AttachCharacter >= 0 ? NeighborhoodTransitionStyle.Scale : (NeighborhoodTransitionStyle)entry.Style;
                element.Configure(entry.Mask, style, entry.LiftDepth, entry.DurationScale, lights);
                elements.Add(element);
                if (entry.Mover.Kind != (int)NeighborhoodMotionKind.None)
                {
                    AddMover(node.gameObject, entry.Mover);
                    movers++;
                }
            }

            NeighborhoodVersion start = NeighborhoodVersions.Clamp(layout.StartVersion);
            GameObject experience = new GameObject("Neighborhood_Experience");
            NeighborhoodVersionController controller = experience.AddComponent<NeighborhoodVersionController>();
            controller.Configure(elements.ToArray(), seat.transform, start);
            NeighborhoodCharacter[] characters = NeighborhoodCharacterBuilder.Build(root, layout, nodes, materials, controller, street.transform, start);
            List<string> overlaps = NeighborhoodStagingBuilder.Apply(staging, layout, characters, nodes, shared.transform, seat.transform.position);
            controller.ShowVersionInEditor(start);

            NeighborhoodPushSettings pushSettings = NeighborhoodAssetUtility.LoadOrCreatePushSettings(root);
            NeighborhoodTableParts table = NeighborhoodTableBuilder.Build(root, layout, street.transform, head, trackingSpace,
                pushSettings, start);
            GameObject voiceObject = new GameObject("Voice");
            voiceObject.transform.SetParent(experience.transform, false);
            NeighborhoodVoiceDirector voice = voiceObject.AddComponent<NeighborhoodVoiceDirector>();
            voice.Configure(library, characters, controller);
            NeighborhoodAmbience ambience = BuildAmbience(experience.transform);
            NeighborhoodExperience coordinator = experience.AddComponent<NeighborhoodExperience>();
            coordinator.Configure(controller, table.Slider, table.Preview, ambience, voice);
            coordinator.ConfigureTable(table.LockButton, table.HandBones, table.SliderDriver);
            coordinator.ConfigureCharacters(FindLooks(characters));
            NeighborhoodDebugKeys debugKeys = experience.AddComponent<NeighborhoodDebugKeys>();
            debugKeys.Configure(controller, voice);
            if (trackingSpace != null)
            {
                // Off until the scene knows it was opened on its own (the finger grab is already set up on it)
                GameObject rig = head.root.gameObject;
                rig.SetActive(false);
                experience.AddComponent<NeighborhoodStandaloneRig>().Configure(rig);
            }

            string scenesFolder = NeighborhoodAssetUtility.EnsureFolder(NeighborhoodAssetUtility.ScenesParentFolder,
                NeighborhoodAssetUtility.ScenesFolder);
            string scenePath = $"{scenesFolder}/{SceneName}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            if (missing > 0)
            {
                Debug.LogWarning($"[Neighborhood] {missing} layout entries have no node in {ElementsModel}.fbx.");
            }

            Debug.Log($"[Neighborhood] Built {scenePath}: {elements.Count} changing objects ({movers} moving), "
                + $"{characters.Length} people, starting at {start}. Staging {(staging.IsEnabled ? "on" : "off")}: "
                + $"{removed.Count} objects removed, {overlaps.Count} overlaps.");
            return scenePath;
        }

        private static NeighborhoodCharacterLook[] FindLooks(NeighborhoodCharacter[] characters)
        {
            List<NeighborhoodCharacterLook> looks = new List<NeighborhoodCharacterLook>();
            foreach (NeighborhoodCharacter character in characters)
            {
                if (character.TryGetComponent(out NeighborhoodCharacterLook look))
                {
                    looks.Add(look);
                }
            }

            return looks.ToArray();
        }

        /// <summary>
        /// Takes the street objects the staging leaves out (with the lights under them) out of the scene and the index.
        /// Returns the names removed.
        /// </summary>
        private static HashSet<string> RemoveObjects(HashSet<string> names, Dictionary<string, Transform> nodes)
        {
            HashSet<string> removed = new HashSet<string>();
            foreach (string name in names)
            {
                if (nodes.TryGetValue(name, out Transform node) && node != null)
                {
                    Object.DestroyImmediate(node.gameObject);
                    nodes.Remove(name);
                    removed.Add(name);
                }
                else
                {
                    Debug.LogWarning($"[Neighborhood] Staging: the object {name} to remove is not in the street.");
                }
            }

            return removed;
        }

        private static void SetUpLighting(NeighborhoodLayoutDusk dusk, Material skybox)
        {
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = dusk.AmbientSky;
            RenderSettings.ambientEquatorColor = dusk.AmbientEquator;
            RenderSettings.ambientGroundColor = dusk.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = dusk.FogColor;
            RenderSettings.fogStartDistance = dusk.FogStart;
            RenderSettings.fogEndDistance = dusk.FogEnd;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            GameObject sunObject = new GameObject("Sun (dusk)");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = dusk.SunColor;
            sun.intensity = dusk.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = SunShadowStrength;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            Vector3 travel = dusk.SunTravel.sqrMagnitude > 0f ? dusk.SunTravel.normalized : Vector3.down;
            sunObject.transform.rotation = Quaternion.LookRotation(travel, Vector3.up);
            RenderSettings.sun = sun;
        }

        /// <summary>
        /// The project's XR rig at the seat, facing the table (+Z). Falls back to a desktop camera when the main scene
        /// or its rig is missing. Returns the head and hands out the rig's tracking space (null without the rig).
        /// </summary>
        private static Transform SetUpViewer(Scene scene, NeighborhoodLayout layout, Transform seat, out Transform trackingSpace)
        {
            Camera rigCamera = NeighborhoodRigBuilder.CopyXRRig(scene, seat.position, CameraFar, out trackingSpace);
            if (rigCamera != null)
            {
                return rigCamera.transform;
            }

            Debug.LogWarning($"[Neighborhood] No XR rig in {NeighborhoodRigBuilder.MainScenePath}; using a desktop camera.");
            return CreateDesktopCamera(layout);
        }

        private static Transform CreateDesktopCamera(NeighborhoodLayout layout)
        {
            // Desktop stand-in for the seated viewer when the project's XR rig can't be copied.
            GameObject cameraObject = new GameObject("Main Camera (desktop preview)");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = CameraFar;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, layout.SeatHeight + layout.EyeHeight, 0f),
                Quaternion.Euler(CameraPitch, 0f, 0f));
            return cameraObject.transform;
        }

        /// <summary>
        /// Point lights where the layout has them, under the object they belong to (so they come and go with it),
        /// each street lamp with a soft light pool on the ground. Returns the lights per owner object.
        /// </summary>
        private static Dictionary<string, List<Light>> CreateLights(NeighborhoodLayout layout, Dictionary<string, Transform> nodes,
            NeighborhoodMaterialSet materials, Transform street)
        {
            GameObject lightsRoot = new GameObject("Lights");
            lightsRoot.transform.SetParent(street, false);
            Dictionary<string, List<Light>> byOwner = new Dictionary<string, List<Light>>();
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            foreach (NeighborhoodLayoutLight entry in layout.Lights)
            {
                GameObject lightObject = new GameObject(entry.Name);
                lightObject.transform.position = entry.Position;
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = entry.Color;
                light.intensity = entry.Intensity;
                light.range = entry.Range;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;

                Transform parent = lightsRoot.transform;
                if (!string.IsNullOrEmpty(entry.Owner) && nodes.TryGetValue(entry.Owner, out Transform owner))
                {
                    parent = owner;
                    if (!byOwner.TryGetValue(entry.Owner, out List<Light> list))
                    {
                        list = new List<Light>();
                        byOwner.Add(entry.Owner, list);
                    }

                    list.Add(light);
                }

                lightObject.transform.SetParent(parent, true);
                if (entry.PoolIntensity > 0f && quad != null && materials.LightPools.TryGetValue(entry.Kind, out Material pool))
                {
                    CreatePool(entry, parent, quad, pool);
                }
            }

            return byOwner;
        }

        private static void CreatePool(NeighborhoodLayoutLight entry, Transform parent, Mesh quad, Material material)
        {
            GameObject poolObject = new GameObject(entry.Name + "_Pool");
            poolObject.transform.SetPositionAndRotation(new Vector3(entry.Position.x, PoolHeight, entry.Position.z), PoolRotation);
            poolObject.transform.localScale = new Vector3(entry.PoolRadius * 2f, entry.PoolRadius * 2f, 1f);
            MeshFilter filter = poolObject.AddComponent<MeshFilter>();
            filter.sharedMesh = quad;
            MeshRenderer renderer = poolObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            poolObject.transform.SetParent(parent, true);
        }

        private static void AddMover(GameObject target, NeighborhoodLayoutMover entry)
        {
            Vector3[] points = new Vector3[entry.Points.Count];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = entry.Points[i];
            }

            NeighborhoodMover mover = target.AddComponent<NeighborhoodMover>();
            mover.Configure((NeighborhoodMotionKind)entry.Kind, points, entry.Center, entry.RadiusX, entry.RadiusZ, entry.Speed,
                entry.Phase, entry.Clockwise, entry.BobHeight, entry.BobPeriod, entry.FaceTravel);

            // Put it where it will be when play starts, so the scene view matches.
            mover.PlaceAtStart();
        }

        private static NeighborhoodAmbience BuildAmbience(Transform parent)
        {
            GameObject ambienceObject = new GameObject("Ambience");
            ambienceObject.transform.SetParent(parent, false);
            AudioSource nature = CreateLoopSource("Ambience_Nature", ambienceObject.transform);
            AudioSource efficiency = CreateLoopSource("Ambience_Efficiency", ambienceObject.transform);
            NeighborhoodAmbience ambience = ambienceObject.AddComponent<NeighborhoodAmbience>();
            ambience.Configure(nature, efficiency);
            return ambience;
        }

        private static AudioSource CreateLoopSource(string name, Transform parent)
        {
            GameObject sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(parent, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        #endregion
    }
}
