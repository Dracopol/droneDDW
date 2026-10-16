using System.Linq;
using UnityEngine;

namespace DroneDDW.Experience
{
	// Works around a Unity OpenXR leak on the Quest: every time the headset comes back on, the XR session restarts and
	// makes a new set of eye textures ("XR Texture [n]", 3 per session, n counting up), but the old sets are never
	// released, so each headset off/on keeps hundreds of MB of graphics memory until the system kills the app. A moment
	// after each resume, this releases every XR Texture but the newest set. Starts itself, so no scene has to carry it.
	internal sealed class XRTextureCleanup : MonoBehaviour
	{
		#region Fields

		private const string XRTexturePrefix = "XR Texture [";

		// The eye textures of one session (the runtime triple-buffers)
		private const int KeptCount = 3;

		// After the restarted session has made its new set, before XRTextureLog reports (5 s)
		private const float DelaySeconds = 2f;

		private float _cleanAt;

		#endregion

		#region Setup

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Create()
		{
			GameObject cleanupObject = new("XR Texture Cleanup") { hideFlags = HideFlags.HideInHierarchy };
			DontDestroyOnLoad(cleanupObject);
			cleanupObject.AddComponent<XRTextureCleanup>();
		}

		private void OnApplicationPause(bool isPaused)
		{
			if (!isPaused)
			{
				_cleanAt = Time.realtimeSinceStartup + DelaySeconds;
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			if (_cleanAt > 0f && Time.realtimeSinceStartup >= _cleanAt)
			{
				_cleanAt = 0f;
				ReleaseOldSets();
			}
		}

		#endregion

		#region Private

		private static void ReleaseOldSets()
		{
			RenderTexture[] stale = Resources.FindObjectsOfTypeAll<RenderTexture>()
				.Where(texture => texture.name.StartsWith(XRTexturePrefix))
				.OrderByDescending(ReadNumber)
				.Skip(KeptCount)
				.ToArray();
			foreach (RenderTexture texture in stale)
			{
				texture.Release();
				Destroy(texture);
			}

			if (stale.Length > 0)
			{
				Debug.Log($"[XR Textures] Released {stale.Length} eye textures of earlier sessions.");
			}
		}

		// n in "XR Texture [n]"; -1 when it isn't a number
		private static int ReadNumber(RenderTexture texture)
		{
			string name = texture.name;
			int end = name.IndexOf(']', XRTexturePrefix.Length);
			return end > 0 && int.TryParse(name.Substring(XRTexturePrefix.Length, end - XRTexturePrefix.Length), out int number)
				? number
				: -1;
		}

		#endregion
	}
}
