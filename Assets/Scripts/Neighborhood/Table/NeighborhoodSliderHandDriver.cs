#if NEIGHBORHOOD_XRI
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
#if NEIGHBORHOOD_XRI_3
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#endif

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// Drives <see cref="NeighborhoodSlider"/> from the hands. By default any part of a hand pushes the handle along the
	/// track, from either side, and a hand on each side holds it in between, so it can be squeezed and carried. When
	/// the fingers close on it (its interactable is selected through the finger grab), the handle follows the grab
	/// instead, keeping its offset, until the fingers open or the pinch leaves the release box (the grab box grown by the
	/// settings' release margin). Pushing and grabbing are one drag: it ends, and the version is committed, only once
	/// nothing has touched or held the handle for a short moment, so a touch that flickers mid-push doesn't commit
	/// early. Every frame it also tells the slider how a hand touches the handle (for its sounds, lights and label).
	/// How the hands meet the handle comes from the shared push settings, read every frame: the spheres' margin and
	/// spacing, the padding of the box they push against, and the grab box, which it keeps the handle's BoxCollider on.
	/// Plays a haptic tick on every detent and a firmer one on commit while grabbing (nothing with hands).
	/// Compiled only when the XR Interaction Toolkit package is installed.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Slider Hand Driver")]
	public sealed class NeighborhoodSliderHandDriver : MonoBehaviour
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

		[SerializeField] private NeighborhoodSlider _slider;

		[SerializeField, Tooltip("The interactable on the handle. Selected when the fingers grab it.")]
		private XRBaseInteractable _interactable;

		[SerializeField, Tooltip("The handle's mesh. The hands push against its bounds, "
			+ "so a push starts on visible contact.")]
		private MeshFilter _handleMesh;

		[SerializeField] private NeighborhoodHandBones _handBones;

		[SerializeField, Tooltip("The shared push settings: sphere margin and spacing, push box padding and grab box. "
			+ "Uses the defaults when empty.")]
		private NeighborhoodPushSettings _settings;

		[Header("Push")]
		[SerializeField, Tooltip("Any part of a hand pushes the handle. Off leaves grabbing only.")]
		private bool _isPushEnabled = true;

		[SerializeField, Tooltip("Closing the fingers on the handle grabs it. Off leaves pushing only.")]
		private bool _isGrabEnabled = true;

		[SerializeField, Min(0f), Tooltip("How long nothing may touch or hold the handle "
			+ "before the version is committed.")]
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

		private readonly List<NeighborhoodHandSphere> _spheres = new List<NeighborhoodHandSphere>();
		private Transform _transform;
		private BoxCollider _handleCollider;
		private HoldMode _mode;
		private float _lastHoldTime;
		private IXRSelectInteractor _dragger;
		private IXRSelectInteractor _lastDragger;

		#endregion

		#region Properties

		/// <summary>Whether the hands move the slider. Off lets go of it; the experience switches it off once the
		/// choice is locked in, and while the session is paused.</summary>
		public bool IsInputEnabled
		{
			get => enabled;
			set => enabled = value;
		}

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
			_slider.OnVersionCommit += HandleVersionCommit;
		}

		private void OnDisable()
		{
			_slider.OnDetentChange -= HandleDetentChange;
			_slider.OnVersionCommit -= HandleVersionCommit;
			_slider.SetContact(NeighborhoodSliderContact.None);
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
			IXRSelectInteractor interactor = _isGrabEnabled && _interactable.isSelected
				? _interactable.firstInteractorSelecting
				: null;
			if (interactor != null && !IsInReleaseBox(GetAttachPoint(interactor)))
			{
				// The pinch left the release box: let go, even with the fingers still closed
				_interactable.interactionManager.SelectExit(interactor, (IXRSelectInteractable)_interactable);
				interactor = null;
			}

			if (interactor != null)
			{
				Vector3 attachPoint = GetAttachPoint(interactor);
				_slider.SetContact(NeighborhoodSliderContact.Grabbed);
				Hold(HoldMode.Grab, attachPoint, attachPoint, interactor);
				return;
			}

			_dragger = null;
			if (_isPushEnabled && TryGetPushTarget(out Vector3 targetPoint))
			{
				// A push has no grab point: anchoring at the handle moves it exactly to where the hands leave room
				_slider.SetContact(NeighborhoodSliderContact.Pushed);
				Hold(HoldMode.Push, _handleMesh.transform.position, targetPoint, null);
				return;
			}

			_slider.SetContact(_interactable.isHovered ? NeighborhoodSliderContact.InReach : NeighborhoodSliderContact.None);
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

		private void HandleVersionCommit(int detent)
		{
			SendPulse(_lastDragger, _commitAmplitude, _commitDuration);
		}

		#endregion

		#region Internal

		internal void Configure(NeighborhoodSlider slider, XRBaseInteractable interactable, MeshFilter handleMesh,
			NeighborhoodHandBones handBones, NeighborhoodPushSettings settings, bool isGrabEnabled)
		{
			_slider = slider;
			_interactable = interactable;
			_handleMesh = handleMesh;
			_handBones = handBones;
			_settings = settings;
			_isGrabEnabled = isGrabEnabled;
		}

		/// <summary>The spheres the push tests right now, for <see cref="NeighborhoodHandSpheresView"/>.</summary>
		internal void CollectSpheres(List<NeighborhoodHandSphere> spheres)
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

			matrix = transform.localToWorldMatrix * Matrix4x4.TRS(boxCenter, Quaternion.identity, halfSize * 2f);
			return true;
		}

		/// <summary>The handle's BoxCollider (the finger grab's reach, and with grabbing off the hover glow's), as a world
		/// matrix of a 1 m cube.</summary>
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

		/// <summary>The release box (the grab box grown by the settings' release margin), as a world matrix of a 1 m
		/// cube.</summary>
		internal bool TryGetReleaseBoxMatrix(out Matrix4x4 matrix)
		{
			matrix = default;
			if (!TryGetReleaseBox(out Vector3 center, out Vector3 size))
			{
				return false;
			}

			matrix = _handleCollider.transform.localToWorldMatrix * Matrix4x4.TRS(center, Quaternion.identity, size);
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
		/// middle of the grip. The same rule as the Push Slider Test. The rig has no rotation between the slider and the
		/// handle, so the slider's local space is in metres and the handle's mesh bounds, times its scale, are aligned
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
			foreach (NeighborhoodHandSphere sphere in _spheres)
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
			float meshCenterX = _handleMesh.sharedMesh.bounds.center.x * _handleMesh.transform.localScale.x;
			targetPoint = _transform.TransformPoint(new Vector3(centerX - meshCenterX, 0f, 0f));
			return true;
		}

		/// <summary>
		/// Samples spheres along every hand bone, sized with the touch margin, and marks the ones touching the push box,
		/// which it hands out in the slider's space. False when the handle has no mesh.
		/// </summary>
		private bool SampleSpheres(List<NeighborhoodHandSphere> spheres, out Vector3 boxCenter, out Vector3 halfSize)
		{
			spheres.Clear();
			if (!TryGetPushBox(out boxCenter, out halfSize))
			{
				return false;
			}

			_handBones.Refresh();
			NeighborhoodPushSettings settings = NeighborhoodPushSettings.OrDefaults(_settings);
			float spacingMeters = Mathf.Max(MinSampleSpacingMeters, settings.SampleSpacingMeters);
			foreach (NeighborhoodHandBone bone in _handBones.Bones)
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
					spheres.Add(new NeighborhoodHandSphere(worldPoint, point, radiusMeters, isTouching));
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
			Vector3 scale = _handleMesh.transform.localScale;
			float paddingMeters = NeighborhoodPushSettings.OrDefaults(_settings).PushBoxPaddingMeters;
			boxCenter = _handleMesh.transform.localPosition + Vector3.Scale(bounds.center, scale);
			halfSize = Vector3.Max(Vector3.Scale(bounds.extents, scale) + Vector3.one * paddingMeters, Vector3.zero);
			return true;
		}

		/// <summary>Keeps the handle's BoxCollider on the settings' grab box, so tuning it applies at once.</summary>
		private void ApplyGrabBox()
		{
			if (_handleCollider == null)
			{
				return;
			}

			NeighborhoodPushSettings settings = NeighborhoodPushSettings.OrDefaults(_settings);
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

		/// <summary>
		/// The release box in the handle collider's space: the grab box grown on every side by the release margin, which
		/// is in metres, so it is divided by the handle's scale. False without a collider.
		/// </summary>
		private bool TryGetReleaseBox(out Vector3 center, out Vector3 size)
		{
			center = default;
			size = default;
			if (_handleCollider == null)
			{
				return false;
			}

			float marginMeters = NeighborhoodPushSettings.OrDefaults(_settings).ReleaseMarginMeters;
			Vector3 scale = _handleCollider.transform.lossyScale;
			Vector3 margin = new Vector3(LocalLength(marginMeters, scale.x), LocalLength(marginMeters, scale.y),
				LocalLength(marginMeters, scale.z));
			center = _handleCollider.center;
			size = _handleCollider.size + margin * 2f;
			return true;
		}

		/// <summary>Whether a world point is inside the release box. True without a collider, so the grab never
		/// drops for lack of one.</summary>
		private bool IsInReleaseBox(Vector3 worldPoint)
		{
			if (!TryGetReleaseBox(out Vector3 center, out Vector3 size))
			{
				return true;
			}

			Vector3 offset = _handleCollider.transform.InverseTransformPoint(worldPoint) - center;
			Vector3 halfSize = size * 0.5f;
			return Mathf.Abs(offset.x) <= halfSize.x
				&& Mathf.Abs(offset.y) <= halfSize.y
				&& Mathf.Abs(offset.z) <= halfSize.z;
		}

		private static float LocalLength(float meters, float scale)
		{
			return Mathf.Approximately(scale, 0f) ? 0f : meters / Mathf.Abs(scale);
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

#if NEIGHBORHOOD_XRI_3
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
