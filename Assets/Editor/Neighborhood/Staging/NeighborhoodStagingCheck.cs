using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DroneDDW.Neighborhood.Characters;
using DroneDDW.Neighborhood.Versions;
using DroneDDW.Neighborhood.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DroneDDW.Neighborhood.Editor.Staging
{
    /// <summary>
    /// Menu: DroneDDW > Neighborhood > Check Staging In Play Mode. Plays the Neighborhood scene on its own and, in each
    /// version, checks that every person stands in front of the seat: at most 45° from straight ahead and 5 to 12 m
    /// away. With "-stagingCaptures &lt;folder&gt;" on the command line it also saves what the seated viewer sees in each
    /// version (looking ahead, and down at the slider) and a view from above. Then each person says their introduction:
    /// their head turns toward the seat (as far as the look's limits allow) and the light under them comes on, and
    /// both go back once the line has ended. Runs headless too:
    /// -batchmode -executeMethod DroneDDW.Neighborhood.Editor.Staging.NeighborhoodStagingCheck.Run (no -quit), which
    /// quits with code 1 on a failure.
    /// </summary>
    [InitializeOnLoad]
    internal static class NeighborhoodStagingCheck
    {
        #region Entities

        private sealed class CheckStep
        {
            internal CheckStep(string name, float waitSeconds, Func<string> run, Func<bool> isReady = null)
            {
                Name = name;
                WaitSeconds = waitSeconds;
                Run = run;
                IsReady = isReady;
            }

            internal string Name { get; }

            // Seconds after the previous step before this one runs
            internal float WaitSeconds { get; }

            // Null when the step went as expected, otherwise what went wrong
            internal Func<string> Run { get; }

            // When set, the step also waits for this (up to ReadyTimeoutSeconds)
            internal Func<bool> IsReady { get; }
        }

        #endregion

        #region Fields

        private const string MenuPath = "DroneDDW/Neighborhood/Check Staging In Play Mode";
        private const string ScenePath = "Assets/Scenes/Neighborhood/Neighborhood.unity";
        private const string RunningKey = "DroneDDW.Neighborhood.StagingCheck.Running";
        private const string CapturesKey = "DroneDDW.Neighborhood.StagingCheck.Captures";
        private const string CapturesArgument = "-stagingCaptures";
        private const float StartDelaySeconds = 1.5f;
        private const float SettleSeconds = 0.6f;
        private const float MaxBearingDegrees = 45f;
        private const float MinDistanceMetres = 5f;
        private const float MaxDistanceMetres = 12f;

        // The layout's eye height above the seat
        private const float EyeHeightMetres = 1.2f;
        private const float CaptureFieldOfViewDegrees = 110f;
        private const float SliderPitchDegrees = 35f;
        private const int CaptureWidth = 1600;
        private const int CaptureHeight = 1000;
        private const float OverviewSizeMetres = 30f;
        private const float ReadyTimeoutSeconds = 20f;
        private const float TurnedSeconds = 1f;
        private const float TurnedBackSeconds = 1.2f;
        private const float TurnToleranceDegrees = 6f;
        private const float FullLevel = 0.95f;
        private const float OffLevel = 0.01f;

        // After the held change has arrived, how long nobody may speak
        private const float HeldSilenceSeconds = 1.5f;

        private static readonly Dictionary<NeighborhoodCharacterId, string> IntroductionLines =
            new Dictionary<NeighborhoodCharacterId, string>
            {
                { NeighborhoodCharacterId.Grandma, "GRA_01" },
                { NeighborhoodCharacterId.Worker, "WOR_01" },
                { NeighborhoodCharacterId.Student, "STU_01" },
            };

        // The turn each person still needs when they start speaking, to compare with where they end up
        private static readonly Dictionary<NeighborhoodCharacterId, Vector2> NeededTurns =
            new Dictionary<NeighborhoodCharacterId, Vector2>();

        private static Transform Viewer;

        // Errors the XR runtime logs when no headset is connected (as in a headless run): reported, but not failures
        private static readonly string[] HeadsetMissingPrefixes =
        {
            "[MetaXRFeature]", "Failed to initialize Open XR", "Unable to set TrackingOrigin",
        };

        private static readonly List<CheckStep> Steps = new List<CheckStep>();
        private static readonly List<string> Failures = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly List<string> Notes = new List<string>();

        private static float StartTime = -1f;
        private static int StepIndex = -1;
        private static float StepTime;

        #endregion

        #region Setup

        static NeighborhoodStagingCheck()
        {
            if (SessionState.GetBool(RunningKey, false))
            {
                Subscribe();
            }
        }

        #endregion

        #region Private

        [MenuItem(MenuPath)]
        private static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError($"[Neighborhood] Staging check: there is no {ScenePath}; build the Neighborhood scene first.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // Play mode may start without a domain reload, which keeps these from a previous run
            StartTime = -1f;
            StepIndex = -1;
            Failures.Clear();
            Errors.Clear();
            Notes.Clear();
            NeededTurns.Clear();
            Viewer = null;
            SessionState.SetString(CapturesKey, ReadCapturesFolder());
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(RunningKey, true);
            Subscribe();
            EditorApplication.EnterPlaymode();
        }

        private static string ReadCapturesFolder()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, CapturesArgument);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;
        }

        // Also called when Play mode starts without a domain reload
        private static void Subscribe()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= HandleLog;
            Application.logMessageReceived += HandleLog;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (StepIndex < 0)
            {
                if (FindController() == null)
                {
                    return;
                }

                if (StartTime < 0f)
                {
                    StartTime = now;
                    return;
                }

                if (now - StartTime < StartDelaySeconds)
                {
                    return;
                }

                BuildSteps();
                StepIndex = 0;
                StepTime = now;
                return;
            }

            CheckStep step = Steps[StepIndex];
            if (now - StepTime < step.WaitSeconds)
            {
                return;
            }

            bool isTimedOut = now - StepTime >= step.WaitSeconds + ReadyTimeoutSeconds;
            if (step.IsReady != null && !step.IsReady() && !isTimedOut)
            {
                return;
            }

            string failure;
            try
            {
                failure = step.IsReady != null && !step.IsReady() ? $"timed out after {ReadyTimeoutSeconds:0} s" : step.Run();
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                Debug.LogException(exception);
            }

            if (failure != null)
            {
                Failures.Add($"{step.Name}: {failure}");
                Debug.Log($"[Neighborhood] Staging check step {StepIndex + 1} failed, {step.Name}: {failure}");
            }
            else
            {
                Debug.Log($"[Neighborhood] Staging check step {StepIndex + 1} passed: {step.Name}.");
            }

            StepIndex++;
            StepTime = now;
            if (StepIndex >= Steps.Count)
            {
                Finish();
            }
        }

        private static void BuildSteps()
        {
            Steps.Clear();
            string captures = SessionState.GetString(CapturesKey, string.Empty);
            foreach (NeighborhoodVersion version in new[] { NeighborhoodVersion.Nature, NeighborhoodVersion.Balanced, NeighborhoodVersion.Efficiency })
            {
                Steps.Add(new CheckStep($"the street switches to {version}", 0f, () => ShowVersion(version)));
                Steps.Add(new CheckStep($"everyone stands in front of the seat in {version}", SettleSeconds, CheckPlaces));
                if (!string.IsNullOrEmpty(captures))
                {
                    Steps.Add(new CheckStep($"the views in {version} are saved", 0f, () => SaveViews(captures, version)));
                }
            }

            Steps.Add(new CheckStep("the street switches back to Balanced", 0f, () => ShowVersion(NeighborhoodVersion.Balanced)));
            foreach (NeighborhoodCharacterId id in IntroductionLines.Keys)
            {
                Steps.Add(new CheckStep($"the {id} starts their introduction", SettleSeconds, () => StartLine(id)));
                Steps.Add(new CheckStep($"the {id} turns to the seat and the light under them is on", TurnedSeconds,
                    () => CheckTurned(id)));
                if (!string.IsNullOrEmpty(captures))
                {
                    Steps.Add(new CheckStep($"the view while the {id} speaks is saved", 0f, () => SaveSpeaking(captures, id)));
                }

                Steps.Add(new CheckStep($"the {id}'s line ends", 0f, () => null, () => !FindVoice().IsPlaying));
                Steps.Add(new CheckStep($"the {id} turns back and the light goes out", TurnedBackSeconds,
                    () => CheckTurnedBack(id)));
            }

            // A change of the street: once it has arrived, the version's three people react one after another
            Steps.Add(new CheckStep("the street changes to Efficiency", 0f, () => RequestVersion(NeighborhoodVersion.Efficiency)));
            foreach (string line in FindVoice().GetReactionIds(NeighborhoodVersion.Efficiency, FindVoice().Stage))
            {
                Steps.Add(new CheckStep($"{line} plays after the change", 0f, () => null, () => IsSaying(line)));
            }

            // While the slider is held the street follows it, but the people wait until it is let go
            Steps.Add(new CheckStep("the line ends", 0f, () => null, () => !FindVoice().IsPlaying));
            Steps.Add(new CheckStep("the slider is held and the street changes to Nature", 0f, HoldAndChangeToNature));
            Steps.Add(new CheckStep("the street arrives at Nature", 0f, () => null, () => !FindController().IsTransitioning));
            Steps.Add(new CheckStep("nobody reacts while the slider is held", HeldSilenceSeconds, CheckSilent));
            Steps.Add(new CheckStep("the slider is let go", 0f, ReleaseReactions));
            string firstNatureLine = FindVoice().GetReactionIds(NeighborhoodVersion.Nature, FindVoice().Stage).FirstOrDefault();
            Steps.Add(new CheckStep($"{firstNatureLine} plays once it is let go", 0f, () => null,
                () => IsSaying(firstNatureLine)));
            Steps.Add(new CheckStep("the voices stop", 0f, StopVoices));
        }

        private static string RequestVersion(NeighborhoodVersion version)
        {
            FindController().RequestVersion(version);
            return null;
        }

        private static bool IsSaying(string lineId)
        {
            NeighborhoodVoiceLine line = FindVoice().CurrentLine;
            return line != null && line.Id == lineId;
        }

        private static string HoldAndChangeToNature()
        {
            FindVoice().HoldReactions();
            FindController().RequestVersion(NeighborhoodVersion.Nature);
            return null;
        }

        private static string CheckSilent()
        {
            NeighborhoodVoiceLine line = FindVoice().CurrentLine;
            return line != null ? $"{line.Id} started while the slider was held" : null;
        }

        private static string ReleaseReactions()
        {
            FindVoice().ReleaseReactions();
            return null;
        }

        private static string StopVoices()
        {
            FindVoice().Stop();
            return null;
        }

        private static string StartLine(NeighborhoodCharacterId id)
        {
            NeighborhoodCharacterLook look = FindLook(id);
            if (look == null)
            {
                return $"the {id} has no NeighborhoodCharacterLook";
            }

            if (Viewer == null)
            {
                Viewer = new GameObject("Staging check viewer").transform;
                Viewer.position = FindController().WaveOrigin + Vector3.up * EyeHeightMetres;
            }

            look.SetTarget(Viewer);
            NeededTurns[id] = MeasureTurn(look);
            FindVoice().PlayLine(IntroductionLines[id]);
            return look.Weight > OffLevel ? $"the {id} was already turned (weight {look.Weight:0.00})" : null;
        }

        private static string CheckTurned(NeighborhoodCharacterId id)
        {
            NeighborhoodCharacterLook look = FindLook(id);
            NeighborhoodSpeakerLight light = FindLight(id);
            Vector2 left = MeasureTurn(look);
            Vector2 needed = NeededTurns[id];
            float expectedYaw = Mathf.Sign(needed.x) * Mathf.Max(0f, Mathf.Abs(needed.x) - look.MaxYawDegrees);
            float expectedPitch = Mathf.Sign(needed.y) * Mathf.Max(0f, Mathf.Abs(needed.y) - look.MaxPitchDegrees);
            Notes.Add($"{id} needed a turn of {needed.x:0}° sideways and {needed.y:0}° down; left over while speaking: "
                + $"{left.x:0}° and {left.y:0}° (limits {look.MaxYawDegrees:0}° and {look.MaxPitchDegrees:0}°)");
            List<string> problems = new List<string>();
            if (look.Weight < FullLevel)
            {
                problems.Add($"the turn is at {look.Weight:0.00}");
            }

            if (Mathf.Abs(left.x - expectedYaw) > TurnToleranceDegrees || Mathf.Abs(left.y - expectedPitch) > TurnToleranceDegrees)
            {
                problems.Add($"the face is {left.x:0}° and {left.y:0}° off the seat, expected {expectedYaw:0}° and {expectedPitch:0}°");
            }

            if (light == null)
            {
                problems.Add("there is no light under them");
            }
            else if (light.Level < FullLevel)
            {
                problems.Add($"the light is at {light.Level:0.00}");
            }

            return problems.Count > 0 ? string.Join("; ", problems) : null;
        }

        private static string CheckTurnedBack(NeighborhoodCharacterId id)
        {
            NeighborhoodCharacterLook look = FindLook(id);
            NeighborhoodSpeakerLight light = FindLight(id);
            List<string> problems = new List<string>();
            if (look.Weight > OffLevel)
            {
                problems.Add($"the turn is still at {look.Weight:0.00}");
            }

            if (light != null && light.Level > OffLevel)
            {
                problems.Add($"the light is still at {light.Level:0.00}");
            }

            return problems.Count > 0 ? string.Join("; ", problems) : null;
        }

        // Sideways (x) and downward (y) turn from where the face points to the viewer, unclamped
        private static Vector2 MeasureTurn(NeighborhoodCharacterLook look)
        {
            Vector3 face = look.Head.rotation * look.FaceForward;
            NeighborhoodCharacterLook.TryComputeTurn(face, Viewer.position - look.Head.position, 180f, 90f,
                out float yaw, out float pitch);
            return new Vector2(yaw, pitch);
        }

        private static string SaveSpeaking(string folder, NeighborhoodCharacterId id)
        {
            Directory.CreateDirectory(folder);
            GameObject cameraObject = new GameObject("Staging check camera");
            try
            {
                Vector3 head = FindLook(id).Head.position;
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 200f;
                camera.fieldOfView = 12f;
                cameraObject.transform.SetPositionAndRotation(Viewer.position,
                    Quaternion.LookRotation(head + Vector3.down * 0.4f - Viewer.position, Vector3.up));
                Save(camera, Path.Combine(folder, $"{id} speaking.png"));
            }
            finally
            {
                Object.Destroy(cameraObject);
            }

            return null;
        }

        private static NeighborhoodCharacterLook FindLook(NeighborhoodCharacterId id)
        {
            NeighborhoodCharacter character = FindCharacters().FirstOrDefault(item => item.Id == id);
            return character != null && character.TryGetComponent(out NeighborhoodCharacterLook look) ? look : null;
        }

        private static NeighborhoodSpeakerLight FindLight(NeighborhoodCharacterId id)
        {
            NeighborhoodCharacter character = FindCharacters().FirstOrDefault(item => item.Id == id);
            return character != null && character.TryGetComponent(out NeighborhoodSpeakerLight light) ? light : null;
        }

        private static NeighborhoodVoiceDirector FindVoice()
        {
            return Object.FindAnyObjectByType<NeighborhoodVoiceDirector>();
        }

        private static string ShowVersion(NeighborhoodVersion version)
        {
            FindController().SetVersionImmediate(version);
            return null;
        }

        private static string CheckPlaces()
        {
            Vector3 seat = FindController().WaveOrigin;
            NeighborhoodCharacter[] characters = FindCharacters();
            if (characters.Length != 3)
            {
                return $"{characters.Length} people in the street, expected 3";
            }

            List<string> problems = new List<string>();
            foreach (NeighborhoodCharacter character in characters)
            {
                Vector3 offset = character.transform.position - seat;
                float bearing = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                float distance = new Vector2(offset.x, offset.z).magnitude;
                string place = NeighborhoodStagingBuilder.Describe(offset);
                Notes.Add($"{character.Id}: {place}");
                if (Mathf.Abs(bearing) > MaxBearingDegrees || distance < MinDistanceMetres || distance > MaxDistanceMetres)
                {
                    problems.Add($"{character.Id} is {place}");
                }
            }

            return problems.Count > 0 ? string.Join("; ", problems) : null;
        }

        private static string SaveViews(string folder, NeighborhoodVersion version)
        {
            Directory.CreateDirectory(folder);
            Vector3 eye = FindController().WaveOrigin + Vector3.up * EyeHeightMetres;
            GameObject cameraObject = new GameObject("Staging check camera");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 1100f;
                camera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(CaptureFieldOfViewDegrees, CaptureWidth / (float)CaptureHeight);
                cameraObject.transform.SetPositionAndRotation(eye, Quaternion.identity);
                Save(camera, Path.Combine(folder, $"{version} ahead.png"));
                cameraObject.transform.rotation = Quaternion.Euler(SliderPitchDegrees, 0f, 0f);
                Save(camera, Path.Combine(folder, $"{version} slider.png"));

                camera.orthographic = true;
                camera.orthographicSize = OverviewSizeMetres * 0.5f * CaptureHeight / CaptureWidth;
                cameraObject.transform.SetPositionAndRotation(eye + new Vector3(0f, 60f, OverviewSizeMetres * 0.25f),
                    Quaternion.Euler(90f, 0f, 0f));
                Save(camera, Path.Combine(folder, $"{version} above.png"));
            }
            finally
            {
                Object.Destroy(cameraObject);
            }

            return null;
        }

        private static void Save(Camera camera, string path)
        {
            RenderTexture target = RenderTexture.GetTemporary(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    camera.targetTexture = target;
                    camera.Render();
                    camera.targetTexture = null;
                }

                RenderTexture.active = target;
                Texture2D image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.Destroy(image);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static NeighborhoodVersionController FindController()
        {
            return Object.FindAnyObjectByType<NeighborhoodVersionController>();
        }

        private static NeighborhoodCharacter[] FindCharacters()
        {
            return Object.FindObjectsByType<NeighborhoodCharacter>(FindObjectsSortMode.None).OrderBy(character => character.Id).ToArray();
        }

        private static void HandleLog(string message, string stackTrace, LogType type)
        {
            bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (isError && !HeadsetMissingPrefixes.Any(message.StartsWith))
            {
                Errors.Add($"{message}\n{stackTrace}");
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= HandleLog;
            SessionState.SetBool(RunningKey, false);
            foreach (string note in Notes.Distinct())
            {
                Debug.Log($"[Neighborhood] Staging check: {note}");
            }

            foreach (string failure in Failures)
            {
                Debug.Log($"[Neighborhood] Staging check failure: {failure}");
            }

            foreach (string error in Errors.Take(10))
            {
                Debug.Log($"[Neighborhood] Staging check error: {error}");
            }

            bool isPassed = Failures.Count == 0 && Errors.Count == 0;
            Debug.Log($"[Neighborhood] Staging check done: {(isPassed ? "passed" : "failed")}, {Steps.Count} steps, "
                + $"{Failures.Count} failures, {Errors.Count} errors.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(isPassed ? 0 : 1);
                return;
            }

            EditorApplication.ExitPlaymode();
        }

        #endregion
    }
}
