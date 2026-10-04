using TMPro;
using UnityEngine;

namespace DroneDDW.Admin.TestScene
{
	// Stands in for a running session in the admin menu test scene, so pausing, resuming and resetting can be seen:
	// a session clock and a spinning cube on scaled time (they stop while paused) and a quiet looping hum (paused with
	// all audio). Reset session reloads the scene, so the clock starts over.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Admin Menu Demo Session")]
	internal sealed class AdminMenuDemoSession : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private TMP_Text _clock;
		[SerializeField] private Transform _spinner;

		[Tooltip("Degrees per second")]
		[SerializeField] private float _spinSpeed = 45f;

		#endregion

		#region Fields

		private const string Instructions = "Left controller menu button: admin menu (Esc in the Editor)";

		private int _shownSeconds = -1;

		#endregion

		#region Properties

		// Session time so far, in scaled seconds
		internal float ElapsedSeconds { get; private set; }

		#endregion

		#region Update

		private void Update()
		{
			ElapsedSeconds += Time.deltaTime;
			_spinner.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.World);

			int seconds = Mathf.FloorToInt(ElapsedSeconds);
			if (seconds == _shownSeconds)
			{
				return;
			}

			_shownSeconds = seconds;
			_clock.text = $"Session running  {seconds / 60}:{seconds % 60:00}\n<size=60%>{Instructions}</size>";
		}

		#endregion

		#region Internal

		internal void Configure(TMP_Text clock, Transform spinner)
		{
			_clock = clock;
			_spinner = spinner;
		}

		#endregion
	}
}
