using System.Collections.Generic;
using DroneDDW.Neighborhood.Characters;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Editor.Staging;
using DroneDDW.Neighborhood.Versions;
using DroneDDW.Neighborhood.Voice;
using DroneDDW.Voice;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>
    /// Puts the three people in the street: each model at its spot, an animator controller with one state per
    /// clip (the pose clips are named Pose_Nature, Pose_Balanced, Pose_Efficiency), the face parts, a spatial
    /// voice source on the head bone, the props they hold (parented to the hand bone while the matching pose
    /// is sampled, so they sit in the hand) and the student's leash. Measures per pose whether they stand or sit,
    /// and, once they stand at their final spots, the ground surfaces under them, which they ride on while playing.
    /// </summary>
    internal static class NeighborhoodCharacterBuilder
    {
        #region Fields

        private const string HeadBone = "Head";
        private const string NeckBone = "Neck";
        private const string SpineBone = "Spine1";
        private const string BodyMesh = "Body";
        private const string HipsBone = "Hips";
        // Just above the ground; the light keeps it there as the paving comes and goes
        private const float SpeakerPoolHeight = 0.01f;
        private const float SpeakerPoolRadius = 1.2f;
        // Hips less than this above the soles: the person sits (standing, the hips are about 0.8 m up, seated 0.55 m)
        private const float MinStandingHipsMetres = 0.65f;
        private const string PreviewClipPrefix = "__preview__";
        private const float LeashWidth = 0.014f;
        private const int LeashPoints = 10;
        // Grandma and the worker stand 16 to 18 m from the seat: full volume up to 10 m keeps them easy to hear,
        // while the direction still tells the participant who is speaking.
        private const float VoiceMinDistance = 10f;
        private const float VoiceMaxDistance = 40f;

        private static readonly string[] VersionNames = { "Nature", "Balanced", "Efficiency" };

        #endregion

        #region Internal

        internal static NeighborhoodCharacter[] Build(string root, NeighborhoodLayout layout, Dictionary<string, Transform> elementNodes,
            NeighborhoodMaterialSet materials, NeighborhoodVersionController controller, Transform parent, NeighborhoodVersion start,
            NeighborhoodVoiceLibrary library)
        {
            GameObject group = new GameObject("Characters");
            group.transform.SetParent(parent, false);
            string animationFolder = NeighborhoodAssetUtility.EnsureFolder(root, NeighborhoodAssetUtility.AnimationFolder);
            List<NeighborhoodCharacter> characters = new List<NeighborhoodCharacter>();
            foreach (NeighborhoodLayoutCharacter entry in layout.Characters)
            {
                GameObject model = NeighborhoodAssetUtility.LoadModel(root, entry.Model);
                if (model == null)
                {
                    Debug.LogError($"[Neighborhood] {entry.Model}.fbx is missing; {entry.Name} is left out.");
                    continue;
                }

                GameObject instance = NeighborhoodBuildUtility.InstantiateUnpacked(model, group.transform, entry.Name);
                instance.transform.SetPositionAndRotation(entry.Position, Quaternion.Euler(0f, entry.RotationY, 0f));
                Dictionary<string, AnimationClip> clips = LoadClips(NeighborhoodAssetUtility.ModelPath(root, entry.Model));
                NeighborhoodLayoutPose startPose = FindPose(entry, (int)start);

                if (!instance.TryGetComponent(out Animator animator))
                {
                    animator = instance.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = CreateController(animationFolder, entry, clips, startPose);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                NeighborhoodFace face = BuildFace(instance, entry.Name);
                AudioSource voice = BuildVoice(instance, entry);
                NeighborhoodCharacter character = instance.AddComponent<NeighborhoodCharacter>();
                NeighborhoodCharacterPose[] poses = ToPoses(entry, library);
                MeasureStances(instance, entry, clips, poses);
                character.Configure((NeighborhoodCharacterId)entry.Id, animator, face, voice, controller, poses);
                BuildLook(instance, character, entry.Name);
                BuildSpeakerLight(instance, character, materials.SpeakerPool);
                AttachProps(instance, entry, clips, layout, elementNodes, materials.Leash);

                // Leave the scene showing the start pose and face.
                if (startPose != null && clips.TryGetValue(startPose.Clip, out AnimationClip startClip))
                {
                    startClip.SampleAnimation(instance, 0f);
                    face.SetEmotion(RestingEmotion(entry, startPose, library));
                }

                characters.Add(character);
            }

            return characters.ToArray();
        }

        /// <summary>
        /// Gives each person the ground surfaces under their feet (paving, plaza, paths, from any version), with a point
        /// on each top, so they ride on them while playing. Runs once the people stand at their final spots (after the
        /// staging) and before the street shows its start version (the surfaces still at their places).
        /// </summary>
        internal static void PlaceOnGround(IReadOnlyList<NeighborhoodCharacter> characters, NeighborhoodLayout layout,
            IReadOnlyDictionary<string, Transform> elementNodes)
        {
            foreach (NeighborhoodCharacter character in characters)
            {
                Vector3 origin = character.transform.position;
                List<NeighborhoodVersionElement> surfaces = new List<NeighborhoodVersionElement>();
                List<Vector3> points = new List<Vector3>();
                float[] groundByVersion = new float[VersionNames.Length];
                foreach (NeighborhoodLayoutElement entry in layout.Elements)
                {
                    if (!NeighborhoodStagingBuilder.GroundGroups.Contains(entry.Group)
                        || !elementNodes.TryGetValue(entry.Name, out Transform node) || node == null
                        || !node.TryGetComponent(out NeighborhoodVersionElement element)
                        || !NeighborhoodGroundBuilder.TryReadTop(node, origin, out Vector3 top))
                    {
                        continue;
                    }

                    surfaces.Add(element);
                    points.Add(node.InverseTransformPoint(top));
                    for (int version = 0; version < groundByVersion.Length; version++)
                    {
                        if ((entry.Mask & (1 << version)) != 0)
                        {
                            groundByVersion[version] = Mathf.Max(groundByVersion[version], top.y - origin.y);
                        }
                    }
                }

                character.ConfigureGround(surfaces.ToArray(), points.ToArray());
                List<string> grounds = new List<string>();
                for (int version = 0; version < groundByVersion.Length; version++)
                {
                    grounds.Add($"{VersionNames[version]} {groundByVersion[version] * 100f:0.0} cm");
                }

                Debug.Log($"[Neighborhood] {character.name} has {surfaces.Count} ground surfaces under them; the ground "
                    + $"there is {string.Join(", ", grounds)} above bare ground.");
            }
        }

        #endregion

        #region Private

        private static Dictionary<string, AnimationClip> LoadClips(string modelPath)
        {
            Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith(PreviewClipPrefix, System.StringComparison.Ordinal))
                {
                    clips[clip.name] = clip;
                }
            }

            return clips;
        }

        private static NeighborhoodLayoutPose FindPose(NeighborhoodLayoutCharacter entry, int version)
        {
            foreach (NeighborhoodLayoutPose pose in entry.Poses)
            {
                if (pose.Version == version)
                {
                    return pose;
                }
            }

            return null;
        }

        private static NeighborhoodCharacterPose[] ToPoses(NeighborhoodLayoutCharacter entry, NeighborhoodVoiceLibrary library)
        {
            NeighborhoodCharacterPose[] poses = new NeighborhoodCharacterPose[entry.Poses.Count];
            for (int i = 0; i < poses.Length; i++)
            {
                NeighborhoodLayoutPose pose = entry.Poses[i];
                poses[i] = new NeighborhoodCharacterPose(NeighborhoodVersions.Clamp(pose.Version), pose.Clip,
                    RestingEmotion(entry, pose, library));
            }

            return poses;
        }

        /// <summary>
        /// Per pose, whether the person stands or sits (how high the hips are above the soles) and how far below the
        /// origin the soles reach, from the body in that pose.
        /// </summary>
        private static void MeasureStances(GameObject instance, NeighborhoodLayoutCharacter entry,
            Dictionary<string, AnimationClip> clips, NeighborhoodCharacterPose[] poses)
        {
            Transform hips = NeighborhoodBuildUtility.FindDeep(instance.transform, HipsBone);
            SkinnedMeshRenderer[] bodies = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (hips == null || bodies.Length == 0)
            {
                Debug.LogWarning($"[Neighborhood] {entry.Name} has no {HipsBone} bone or no skinned body; they stay where "
                    + "they were placed in every version.");
                return;
            }

            Mesh baked = new Mesh();
            List<string> stances = new List<string>();
            foreach (NeighborhoodCharacterPose pose in poses)
            {
                if (!clips.TryGetValue(pose.State, out AnimationClip clip))
                {
                    continue;
                }

                clip.SampleAnimation(instance, 0f);
                float lowest = float.PositiveInfinity;
                foreach (SkinnedMeshRenderer body in bodies)
                {
                    body.BakeMesh(baked, true);
                    Matrix4x4 world = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                    foreach (Vector3 vertex in baked.vertices)
                    {
                        lowest = Mathf.Min(lowest, world.MultiplyPoint3x4(vertex).y);
                    }
                }

                if (float.IsPositiveInfinity(lowest))
                {
                    continue;
                }

                bool isStanding = hips.position.y - lowest >= MinStandingHipsMetres;
                float soleDepth = instance.transform.position.y - lowest;
                pose.SetStance(isStanding, soleDepth);
                stances.Add($"{VersionNames[(int)pose.Version]} {(isStanding ? "stands" : "sits")}, soles "
                    + $"{soleDepth * 100f:0.0} cm below the origin");
            }

            Object.DestroyImmediate(baked);
            Debug.Log($"[Neighborhood] {entry.Name}: {string.Join("; ", stances)}.");
        }

        /// <summary>
        /// The face a person rests with in a version: the emotion of their line in that version's reactions, so the face
        /// doesn't change when they start or stop talking. Without a line there, the export's face, never angry.
        /// </summary>
        private static NeighborhoodEmotion RestingEmotion(NeighborhoodLayoutCharacter entry, NeighborhoodLayoutPose pose,
            NeighborhoodVoiceLibrary library)
        {
            if (library != null)
            {
                NeighborhoodVersion version = NeighborhoodVersions.Clamp(pose.Version);
                foreach (string lineId in library.GetReactions(version, NeighborhoodVoiceStage.Now))
                {
                    if (library.TryGetLine(lineId, out NeighborhoodVoiceLine line) && (int)line.Character == entry.Id)
                    {
                        return line.Emotion;
                    }
                }
            }

            NeighborhoodEmotion exported = (NeighborhoodEmotion)pose.Emotion;
            return exported == NeighborhoodEmotion.Angry ? NeighborhoodEmotion.Natural : exported;
        }

        /// <summary>
        /// One state per clip, named after it; the start pose is the default state. An existing controller is
        /// refilled rather than replaced, so its GUID (and anything pointing at it) survives a rebuild.
        /// </summary>
        private static AnimatorController CreateController(string folder, NeighborhoodLayoutCharacter entry,
            Dictionary<string, AnimationClip> clips, NeighborhoodLayoutPose startPose)
        {
            string path = $"{folder}/{entry.Model}.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states)
            {
                machine.RemoveState(child.state);
            }

            List<string> names = new List<string>(clips.Keys);
            names.Sort(System.StringComparer.Ordinal);
            foreach (string clipName in names)
            {
                AnimatorState state = machine.AddState(clipName);
                state.motion = clips[clipName];
                state.writeDefaultValues = true;
                if (startPose != null && clipName == startPose.Clip)
                {
                    machine.defaultState = state;
                }
            }

            foreach (NeighborhoodLayoutPose pose in entry.Poses)
            {
                if (!clips.ContainsKey(pose.Clip))
                {
                    Debug.LogError($"[Neighborhood] {entry.Model}.fbx has no clip {pose.Clip}.");
                }
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static NeighborhoodFace BuildFace(GameObject instance, string characterName)
        {
            NeighborhoodFace face = instance.AddComponent<NeighborhoodFace>();
            face.Configure(FindRenderer(instance, characterName, "eyes_usual"), FindRenderer(instance, characterName, "eyes_happy"),
                FindRenderer(instance, characterName, "eyes_angry"), FindRenderer(instance, characterName, "mouth_smile"),
                FindRenderer(instance, characterName, "mouth_bigsmile"), FindRenderer(instance, characterName, "mouth_frown"),
                FindRenderer(instance, characterName, "mouth_open"));
            return face;
        }

        private static Renderer FindRenderer(GameObject instance, string characterName, string part)
        {
            Transform node = NeighborhoodBuildUtility.FindDeep(instance.transform, $"{characterName}_{part}");
            if (node != null && node.TryGetComponent(out Renderer renderer))
            {
                return renderer;
            }

            Debug.LogWarning($"[Neighborhood] {characterName} has no face part {part}.");
            return null;
        }

        /// <summary>The turn toward the viewer while speaking, on the upper spine, neck and head.</summary>
        private static void BuildLook(GameObject instance, NeighborhoodCharacter character, string characterName)
        {
            Transform head = NeighborhoodBuildUtility.FindDeep(instance.transform, HeadBone);
            if (head == null)
            {
                Debug.LogWarning($"[Neighborhood] {characterName} has no {HeadBone} bone; they won't turn to the viewer.");
                return;
            }

            NeighborhoodCharacterLook look = instance.AddComponent<NeighborhoodCharacterLook>();
            look.Configure(character, NeighborhoodBuildUtility.FindDeep(instance.transform, SpineBone),
                NeighborhoodBuildUtility.FindDeep(instance.transform, NeckBone), head,
                MeasureFaceForward(instance, characterName, head));
        }

        /// <summary>
        /// Where the face points in the head bone's own axes. In the bind pose the person faces the model's forward,
        /// so that direction is read from the body mesh's bind pose of the head. Falls back to the current pose.
        /// </summary>
        private static Vector3 MeasureFaceForward(GameObject instance, string characterName, Transform head)
        {
            Transform bodyNode = NeighborhoodBuildUtility.FindDeep(instance.transform, $"{characterName}_{BodyMesh}");
            if (bodyNode != null && bodyNode.TryGetComponent(out SkinnedMeshRenderer body) && body.sharedMesh != null)
            {
                int index = System.Array.IndexOf(body.bones, head);
                Matrix4x4[] bindposes = body.sharedMesh.bindposes;
                if (index >= 0 && index < bindposes.Length)
                {
                    Quaternion headAtBind = (body.transform.localToWorldMatrix * bindposes[index].inverse).rotation;
                    return Quaternion.Inverse(headAtBind) * instance.transform.forward;
                }
            }

            Debug.LogWarning($"[Neighborhood] No bind pose for {characterName}'s head; the face direction comes from the current pose.");
            return Quaternion.Inverse(head.rotation) * instance.transform.forward;
        }

        /// <summary>A light pool at the person's feet that shows while they speak (dark and switched off until then).</summary>
        private static void BuildSpeakerLight(GameObject instance, NeighborhoodCharacter character, Material material)
        {
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (material == null || quad == null)
            {
                Debug.LogWarning("[Neighborhood] No speaker light pool material; the light under whoever speaks is skipped.");
                return;
            }

            GameObject poolObject = new GameObject("Speaker Light");
            poolObject.transform.SetParent(instance.transform, false);
            poolObject.transform.localPosition = new Vector3(0f, SpeakerPoolHeight, 0f);
            poolObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            poolObject.transform.localScale = new Vector3(SpeakerPoolRadius * 2f, SpeakerPoolRadius * 2f, 1f);
            poolObject.AddComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer renderer = poolObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            instance.AddComponent<NeighborhoodSpeakerLight>().Configure(character, renderer);
        }

        private static AudioSource BuildVoice(GameObject instance, NeighborhoodLayoutCharacter entry)
        {
            Transform head = NeighborhoodBuildUtility.FindDeep(instance.transform, HeadBone);
            GameObject voiceObject = new GameObject("Voice");
            voiceObject.transform.SetParent(head != null ? head : instance.transform, false);
            if (head == null)
            {
                voiceObject.transform.localPosition = new Vector3(0f, entry.HeadHeight, 0f);
            }

            AudioSource source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = VoiceMinDistance;
            source.maxDistance = VoiceMaxDistance;
            source.dopplerLevel = 0f;
            AddVoiceGain(voiceObject, entry.Name);
            return source;
        }

        /// <summary>Their level from Voice Levels (the recordings came in quieter than the narrator's lines).</summary>
        private static void AddVoiceGain(GameObject voiceObject, string characterName)
        {
            VoiceLevels levels = AssetDatabase.LoadAssetAtPath<VoiceLevels>(VoiceLevels.AssetPath);
            if (levels == null || !System.Enum.TryParse(characterName, out VoiceRole role))
            {
                Debug.LogWarning($"[Neighborhood] No voice level for {characterName} ({VoiceLevels.AssetPath}); they play as recorded.");
                return;
            }

            voiceObject.AddComponent<VoiceGain>().Configure(levels, role);
        }

        /// <summary>
        /// For each version: sample its pose, then hang that version's hand props on their bones (keeping where
        /// they are, which is in the hand) and build that version's leash.
        /// </summary>
        private static void AttachProps(GameObject instance, NeighborhoodLayoutCharacter entry, Dictionary<string, AnimationClip> clips,
            NeighborhoodLayout layout, Dictionary<string, Transform> elementNodes, Material leashMaterial)
        {
            foreach (NeighborhoodLayoutPose pose in entry.Poses)
            {
                if (!clips.TryGetValue(pose.Clip, out AnimationClip clip))
                {
                    continue;
                }

                clip.SampleAnimation(instance, 0f);
                int versionMask = 1 << pose.Version;
                foreach (NeighborhoodLayoutElement element in layout.Elements)
                {
                    if (element.AttachCharacter != entry.Id || element.Mask != versionMask
                        || !elementNodes.TryGetValue(element.Name, out Transform node))
                    {
                        continue;
                    }

                    Transform bone = NeighborhoodBuildUtility.FindDeep(instance.transform, element.AttachBone);
                    if (bone == null)
                    {
                        Debug.LogWarning($"[Neighborhood] {entry.Name} has no bone {element.AttachBone} for {element.Name}.");
                        continue;
                    }

                    node.SetParent(bone, true);
                }

                foreach (NeighborhoodLayoutLeash leash in layout.Leashes)
                {
                    if (leash.Character == entry.Id && leash.Version == pose.Version)
                    {
                        BuildLeash(instance, leash, elementNodes, leashMaterial);
                    }
                }
            }
        }

        private static void BuildLeash(GameObject instance, NeighborhoodLayoutLeash leash, Dictionary<string, Transform> elementNodes,
            Material material)
        {
            Transform bone = NeighborhoodBuildUtility.FindDeep(instance.transform, leash.Bone);
            if (bone == null || !elementNodes.TryGetValue(leash.Element, out Transform dog))
            {
                Debug.LogWarning($"[Neighborhood] Leash for {leash.Element} skipped: bone {leash.Bone} or the dog is missing.");
                return;
            }

            string versionName = VersionNames[Mathf.Clamp(leash.Version, 0, VersionNames.Length - 1)];
            GameObject grip = new GameObject($"LeashGrip_{versionName}");
            grip.transform.position = leash.Grip;
            grip.transform.SetParent(bone, true);
            GameObject collar = new GameObject("LeashCollar");
            collar.transform.position = leash.Collar;
            collar.transform.SetParent(dog, true);

            GameObject lineObject = new GameObject("Leash");
            lineObject.transform.SetParent(dog, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = LeashWidth;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material;
            line.positionCount = LeashPoints;
            for (int i = 0; i < LeashPoints; i++)
            {
                line.SetPosition(i, Vector3.Lerp(leash.Grip, leash.Collar, i / (float)(LeashPoints - 1)));
            }

            NeighborhoodLeash component = lineObject.AddComponent<NeighborhoodLeash>();
            component.Configure(line, grip.transform, collar.transform);
        }

        #endregion
    }
}
