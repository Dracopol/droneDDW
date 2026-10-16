using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR;

namespace DroneDDW.Experience
{
	// Diagnostics, to be removed once the headset off/on memory growth is understood: logs every render texture the app
	// holds (grouped by name, with size, MSAA and an estimate of their memory) a few seconds after start and after every
	// return from a pause (the headset coming back on restarts the XR session, which makes new eye textures). Read it on
	// a computer with: adb logcat -s Unity | findstr "XR Textures". Starts itself, so no scene has to carry it.
	internal sealed class XRTextureLog : MonoBehaviour
	{
		#region Fields

		private const float DelaySeconds = 5f;
		private const int MegaByte = 1024 * 1024;

		// The biggest groups only: the headset's log cuts a message off at about 4 KB
		private const int ListedGroups = 15;

		private int _resumeCount;
		private float _logAt = DelaySeconds;

		#endregion

		#region Setup

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Create()
		{
			GameObject logObject = new("XR Texture Log") { hideFlags = HideFlags.HideInHierarchy };
			DontDestroyOnLoad(logObject);
			logObject.AddComponent<XRTextureLog>();
		}

		private void OnApplicationPause(bool isPaused)
		{
			if (!isPaused)
			{
				_resumeCount++;
				_logAt = Time.realtimeSinceStartup + DelaySeconds;
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			if (_logAt > 0f && Time.realtimeSinceStartup >= _logAt)
			{
				_logAt = 0f;
				Log(_resumeCount == 0 ? "after start" : $"after resume {_resumeCount}");
			}
		}

		#endregion

		#region Private

		private static void Log(string moment)
		{
			RenderTexture[] textures = Resources.FindObjectsOfTypeAll<RenderTexture>();
			long total = textures.Sum(EstimateBytes);
			StringBuilder groups = new();
			foreach (IGrouping<string, RenderTexture> group in textures
				.GroupBy(texture => $"{texture.name} {texture.width}x{texture.height}x{texture.volumeDepth} MSAA {texture.antiAliasing}")
				.OrderByDescending(group => group.Sum(EstimateBytes))
				.Take(ListedGroups))
			{
				groups.Append($"\n  {group.Count()} x {group.Key}: {group.Sum(EstimateBytes) / MegaByte} MB");
			}

			Debug.Log($"[XR Textures] {moment}: {textures.Length} render textures, about {total / MegaByte} MB; eye "
				+ $"{XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}, resolution scale "
				+ $"{XRSettings.eyeTextureResolutionScale:0.00}, viewport scale {XRSettings.renderViewportScale:0.00}{groups}");
		}

		// Colour plus depth, times the MSAA samples; an estimate, as the driver may add padding or a resolve copy
		private static long EstimateBytes(RenderTexture texture)
		{
			long pixels = (long)texture.width * texture.height * Mathf.Max(1, texture.volumeDepth)
				* Mathf.Max(1, texture.antiAliasing);
			long colour = texture.graphicsFormat == GraphicsFormat.None ? 0 : GraphicsFormatUtility.GetBlockSize(texture.graphicsFormat);
			long depth = texture.depthStencilFormat == GraphicsFormat.None ? 0 : GraphicsFormatUtility.GetBlockSize(texture.depthStencilFormat);
			return pixels * (colour + depth);
		}

		#endregion
	}
}
