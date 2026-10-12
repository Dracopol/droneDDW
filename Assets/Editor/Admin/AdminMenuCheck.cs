using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DroneDDW.Admin.Sessions;
using DroneDDW.Admin.TestScene;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace DroneDDW.Admin.Editor
{
	// Menu: DroneDDW > Admin Menu > Check Admin Menu In Play Mode. Plays the admin menu test scene and works the menu
	// through its real input bindings with two simulated controllers (an XR controller layout with the Quest Touch
	// controller's usages) and a simulated keyboard: opening and pausing, moving the hover with both sticks (with
	// repeat and no wrap-around), the history with a delete cancelled by B and one confirmed, scrolling to the last
	// row, the end and reset dialogs, Reset player location after the rig has drifted, closing with Esc, and Reset
	// session reloading the scene. (The check-in screen is checked by the experience's Play mode check: this scene
	// runs without check-in.) The history reads a temporary file with 14 made-up sessions in the temp folder, deleted
	// afterwards; the real session files are never touched. Reports every step and the errors logged, and also runs
	// headless (-batchmode -executeMethod DroneDDW.Admin.Editor.AdminMenuCheck.Run), quitting with code 1 on a failure.
	[InitializeOnLoad]
	internal static class AdminMenuCheck
	{
		#region Entities

		private enum Control
		{
			None,
			LeftMenu,
			LeftStickUp,
			LeftStickDown,
			LeftStickRight,
			RightStickDown,
			LeftTrigger,
			RightTrigger,
			LeftGrip,
			RightSecondary,
			KeyEscape,
			KeyUp,
			KeyDown,
			KeyRight,
			KeyEnter,
		}

		private sealed class CheckStep
		{
			internal CheckStep(string name, Control control, float holdSeconds, Func<string> verify, Action before,
				float settleSeconds)
			{
				Name = name;
				Control = control;
				HoldSeconds = holdSeconds;
				Verify = verify;
				Before = before;
				SettleSeconds = settleSeconds;
			}

			internal string Name { get; }
			internal Control Control { get; }
			internal float HoldSeconds { get; }

			// Null when the step went as expected, otherwise what went wrong
			internal Func<string> Verify { get; }

			internal Action Before { get; }
			internal float SettleSeconds { get; }
		}

		#endregion

		#region Fields

		private const string MenuPath = "DroneDDW/Admin Menu/Check Admin Menu In Play Mode";
		private const string RunningKey = "DroneDDW.Admin.AdminMenuCheck.Running";
		private const string LayoutName = "AdminMenuCheckController";
		private const float StartDelaySeconds = 1.5f;
		private const float TapSeconds = 0.1f;
		private const float SettleSeconds = 0.35f;
		private const float ReloadSettleSeconds = 2f;
		private const int SampleSessions = 14;

		// A controller layout with the Touch controller's usages, so the menu's bindings resolve as on the Quest
		private const string LayoutJson = "{ \"name\": \"" + LayoutName + "\", \"extend\": \"XRController\", "
			+ "\"controls\": ["
			+ "{ \"name\": \"thumbstick\", \"layout\": \"Vector2\", \"usage\": \"Primary2DAxis\" },"
			+ "{ \"name\": \"triggerPressed\", \"layout\": \"Button\", \"usage\": \"TriggerButton\" },"
			+ "{ \"name\": \"gripPressed\", \"layout\": \"Button\", \"usage\": \"GripButton\" },"
			+ "{ \"name\": \"menu\", \"layout\": \"Button\", \"usage\": \"MenuButton\" },"
			+ "{ \"name\": \"secondaryButton\", \"layout\": \"Button\", \"usage\": \"SecondaryButton\" } ] }";

		// Errors the XR runtime logs when no headset is connected (as in a headless run): reported, but not failures
		private static readonly string[] HeadsetMissingPrefixes =
		{
			"[MetaXRFeature]", "Failed to initialize Open XR", "Unable to set TrackingOrigin",
		};

		private static readonly string[] FormChoices =
		{
			"nature max", "nature skewed", "neutral", "efficiency skewed", "max efficiency",
		};

		private static readonly List<CheckStep> Steps = new();
		private static readonly List<string> Failures = new();
		private static readonly List<string> Errors = new();
		private static readonly List<string> Warnings = new();

		private static float StartTime = -1f;
		private static int StepIndex = -1;
		private static float StepStartTime;
		private static bool IsReleased;
		private static InputDevice LeftController;
		private static InputDevice RightController;
		private static Keyboard CheckKeyboard;
		private static string TempFolder;
		private static float ElapsedWhenPaused;
		private static int MenuIdBeforeReset;
		private static int SessionLinesBeforeSave;
		private static float PanelHeightWithTestSave;
		private static InputSettings.EditorInputBehaviorInPlayMode PreviousInputBehavior;
		private static InputSettings.BackgroundBehavior PreviousBackgroundBehavior;

		#endregion

		#region Setup

		static AdminMenuCheck()
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
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(AdminMenuTestSceneBuilder.ScenePath) == null)
			{
				Debug.LogError($"[Admin] Check: there is no {AdminMenuTestSceneBuilder.ScenePath}; run DroneDDW > "
					+ "Admin Menu > Create Admin Menu Test Scene first.");
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

			EditorSceneManager.OpenScene(AdminMenuTestSceneBuilder.ScenePath);
			SessionState.SetBool(RunningKey, true);
			Subscribe();
			EditorApplication.EnterPlaymode();
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
				AdminMenu menu = FindMenu();
				if (menu == null)
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

				Prepare(menu);
				StartStep(0, now);
				return;
			}

			CheckStep step = Steps[StepIndex];
			if (!IsReleased && now - StepStartTime >= step.HoldSeconds)
			{
				SetControl(step.Control, false);
				IsReleased = true;
			}

			if (now - StepStartTime < step.HoldSeconds + step.SettleSeconds)
			{
				return;
			}

			string failure = FindMenu() == null ? "the admin menu is gone" : step.Verify?.Invoke();
			if (failure != null)
			{
				Failures.Add($"{step.Name}: {failure}");
				Debug.Log($"[Admin] Check step {StepIndex + 1} failed, {step.Name}: {failure}");
			}
			else
			{
				Debug.Log($"[Admin] Check step {StepIndex + 1} passed: {step.Name}.");
			}

			if (StepIndex + 1 < Steps.Count)
			{
				StartStep(StepIndex + 1, now);
			}
			else
			{
				Finish();
			}
		}

		private static void StartStep(int index, float now)
		{
			StepIndex = index;
			StepStartTime = now;
			CheckStep step = Steps[index];
			step.Before?.Invoke();
			IsReleased = step.Control == Control.None;
			SetControl(step.Control, true);
		}

		// The simulated devices, the temporary history, and the steps
		private static void Prepare(AdminMenu menu)
		{
			// Without this, the simulated input is dropped: a headless editor (or one in the background) has no focus,
			// and by default the Input System disables devices while the app is unfocused
			PreviousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
			PreviousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
			InputSystem.settings.editorInputBehaviorInPlayMode =
				InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
			InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
			InputSystem.RegisterLayout(LayoutJson);
			LeftController = InputSystem.AddDevice(LayoutName, "Admin Check Left Controller");
			InputSystem.SetDeviceUsage(LeftController, CommonUsages.LeftHand);
			RightController = InputSystem.AddDevice(LayoutName, "Admin Check Right Controller");
			InputSystem.SetDeviceUsage(RightController, CommonUsages.RightHand);
			CheckKeyboard = InputSystem.AddDevice<Keyboard>("Admin Check Keyboard");

			TempFolder = Path.Combine(Path.GetTempPath(), "DroneDDW Admin Menu Check");
			if (Directory.Exists(TempFolder))
			{
				Directory.Delete(TempFolder, true);
			}

			Directory.CreateDirectory(TempFolder);
			File.WriteAllText(Path.Combine(TempFolder, "Sessions.csv"), MakeSampleSessions(), new UTF8Encoding(true));
			menu.UseSessionsFolder(TempFolder);
			Debug.Log($"[Admin] Check: {SampleSessions} made-up sessions in {TempFolder}.");

			BuildSteps();
		}

		private static void BuildSteps()
		{
			Steps.Clear();
			Add("the left menu button opens the menu and pauses", Control.LeftMenu,
				() => Expect(true, Menu.PauseScreen, Menu.ResumeButton) ?? ExpectPaused(true));
			Add("the session clock stands still while paused", Control.None, CheckClockStopped, RememberClock);
			Add("left stick down moves the hover", Control.LeftStickDown,
				() => Expect(true, Menu.PauseScreen, Menu.ResetButton));
			Add("right stick down moves it too", Control.RightStickDown,
				() => Expect(true, Menu.PauseScreen, Menu.EndButton));
			Add("holding the stick repeats and stops at the last button", Control.LeftStickDown,
				() => Expect(true, Menu.PauseScreen, Menu.TestSaveButton), holdSeconds: 0.9f);
			Add("pushing right where nothing is leaves the hover", Control.LeftStickRight,
				() => Expect(true, Menu.PauseScreen, Menu.TestSaveButton));
			Add("stick up", Control.LeftStickUp, () => Expect(true, Menu.PauseScreen, Menu.LanguageButton));
			Add("stick up again", Control.LeftStickUp, () => Expect(true, Menu.PauseScreen, Menu.LocationButton));
			Add("and again", Control.LeftStickUp, () => Expect(true, Menu.PauseScreen, Menu.HistoryButton));
			Add("the right trigger opens the history on Go back", Control.RightTrigger, CheckHistoryOpened);
			Add("down reaches the first row's Delete", Control.LeftStickDown,
				() => Expect(true, Menu.HistoryScreen, Menu.History.GetDeleteButton(0)));
			Add("the left grip asks to delete it, on Cancel", Control.LeftGrip, CheckDeleteDialog);
			Add("B cancels back to the row", Control.RightSecondary,
				() => Expect(true, Menu.HistoryScreen, Menu.History.GetDeleteButton(0)));
			Add("the trigger asks again", Control.LeftTrigger,
				() => Expect(true, Menu.DeleteScreen, Menu.DeleteCancelButton));
			Add("right reaches Delete session", Control.LeftStickRight,
				() => Expect(true, Menu.DeleteScreen, Menu.DeleteConfirmButton));
			Add("the trigger deletes the session", Control.RightTrigger, CheckDeleted);
			Add("holding down scrolls to the last row", Control.LeftStickDown, CheckScrolledToEnd, holdSeconds: 2.6f);
			Add("B goes back to Paused on View session history", Control.RightSecondary,
				() => Expect(true, Menu.PauseScreen, Menu.HistoryButton));
			Add("up to End session", Control.LeftStickUp, () => Expect(true, Menu.PauseScreen, Menu.EndButton));
			Add("the end dialog opens on Cancel", Control.RightTrigger,
				() => Expect(true, Menu.EndScreen, Menu.EndCancelButton));
			Add("B cancels it, back on End session", Control.RightSecondary,
				() => Expect(true, Menu.PauseScreen, Menu.EndButton));
			Add("up to Reset session", Control.LeftStickUp, () => Expect(true, Menu.PauseScreen, Menu.ResetButton));
			Add("the reset dialog opens on Cancel", Control.RightTrigger,
				() => Expect(true, Menu.ResetScreen, Menu.ResetCancelButton));
			Add("B cancels it", Control.RightSecondary, () => Expect(true, Menu.PauseScreen, Menu.ResetButton));
			Add("down", Control.LeftStickDown, () => Expect(true, Menu.PauseScreen, Menu.EndButton));
			Add("down again", Control.LeftStickDown, () => Expect(true, Menu.PauseScreen, Menu.HistoryButton));
			Add("down to Reset player location", Control.LeftStickDown,
				() => Expect(true, Menu.PauseScreen, Menu.LocationButton));
			Add("Reset player location puts the head over the Player Spot", Control.RightTrigger,
				CheckPlayerLocation, MoveRigAway);
			Add("down to Change player language", Control.LeftStickDown,
				() => Expect(true, Menu.PauseScreen, Menu.LanguageButton));
			Add("Change player language does nothing yet", Control.RightTrigger,
				() => Expect(true, Menu.PauseScreen, Menu.LanguageButton));
			Add("down to Test save current session", Control.LeftStickDown,
				() => Expect(true, Menu.PauseScreen, Menu.TestSaveButton));
			Add("the trigger saves a test row and says so", Control.RightTrigger, CheckTestSaved, RememberSessionCount);
			Add("the button reads its own label again", Control.None, CheckTestSaveLabel, settleSeconds: 2.8f);
			Add("Esc closes the menu and resumes", Control.KeyEscape, CheckClosed, settleSeconds: 0.6f);
			Add("with the checkbox off, Esc opens it without the test save button", Control.KeyEscape,
				() => Expect(true, Menu.PauseScreen, Menu.ResumeButton) ?? CheckTestSaveHidden(), HideTestSave);
			Add("holding the down arrow stops at Change player language", Control.KeyDown,
				() => Expect(true, Menu.PauseScreen, Menu.LanguageButton), holdSeconds: 1.2f);
			Add("up arrow", Control.KeyUp, () => Expect(true, Menu.PauseScreen, Menu.LocationButton));
			Add("up arrow again", Control.KeyUp, () => Expect(true, Menu.PauseScreen, Menu.HistoryButton));
			Add("and again, to End session", Control.KeyUp, () => Expect(true, Menu.PauseScreen, Menu.EndButton));
			Add("and again, to Reset session", Control.KeyUp, () => Expect(true, Menu.PauseScreen, Menu.ResetButton));
			Add("Enter opens the reset dialog", Control.KeyEnter,
				() => Expect(true, Menu.ResetScreen, Menu.ResetCancelButton));
			Add("the right arrow reaches Reset session", Control.KeyRight,
				() => Expect(true, Menu.ResetScreen, Menu.ResetConfirmButton));
			Add("Enter resets: the scene reloads, closed and running", Control.KeyEnter, CheckReset, RememberMenu,
				settleSeconds: ReloadSettleSeconds);
		}

		private static void Add(string name, Control control, Func<string> verify, Action before = null,
			float holdSeconds = TapSeconds, float settleSeconds = SettleSeconds)
		{
			Steps.Add(new CheckStep(name, control, holdSeconds, verify, before, settleSeconds));
		}

		private static AdminMenu Menu => FindMenu();

		private static AdminMenu FindMenu()
		{
			return Object.FindFirstObjectByType<AdminMenu>();
		}

		// Null when the menu is open (or closed) on that screen with that button hovered
		private static string Expect(bool isOpen, AdminMenuScreen screen, AdminMenuButton hovered)
		{
			AdminMenu menu = Menu;
			if (menu.IsOpen != isOpen)
			{
				return $"the menu is {(menu.IsOpen ? "open" : "closed")}";
			}

			if (menu.CurrentScreen != screen)
			{
				return $"the screen is {Describe(menu.CurrentScreen)}, not {Describe(screen)}";
			}

			if (menu.Hovered != hovered)
			{
				return $"the hover is on {Describe(menu.Hovered)}, not {Describe(hovered)}";
			}

			return null;
		}

		private static string ExpectPaused(bool isPaused)
		{
			bool isTimeStopped = Mathf.Approximately(Time.timeScale, 0f);
			if (isTimeStopped != isPaused || AudioListener.pause != isPaused)
			{
				return $"time scale {Time.timeScale}, audio {(AudioListener.pause ? "paused" : "playing")}";
			}

			float opacity = Menu.Dimmer.Opacity;
			if (isPaused && opacity <= 0f)
			{
				return "the world isn't dimmed";
			}

			return null;
		}

		private static void RememberClock()
		{
			ElapsedWhenPaused = Object.FindFirstObjectByType<AdminMenuDemoSession>().ElapsedSeconds;
		}

		private static string CheckClockStopped()
		{
			float elapsed = Object.FindFirstObjectByType<AdminMenuDemoSession>().ElapsedSeconds;
			return Mathf.Approximately(elapsed, ElapsedWhenPaused)
				? null
				: $"the clock went on from {ElapsedWhenPaused:0.00} to {elapsed:0.00} s";
		}

		private static string CheckHistoryOpened()
		{
			SessionHistoryScreen history = Menu.History;
			string state = Expect(true, Menu.HistoryScreen, history.BackButton);
			if (state != null)
			{
				return state;
			}

			if (history.RowCount != SampleSessions || history.FooterText != $"{SampleSessions} sessions")
			{
				return $"{history.RowCount} rows and footer \"{history.FooterText}\", not {SampleSessions}";
			}

			return null;
		}

		private static string CheckDeleteDialog()
		{
			string state = Expect(true, Menu.DeleteScreen, Menu.DeleteCancelButton);
			if (state != null)
			{
				return state;
			}

			return Menu.DeleteDetailsText.Contains("001") ? null : $"the details read \"{Menu.DeleteDetailsText}\"";
		}

		// Row 001 left Sessions.csv (the others and the extra column kept) and is in Deleted Sessions.csv
		private static string CheckDeleted()
		{
			SessionHistoryScreen history = Menu.History;
			string state = Expect(true, Menu.HistoryScreen, history.GetDeleteButton(0));
			if (state != null)
			{
				return state;
			}

			if (history.RowCount != SampleSessions - 1)
			{
				return $"{history.RowCount} rows after the delete";
			}

			List<string[]> sessions = ReadCsv("Sessions.csv");
			List<string[]> deleted = ReadCsv("Deleted Sessions.csv");
			if (sessions.Count != SampleSessions || sessions[0].Last() != "Answer 1" || sessions[1][0] != "002")
			{
				return $"Sessions.csv has {sessions.Count - 1} rows, header {string.Join("|", sessions[0])}, first "
					+ $"{sessions[1][0]}";
			}

			if (deleted.Count != 2 || deleted[0].Last() != SessionColumns.DeletedAt || deleted[1][0] != "001"
				|| deleted[1][5] != "answer 1")
			{
				return $"Deleted Sessions.csv is {string.Join(" / ", deleted.Select(row => string.Join("|", row)))}";
			}

			return null;
		}

		private static string CheckScrolledToEnd()
		{
			SessionHistoryScreen history = Menu.History;
			string state = Expect(true, Menu.HistoryScreen, history.GetDeleteButton(history.RowCount - 1));
			if (state != null)
			{
				return state;
			}

			return history.ScrollPixels > 0f ? null : "the list didn't scroll";
		}

		// As if the participant had moved and turned: the rig shifted and turned away from the spot
		private static void MoveRigAway()
		{
			XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
			origin.transform.SetPositionAndRotation(new Vector3(1.5f, 0f, -0.7f), Quaternion.Euler(0f, 70f, 0f));
		}

		private static string CheckPlayerLocation()
		{
			string state = Expect(true, Menu.PauseScreen, Menu.LocationButton);
			if (state != null)
			{
				return state;
			}

			PlayerSpot spot = Object.FindFirstObjectByType<PlayerSpot>();
			Transform head = Object.FindFirstObjectByType<XROrigin>().Camera.transform;
			Vector3 offset = head.position - spot.Position;
			offset.y = 0f;
			Vector3 headForward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
			float angle = Vector3.Angle(headForward, spot.Forward);
			if (offset.magnitude > 0.01f || angle > 1f)
			{
				return $"the head is {offset.magnitude:0.000} m from the spot, {angle:0.0} degrees off its facing";
			}

			return null;
		}

		private static void RememberSessionCount()
		{
			SessionLinesBeforeSave = ReadCsv("Sessions.csv").Count;
		}

		// One more row in the temporary Sessions.csv: 0, A, no, neutral, now, and empty under its extra column
		private static string CheckTestSaved()
		{
			string state = Expect(true, Menu.PauseScreen, Menu.TestSaveButton);
			if (state != null)
			{
				return state;
			}

			List<string[]> sessions = ReadCsv("Sessions.csv");
			if (sessions.Count != SessionLinesBeforeSave + 1)
			{
				return $"Sessions.csv went from {SessionLinesBeforeSave} to {sessions.Count} lines";
			}

			DateTime now = DateTime.Now;
			string[] row = sessions[sessions.Count - 1];
			string expected = $"0|A|neutral|{now:yyyy-MM-dd}|";
			string actual = string.Join("|", row);
			bool isTimeRight = row.Length == 6 && (row[4] == now.ToString("HH:mm")
				|| row[4] == now.AddMinutes(-1).ToString("HH:mm"));
			if (!actual.StartsWith(expected) || !isTimeRight || row[5] != string.Empty)
			{
				return $"the saved row is {actual}";
			}

			string label = Menu.TestSaveButton.Label;
			return label.StartsWith("Saved 000 at ") ? null : $"the button reads \"{label}\"";
		}

		private static string CheckTestSaveLabel()
		{
			string label = Menu.TestSaveButton.Label;
			return label == "Test save current session" ? null : $"the button still reads \"{label}\"";
		}

		// As if the checkbox were turned off in the inspector during Play mode
		private static void HideTestSave()
		{
			PanelHeightWithTestSave = Menu.PausePanelHeight;
			Menu.ConfigureTestSave(false);
		}

		private static string CheckTestSaveHidden()
		{
			AdminMenu menu = Menu;
			if (menu.TestSaveButton.IsShown)
			{
				return "the test save button still shows";
			}

			float shrink = PanelHeightWithTestSave - menu.PausePanelHeight;
			return Mathf.Approximately(shrink, 104f) ? null : $"the panel shrank by {shrink} px, not 104";
		}

		private static string CheckClosed()
		{
			AdminMenu menu = Menu;
			if (menu.IsOpen)
			{
				return "the menu is still open";
			}

			if (!Mathf.Approximately(Time.timeScale, 1f) || AudioListener.pause)
			{
				return $"time scale {Time.timeScale}, audio {(AudioListener.pause ? "paused" : "playing")}";
			}

			return menu.Dimmer.Opacity > 0f ? $"the dimming is still at {menu.Dimmer.Opacity:0.00}" : null;
		}

		private static void RememberMenu()
		{
			MenuIdBeforeReset = Menu.GetInstanceID();
		}

		private static string CheckReset()
		{
			AdminMenu menu = Menu;
			if (menu.GetInstanceID() == MenuIdBeforeReset)
			{
				return "the scene didn't reload";
			}

			if (menu.IsOpen || !Mathf.Approximately(Time.timeScale, 1f) || AudioListener.pause)
			{
				return $"after the reload the menu is {(menu.IsOpen ? "open" : "closed")}, time scale "
					+ $"{Time.timeScale}, audio {(AudioListener.pause ? "paused" : "playing")}";
			}

			float elapsed = Object.FindFirstObjectByType<AdminMenuDemoSession>().ElapsedSeconds;
			return elapsed < ReloadSettleSeconds + 0.5f ? null : $"the new session's clock is at {elapsed:0.0} s";
		}

		private static void SetControl(Control control, bool isDown)
		{
			Vector2 stick = Vector2.zero;
			switch (control)
			{
				case Control.None:
					return;
				case Control.LeftMenu:
					SetButton(LeftController, "menu", isDown);
					return;
				case Control.LeftStickUp:
					stick = Vector2.up;
					break;
				case Control.LeftStickDown:
					stick = Vector2.down;
					break;
				case Control.LeftStickRight:
					stick = Vector2.right;
					break;
				case Control.RightStickDown:
					SetStick(RightController, isDown ? Vector2.down : Vector2.zero);
					return;
				case Control.LeftTrigger:
					SetButton(LeftController, "triggerPressed", isDown);
					return;
				case Control.RightTrigger:
					SetButton(RightController, "triggerPressed", isDown);
					return;
				case Control.LeftGrip:
					SetButton(LeftController, "gripPressed", isDown);
					return;
				case Control.RightSecondary:
					SetButton(RightController, "secondaryButton", isDown);
					return;
				case Control.KeyEscape:
					SetKey(Key.Escape, isDown);
					return;
				case Control.KeyUp:
					SetKey(Key.UpArrow, isDown);
					return;
				case Control.KeyDown:
					SetKey(Key.DownArrow, isDown);
					return;
				case Control.KeyRight:
					SetKey(Key.RightArrow, isDown);
					return;
				case Control.KeyEnter:
					SetKey(Key.Enter, isDown);
					return;
				default:
					throw new ArgumentOutOfRangeException(nameof(control), control, null);
			}

			SetStick(LeftController, isDown ? stick : Vector2.zero);
		}

		private static void SetButton(InputDevice device, string controlName, bool isDown)
		{
			ButtonControl button = (ButtonControl)device[controlName];
			using (StateEvent.From(device, out InputEventPtr eventPtr))
			{
				button.WriteValueIntoEvent(isDown ? 1f : 0f, eventPtr);
				InputSystem.QueueEvent(eventPtr);
			}
		}

		private static void SetStick(InputDevice device, Vector2 value)
		{
			Vector2Control stick = (Vector2Control)device["thumbstick"];
			using (StateEvent.From(device, out InputEventPtr eventPtr))
			{
				stick.WriteValueIntoEvent(value, eventPtr);
				InputSystem.QueueEvent(eventPtr);
			}
		}

		private static void SetKey(Key key, bool isDown)
		{
			InputSystem.QueueStateEvent(CheckKeyboard, isDown ? new KeyboardState(key) : new KeyboardState());
		}

		// Made-up sessions for the check only, with an extra column the menu must keep
		private static string MakeSampleSessions()
		{
			StringBuilder text = new();
			text.Append("Id;Group;Form choice;Date;Time;Answer 1\r\n");
			for (int session = 1; session <= SampleSessions; session++)
			{
				string group = session % 2 == 1 ? "A" : "B";
				string formChoice = FormChoices[(session - 1) % FormChoices.Length];
				int minutes = 9 * 60 + 10 + session * 4;
				text.Append($"{session:000};{group};{formChoice};2026-03-12;"
					+ $"{minutes / 60:00}:{minutes % 60:00};answer {session}\r\n");
			}

			return text.ToString();
		}

		private static List<string[]> ReadCsv(string fileName)
		{
			string path = Path.Combine(TempFolder, fileName);
			return File.Exists(path) ? CsvUtil.Parse(File.ReadAllText(path), ';') : new List<string[]>();
		}

		private static string Describe(AdminMenuScreen screen)
		{
			return screen != null ? screen.name : "none";
		}

		private static string Describe(AdminMenuButton button)
		{
			return button != null ? $"\"{button.Label}\"" : "nothing";
		}

		private static void HandleLog(string message, string stackTrace, LogType type)
		{
			bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
			if (isError && !HeadsetMissingPrefixes.Any(message.StartsWith))
			{
				Errors.Add($"{message}\n{stackTrace}");
			}
			else if (isError || type == LogType.Warning)
			{
				Warnings.Add(message);
			}
		}

		private static void Finish()
		{
			EditorApplication.update -= Tick;
			Application.logMessageReceived -= HandleLog;
			SessionState.SetBool(RunningKey, false);
			InputSystem.RemoveDevice(LeftController);
			InputSystem.RemoveDevice(RightController);
			InputSystem.RemoveDevice(CheckKeyboard);
			InputSystem.RemoveLayout(LayoutName);
			InputSystem.settings.editorInputBehaviorInPlayMode = PreviousInputBehavior;
			InputSystem.settings.backgroundBehavior = PreviousBackgroundBehavior;
			if (Directory.Exists(TempFolder))
			{
				Directory.Delete(TempFolder, true);
			}

			foreach (string failure in Failures)
			{
				Debug.Log($"[Admin] Check failure: {failure}");
			}

			foreach (string error in Errors.Take(10))
			{
				Debug.Log($"[Admin] Check error: {error}");
			}

			foreach (string warning in Warnings.Distinct().Take(10))
			{
				Debug.Log($"[Admin] Check warning: {warning}");
			}

			bool isPassed = Failures.Count == 0 && Errors.Count == 0;
			Debug.Log($"[Admin] Check done: {(isPassed ? "passed" : "failed")}, {Steps.Count} steps, "
				+ $"{Failures.Count} failures, {Errors.Count} errors, {Warnings.Count} warnings.");
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
