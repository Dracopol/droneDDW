using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using Debug = UnityEngine.Debug;

namespace DroneDDW.Admin.Editor
{
	// Menu: DroneDDW > Admin Menu > Pull Session Files From Headset. With the Quest plugged in by USB (and this computer
	// allowed USB debugging), copies the app's session files, Sessions.csv and Deleted Sessions.csv, to
	// Documents/DroneDDW Sessions/<date time>/ and opens the folder. Every pull gets its own folder, so nothing is
	// overwritten. Uses the adb that comes with Unity's Android support, or adb on the PATH.
	internal static class AdminMenuHeadsetFiles
	{
		#region Fields

		private const string MenuPath = "DroneDDW/Admin Menu/Pull Session Files From Headset";
		private const string DialogTitle = "Pull session files";
		private const string BundledAdbPath = "PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb";
		private const string LocalFolderName = "DroneDDW Sessions";
		private const string DefaultSessionsFolder = "Sessions";
		private const int TimeoutMilliseconds = 30000;

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Pull()
		{
			string adb = FindAdb();
			if (!TryRun(adb, "devices", out string devices))
			{
				Show($"Couldn't run adb ({adb}). Is Android support installed for this Unity version?\n\n{devices}");
				return;
			}

			if (!TryFindHeadset(devices, out string serial, out string state))
			{
				Show("No headset found. Plug the Quest in by USB, put it on, and allow USB debugging if it asks.");
				return;
			}

			if (state != "device")
			{
				Show(state == "unauthorized"
					? "The headset hasn't allowed this computer yet: put it on, accept \"Allow USB debugging\", then "
					+ "try again."
					: $"The headset is {state}. Unplug it, plug it in again and try again.");
				return;
			}

			string package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
			AdminMenuSettings settings =
				AssetDatabase.LoadAssetAtPath<AdminMenuSettings>(AdminMenuAssetBuilder.SettingsPath);
			string sessionsFolder = settings != null ? settings.SessionsFolder : DefaultSessionsFolder;
			string remote = $"/sdcard/Android/data/{package}/files/{sessionsFolder}";
			string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
				LocalFolderName, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
			Directory.CreateDirectory(local);

			if (!TryRun(adb, $"-s {serial} pull \"{remote}\" \"{local}\"", out string output))
			{
				Directory.Delete(local);
				Show(output.Contains("does not exist")
					? $"There are no session files on the headset yet ({remote}): this app hasn't saved anything."
					: $"adb couldn't copy {remote}:\n\n{output}");
				return;
			}

			string[] files = Directory.GetFiles(local, "*", SearchOption.AllDirectories);
			string names = string.Join("\n", files.Select(Path.GetFileName));
			EditorUtility.RevealInFinder(files.Length > 0 ? files[0] : local);
			Show($"Copied {files.Length} file(s) from the headset ({remote}) to\n{local}\n\n{names}");
		}

		// Unity's own adb when Android support is installed, otherwise whatever adb is on the PATH
		private static string FindAdb()
		{
			string bundled = Path.Combine(EditorApplication.applicationContentsPath, BundledAdbPath);
			if (File.Exists(bundled + ".exe"))
			{
				return bundled + ".exe";
			}

			return File.Exists(bundled) ? bundled : "adb";
		}

		// The first device in `adb devices`, preferring one that is ready
		private static bool TryFindHeadset(string devices, out string serial, out string state)
		{
			serial = null;
			state = null;
			foreach (string line in devices.Split('\n'))
			{
				string[] parts = line.Trim().Split('\t');
				if (parts.Length != 2 || line.StartsWith("*"))
				{
					continue;
				}

				if (serial == null || parts[1] == "device")
				{
					serial = parts[0];
					state = parts[1];
				}
			}

			return serial != null;
		}

		// Runs adb and returns its output; false when it couldn't start, timed out or failed
		private static bool TryRun(string adb, string arguments, out string output)
		{
			ProcessStartInfo startInfo = new(adb, arguments)
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};

			try
			{
				using Process process = Process.Start(startInfo);
				if (process == null)
				{
					output = "adb didn't start";
					return false;
				}

				// Read while it runs, so a stuck adb times out instead of freezing the editor
				Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
				Task<string> standardError = process.StandardError.ReadToEndAsync();
				if (!process.WaitForExit(TimeoutMilliseconds))
				{
					process.Kill();
					output = "adb took too long and was stopped";
					return false;
				}

				output = standardOutput.Result + standardError.Result;
				return process.ExitCode == 0;
			}
			catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
				|| exception is InvalidOperationException)
			{
				output = exception.Message;
				return false;
			}
		}

		private static void Show(string message)
		{
			Debug.Log($"[Admin] Pull session files: {message}");
			EditorUtility.DisplayDialog(DialogTitle, message, "OK");
		}

		#endregion
	}
}
