#if PARKS_XRI
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
#if PARKS_XRI_3
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#endif

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// Drives <see cref="ParkLevelSlider"/> from the hands. By default any part of a hand pushes the handle along the
	/// track, from either side, and a hand on each side holds it in between, so it can be squeezed and carried. When
	/// the fingers close on it (its interactable is selected through the finger grab), the handle follows the grab
	/// instead, keeping its offset. Pushing and grabbing are one drag: it ends, and the level is committed, only once
	/// nothing has touched or held the handle for a short moment, so a touch that flickers mid-push doesn't commit
	/// early. Every frame it also tells the slider how a hand touches the handle (for its sounds, lights and label).
	/// How the hands meet the handle comes from the shared push settings, read every frame: the spheres' margin and
	/// spacing, the padding of the box they push against, and the grab box, which it keeps the handle's BoxCollider on.
	/// Plays a haptic tick on every detent and a firmer one on commit while grabbing (nothing with hands).
	/// Compiled only when the XR Interaction Toolkit package is installed.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Slider Hand Driver")]
	public sealed class ParkSliderHandDriver : MonoBehaviour
	{
		#region Entities

		private enum HoldMode
		{
			None,
			Push,
			Grab,
		}

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLevelSlider _slider;

		[SerializeField, Tooltip("The interactable on the handle. Selected when the fingers grab it.")]
		private XRBaseInteractable _interactable;

		[SerializeField, Tooltip("The handle's mesh. The hands push against its bounds, "
			+ "so a push starts on visible contact.")]
		private MeshFilter _handleMesh;

		[SerializeField] private ParkHandBones _handBones;

		[SerializeField, Tooltip("The shared push settings: sphere margin and spacing, push box padding and grab box. "
			+ "Uses the defaults when empty.")]
		private ParkPushSettings _settings;

		[Header("Push")]
		[SerializeField, Tooltip("Any part of a hand pushes the handle. Off leaves grabbing only.")]
		private bool _isPushEnabled = true;

		[SerializeField, Min(0f), Tooltip("How long nothing may touch or hold the handle "
			+ "before the level is committed.")]
		private float _releaseDelaySeconds = 0.3f;

		[Header("Haptics")]
		[SerializeField, Range(0f, 1f)] private float _detentAmplitude = 0.3f;
		[SerializeField, Min(0f)] private float _detentDuration = 0.03f;
		[SerializeField, Range(0f, 1f)] private float _commitAmplitude = 0.6f;
		[SerializeField, Min(0f)] private float _commitDuration = 0.08f;

		#endregion

		#region Fields

		// Keeps the sphere count sane whatever the settings say
		private const float MinSampleSpacingMeters = 0.002f;

		private readonly List<ParkHandSphere> _spheres = new List<ParkHandSphere>();
		private Transform _transform;
		private BoxCollider _handleCollider;
		private HoldMode _mode;
		private float _lastHoldTime;
		private IXRSelectInteractor _dragger;
		private IXRSelectInteractor _lastDragger;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
			if (_handleMesh != null)
			{
				_handleMesh.TryGetComponent(out _handleCollider);
			}
		}

		private void OnEnable()
		{
			_slider.OnDetentChange += HandleDetentChange;
			_slider.OnLevelCommit += HandleLevelCommit;
		}

		private void OnDisable()
		{
			_slider.OnDetentChange -= HandleDetentChange;
			_slider.OnLevelCommit -= HandleLevelCommit;
			_slider.SetContact(ParkSliderContact.None);
			if (_mode == HoldMode.None)
			{
				return;
			}

			_mode = HoldMode.None;
			_dragger = null;
			_slider.EndDrag();
		}

		#endregion

		#region Update

		private void Update()
		{
			ApplyGrabBox();

			// The contact is reported before dragging: the slider's soft detents only apply to a grab
			IXRSelectInteractor interactor = _interactable.isSelected ? _interactable.firstInteractorSelecting : null;
			if (interactor != null)
			{
				Vector3 attachPoint = GetAttachPoint(interactor);
				_slider.SetContact(ParkSliderContact.Grabbed);
				Hold(HoldMode.Grab, attachPoint, attachPoint, interactor);
				return;
			}

			_dragger = null;
			if (_isPushEnabled && TryGetPushTarget(out Vector3 targetPoint))
			{
				// A push has no grab point: anchoring at the handle moves it exactly to where the hands leave room
				_slider.SetContact(ParkSliderContact.Pushed);
				Hold(HoldMode.Push, _handleMesh.transform.position, targetPoint, null);
				return;
			}

			_slider.SetContact(_interactable.isHovered ? ParkSliderContact.InReach : ParkSliderContact.None);
			if (_mode != HoldMode.None && Time.time - _lastHoldTime >= _releaseDelaySeconds)
			{
				_mode = HoldMode.None;
				_slider.EndDrag();
			}
		}

		#endregion

		#region Event handlers

		private void HandleDetentChange(int detent)
		{
			SendPulse(_dragger, _detentAmplitude, _detentDuration);
		}

		private void HandleLevelCommit(int level)
		{
			SendPulse(_lastDragger, _commitAmplitude, _commitDuration);
		}

		#endregion

		#region Internal

		internal void Configure(ParkLevelSlider slider, XRBaseInteractable interactable, MeshFilter handleMesh,
			ParkHandBones handBones, ParkPushSettings settings)
		{
			_slider = slider;
			_interactable = interactable;
			_handleMesh = handleMesh;
			_handBones = handBones;
			_settings = settings;
		}

		/// <summary>The spheres the push tests right now, for <see cref="ParkHandSpheresView"/>.</summary>
		internal void CollectSpheres(List<ParkHandSphere> spheres)
		{
			SampleSpheres(spheres, out _, out _);
		}

		/// <summary>The box the spheres push against, as a world matrix of a 1 m cube, for drawing it.</summary>
		internal bool TryGetPushBoxMatrix(out Matrix4x4 matrix)
		{
			matrix = default;
			if (!TryGetPushBox(out Vector3 boxCenter, out Vector3 halfSize))
			{
				return false;
			}

			matrix = _transform.localToWorldMatrix * Matrix4x4.TRS(boxCenter, Quaternion.identity, halfSize * 2f);
			return true;
		}

		/// <summary>The handle's BoxCollider, which the finger grab reaches for, as a world matrix of a 1 m cube.</summary>
		internal bool TryGetGrabBoxMatrix(out Matrix4x4 matrix)
		{
			matrix = default;
			if (_handleCollider == null)
			{
				return false;
			}

			matrix = _handleCollider.transform.localToWorldMatrix
				* Matrix4x4.TRS(_handleCollider.center, Quaternion.identity, _handleCollider.size);
			return true;
		}

		#endregion

		#region Private

		/// <summary>
		/// Starts the drag, or re-anchors it when switching between pushing and grabbing or between grabbing hands, so
		/// the handle never jumps; then moves the handle towards the target.
		/// </summary>
		private void Hold(HoldMode mode, Vector3 anchorPoint, Vector3 targetPoint, IXRSelectInteractor interactor)
		{
			_lastHoldTime = Time.time;
			if (mode != _mode || interactor != _dragger)
			{
				_slider.BeginDrag(anchorPoint);
				_mode = mode;
			}

			_dragger = interactor;
			if (interactor != null)
			{
				_lastDragger = interactor;
			}

			_slider.DragTo(targetPoint);
		}

		/// <summary>
		/// Where the hands push the handle to, if any part of a hand touches it: the handle stays clear of every
		/// touching sphere along the hand bones, and squeezed from both sides tighter than it is wide, it sits in the
		/// middle of the grip. The same rule as the Push Slider Test. The rig has no scale or rotation between the
		/// slider and the handle, so the slider's local space is in metres and the handle's mesh bounds are aligned
		/// with it.
		/// </summary>
		private bool TryGetPushTarget(out Vector3 targetPoint)
		{
			targetPoint = default;
			if (!SampleSpheres(_spheres, out Vector3 boxCenter, out Vector3 halfSize))
			{
				return false;
			}

			float lowestCenterX = float.NegativeInfinity;
			float highestCenterX = float.PositiveInfinity;
			bool isTouched = false;
			foreach (ParkHandSphere sphere in _spheres)
			{
				if (!sphere.IsTouching)
				{
					continue;
				}

				isTouched = true;
				float x = sphere.SliderPosition.x;
				float clearanceMeters = halfSize.x + sphere.RadiusMeters;
				if (x < boxCenter.x)
				{
					lowestCenterX = Mathf.Max(lowestCenterX, x + clearanceMeters);
				}
				else
				{
					highestCenterX = Mathf.Min(highestCenterX, x - clearanceMeters);
				}
			}

			if (!isTouched)
			{
				return false;
			}

			float centerX = lowestCenterX > highestCenterX
				? (lowestCenterX + highestCenterX) * 0.5f
				: Mathf.Clamp(boxCenter.x, lowestCenterX, highestCenterX);
			targetPoint = _transform.TransformPoint(new Vector3(centerX - _handleMesh.sharedMesh.bounds.center.x, 0f, 0f));
			return true;
		}

		/// <summary>
		/// Samples spheres along every hand bone, sized with the touch margin, and marks the ones touching the push box,
		/// which it hands out in the slider's space. False when the handle has no mesh.
		/// </summary>
		private bool SampleSpheres(List<ParkHandSphere> spheres, out Vector3 boxCenter, out Vector3 halfSize)
		{
			spheres.Clear();
			if (!TryGetPushBox(out boxCenter, out halfSize))
			{
				return false;
			}

			_handBones.Refresh();
			ParkPushSettings settings = ParkPushSettings.OrDefaults(_settings);
			float spacingMeters = Mathf.Max(MinSampleSpacingMeters, settings.SampleSpacingMeters);
			foreach (ParkHandBone bone in _handBones.Bones)
			{
				float radiusMeters = bone.RadiusMeters + settings.TouchMarginMeters;
				float lengthMeters = Vector3.Distance(bone.Start, bone.End);
				int segmentCount = Mathf.Max(1, Mathf.CeilToInt(lengthMeters / spacingMeters));
				for (int sample = 0; sample <= segmentCount; sample++)
				{
					Vector3 worldPoint = Vector3.Lerp(bone.Start, bone.End, (float)sample / segmentCount);
					Vector3 point = _transform.InverseTransformPoint(worldPoint);
					bool isTouching = Mathf.Abs(point.y - boxCenter.y) <= halfSize.y + radiusMeters
						&& Mathf.Abs(point.z - boxCenter.z) <= halfSize.z + radiusMeters
						&& Mathf.Abs(point.x - boxCenter.x) < halfSize.x + radiusMeters;
					spheres.Add(new ParkHandSphere(worldPoint, point, radiusMeters, isTouching));
				}
			}

			return true;
		}

		/// <summary>
		/// The box the spheres push against, in the slider's space: the handle's visible mesh box, grown on every side by
		/// the settings' padding (shrunk when negative). False when the handle has no mesh.
		/// </summary>
		private bool TryGetPushBox(out Vector3 boxCenter, out Vector3 halfSize)
		{
			boxCenter = default;
			halfSize = default;
			if (_handleMesh == null || _handleMesh.sharedMesh == null)
			{
				return false;
			}

			Bounds bounds = _handleMesh.sharedMesh.bounds;
			float paddingMeters = ParkPushSettings.OrDefaults(_settings).PushBoxPaddingMeters;
			boxCenter = _handleMesh.transform.localPosition + bounds.center;
			halfSize = Vector3.Max(bounds.extents + Vector3.one * paddingMeters, Vector3.zero);
			return true;
		}

		/// <summary>Keeps the handle's BoxCollider on the settings' grab box, so tuning it applies at once.</summary>
		private void ApplyGrabBox()
		{
			if (_handleCollider == null)
			{
				return;
			}

			ParkPushSettings settings = ParkPushSettings.OrDefaults(_settings);
			Vector3 size = Vector3.Max(settings.GrabBoxSize, Vector3.zero);
			if (_handleCollider.center != settings.GrabBoxCenter)
			{
				_handleCollider.center = settings.GrabBoxCenter;
			}

			if (_handleCollider.size != size)
			{
				_handleCollider.size = size;
			}
		}

		private Vector3 GetAttachPoint(IXRSelectInteractor interactor)
		{
			Transform attach = interactor.GetAttachTransform(_interactable);
			return attach != null ? attach.position : interactor.transform.position;
		}

		private static void SendPulse(IXRSelectInteractor interactor, float amplitude, float duration)
		{
			if (amplitude <= 0f || duration <= 0f)
			{
				return;
			}

#if PARKS_XRI_3
			if (interactor is XRBaseInputInteractor inputInteractor)
			{
				inputInteractor.SendHapticImpulse(amplitude, duration);
			}
#else
			if (interactor is XRBaseControllerInteractor controllerInteractor)
			{
				controllerInteractor.SendHapticImpulse(amplitude, duration);
			}
#endif
		}

		#endregion
	}
}
#endif
