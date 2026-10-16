using DroneDDW.Neighborhood.Table;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDDW.Experience.TestScene
{
	// The Table Tuning scene: moves the Neighborhood's table nearer or further and up or down while sitting at it, in
	// passthrough with the eyes put where the experience puts them, and shows how far everything is on a label. The values
	// are Content/Neighborhood/Data/Neighborhood Table Settings.asset, which the Neighborhood and the experience are built
	// with: over Link changes are kept after Play mode (also when made in its Inspector); in a build, copy the numbers
	// from the label into the asset.
	// Controls: left stick (or the up and down arrows) nearer and further, right stick (or Page Up and Page Down) up and
	// down, a step each push and repeating while held; A / X (or Space) puts the eyes back on the seat.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Test Scene/Table Tuning Controls")]
	internal sealed class TableTuningControls : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private NeighborhoodTablePlacement _table;
		[SerializeField] private XROrigin _origin;

		[Tooltip("How high above the seat the eyes are put, in metres, as the experience does")]
		[Min(0f)]
		[SerializeField] private float _eyeHeightMeters = 1.2f;

		[SerializeField] private Transform _button;
		[SerializeField] private Transform _slider;
		[SerializeField] private Transform _map;
		[SerializeField] private TMP_Text _label;

		[Tooltip("Nearer and further: the left stick, and the up and down arrows")]
		[SerializeField] private InputAction _distanceAction;

		[Tooltip("Up and down: the right stick, and Page Up and Page Down")]
		[SerializeField] private InputAction _heightAction;

		[Tooltip("Puts the eyes back on the seat: A / X, and Space")]
		[SerializeField] private InputAction _resetAction;

		[Tooltip("How far one push moves the table, in metres")]
		[Min(0.001f)]
		[SerializeField] private float _stepMeters = 0.01f;

		[Tooltip("How long a stick is held before the steps repeat, and the time between them")]
		[Min(0.05f)]
		[SerializeField] private float _repeatDelaySeconds = 0.4f;

		[Min(0.02f)]
		[SerializeField] private float _repeatIntervalSeconds = 0.1f;

		[Tooltip("How long after starting the eyes are put on the seat by themselves, once the headset tracks")]
		[Min(0f)]
		[SerializeField] private float _firstResetSeconds = 1f;

		#endregion

		#region Fields

		private const float PressThreshold = 0.6f;
		private const float ReleaseThreshold = 0.3f;
		private const float MinFlatForwardLength = 0.1f;

		private readonly StepRepeat _distanceSteps = new();
		private readonly StepRepeat _heightSteps = new();
		private bool _hasReset;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_distanceAction.Enable();
			_heightAction.Enable();
			_resetAction.Enable();
		}

		private void OnDisable()
		{
			_distanceAction.Disable();
			_heightAction.Disable();
			_resetAction.Disable();
		}

		#endregion

		#region Update

		private void Update()
		{
			if (_resetAction.WasPressedThisFrame() || (!_hasReset && Time.time >= _firstResetSeconds))
			{
				_hasReset = ResetEyes();
			}

			NeighborhoodTableSettings settings = _table.Settings;
			float now = Time.unscaledTime;
			int distanceSteps = _distanceSteps.Read(_distanceAction.ReadValue<Vector2>().y, now, this);
			int heightSteps = _heightSteps.Read(_heightAction.ReadValue<Vector2>().y, now, this);
			if (distanceSteps != 0 || heightSteps != 0)
			{
				settings.EdgeDistanceMeters = Snap(settings.EdgeDistanceMeters + distanceSteps * _stepMeters);
				settings.TopHeightMeters = Snap(settings.TopHeightMeters + heightSteps * _stepMeters);
#if UNITY_EDITOR
				UnityEditor.EditorUtility.SetDirty(settings);
#endif
			}

			// Every frame, so changes made in the Inspector show at once too
			_table.Apply();
			_label.text = Describe(settings);
		}

		#endregion

		#region Internal

		internal void Configure(NeighborhoodTablePlacement table, XROrigin origin, float eyeHeightMeters, Transform button,
			Transform slider, Transform map, TMP_Text label, InputAction distanceAction, InputAction heightAction,
			InputAction resetAction)
		{
			_table = table;
			_origin = origin;
			_eyeHeightMeters = eyeHeightMeters;
			_button = button;
			_slider = slider;
			_map = map;
			_label = label;
			_distanceAction = distanceAction;
			_heightAction = heightAction;
			_resetAction = resetAction;
		}

		#endregion

		#region Private

		// As the experience's Reset player location: the head turned to face the table and put above the seat at the eye
		// height. False while the headset doesn't track yet.
		private bool ResetEyes()
		{
			if (_origin == null || _origin.Camera == null)
			{
				return false;
			}

			Transform head = _origin.Camera.transform;
			Vector3 up = _origin.transform.up;
			Vector3 headForward = Vector3.ProjectOnPlane(head.forward, up);
			if (headForward.magnitude < MinFlatForwardLength)
			{
				headForward = Vector3.ProjectOnPlane(head.up, up);
			}

			_origin.RotateAroundCameraUsingOriginUp(Vector3.SignedAngle(headForward, Vector3.forward, up));
			_origin.MoveCameraToWorldLocation(_table.Seat + Vector3.up * _eyeHeightMeters);
			return true;
		}

		private string Describe(NeighborhoodTableSettings settings)
		{
			float belowEyes = _eyeHeightMeters - settings.TopHeightMeters;
			return $"Near edge {ToCentimeters(settings.EdgeDistanceMeters)} in front of the eyes  (left stick)\n"
				+ $"Top {ToCentimeters(settings.TopHeightMeters)} above the floor, {ToCentimeters(belowEyes)} below the eyes"
				+ "  (right stick)\n"
				+ $"From the eyes: button {ToCentimeters(_table.GetDistanceFromEyes(_button.position))}, "
				+ $"slider {ToCentimeters(_table.GetDistanceFromEyes(_slider.position))}, "
				+ $"map {ToCentimeters(_table.GetDistanceFromEyes(_map.position))}\n"
				+ "A / X: put the eyes back on the seat";
		}

		private static string ToCentimeters(float meters)
		{
			return $"{meters * 100f:0} cm";
		}

		// To the millimetre, so repeated steps don't drift
		private static float Snap(float meters)
		{
			return Mathf.Round(meters * 1000f) / 1000f;
		}

		#endregion

		#region Types

		// A stick axis as steps: one when it is pushed past the press threshold, then repeating while it stays past the
		// release threshold
		private sealed class StepRepeat
		{
			private int _heldSign;
			private float _nextStepSeconds;

			internal int Read(float axis, float nowSeconds, TableTuningControls controls)
			{
				if (_heldSign != 0 && axis * _heldSign >= ReleaseThreshold)
				{
					if (nowSeconds < _nextStepSeconds)
					{
						return 0;
					}

					_nextStepSeconds = nowSeconds + controls._repeatIntervalSeconds;
					return _heldSign;
				}

				_heldSign = 0;
				if (Mathf.Abs(axis) < PressThreshold)
				{
					return 0;
				}

				_heldSign = axis > 0f ? 1 : -1;
				_nextStepSeconds = nowSeconds + controls._repeatDelaySeconds;
				return _heldSign;
			}
		}

		#endregion
	}
}
