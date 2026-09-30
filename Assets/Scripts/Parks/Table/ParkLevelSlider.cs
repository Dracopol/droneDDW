using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Parks.Levels;
using DroneDDW.Parks.Utilities;
using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// The five-detent slider on the table. Put it on the handle's parent (Rig_Slider): the handle moves
	/// along this object's local X axis between the two ends. While dragging it reports the detent the handle
	/// is on (for the live model preview); on release it snaps to that detent and commits the level.
	/// Input comes from outside (<see cref="ParkSliderHandDriver"/>, or anything else that calls the drag methods),
	/// and reports how a hand touches the handle (<see cref="Contact"/>) for the slider's sounds, lights and label.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Level Slider")]
	public sealed class ParkLevelSlider : MonoBehaviour
	{
		#region Events

		/// <summary>The handle passed into another detent while being dragged.</summary>
		public event Action<int> OnDetentChange;

		/// <summary>The handle was released on a detent.</summary>
		public event Action<int> OnLevelCommit;

		public event Action<ParkSliderContact> OnContactChange;

		#endregion

		#region Serialized fields

		[SerializeField, Tooltip("The part that slides (Rig_Handle), a child of this object.")]
		private Transform _handle;

		[SerializeField, Tooltip("Local X of detent 0 (full nature).")]
		private float _minLocalX = -0.3f;

		[SerializeField, Tooltip("Local X of the last detent (full efficiency).")]
		private float _maxLocalX = 0.3f;

		[SerializeField, Min(0.01f), Tooltip("Seconds the handle takes to settle on a detent after release.")]
		private float _snapDuration = 0.18f;

		[Header("Detents")]
		[SerializeField, Range(0f, 0.5f), Tooltip("How far past the halfway point between two detents the handle "
			+ "has to go before the next one counts, as a fraction of a step, so it doesn't flicker when resting "
			+ "in between.")]
		private float _detentHysteresis = 0.1f;

		[SerializeField, Min(0f), Tooltip("While grabbed, how far the hand goes past a detent before the handle "
			+ "follows, so each detent catches it for a moment. 0 = off.")]
		private float _softDetentMeters = 0.012f;

		#endregion

		#region Fields

		private CancellationTokenSource _lifetime;
		private bool _isDragging;
		private float _grabOffset;
		private int _detent = -1;
		private int _snapId;
		private ParkSliderContact _contact;

		#endregion

		#region Properties

		public bool IsDragging => _isDragging;

		/// <summary>The detent the handle is on, or was last on while dragging.</summary>
		public int Detent => _detent;
		public int DetentCount => ParkLevels.Count;
		public ParkSliderContact Contact => _contact;
		public Transform Handle => _handle;

		/// <summary>Where the handle is along the track: 0 at full nature, 1 at full efficiency.</summary>
		public float HandleBlend => Mathf.InverseLerp(_minLocalX, _maxLocalX, HandleX);

		private float HandleX => _handle.localPosition.x;
		private float StepMeters => Mathf.Abs(_maxLocalX - _minLocalX) / (ParkLevels.Count - 1);

		private CancellationToken LifetimeToken
		{
			get
			{
				if (_lifetime == null)
				{
					_lifetime = new CancellationTokenSource();
				}

				return _lifetime.Token;
			}
		}

		#endregion

		#region Setup

		private void OnDestroy()
		{
			if (_lifetime == null)
			{
				return;
			}

			_lifetime.Cancel();
			_lifetime.Dispose();
			_lifetime = null;
		}

		#endregion

		#region Public

		/// <summary>Starts a drag. The grab point keeps its offset from the handle, so the handle never jumps.</summary>
		public void BeginDrag(Vector3 worldGrabPoint)
		{
			_snapId++;
			_isDragging = true;
			_grabOffset = HandleX - ToLocalX(worldGrabPoint);
			if (_detent < 0)
			{
				_detent = XToDetent(HandleX);
			}
		}

		public void DragTo(Vector3 worldPoint)
		{
			if (!_isDragging)
			{
				return;
			}

			float x = Mathf.Clamp(ToLocalX(worldPoint) + _grabOffset, Mathf.Min(_minLocalX, _maxLocalX), Mathf.Max(_minLocalX, _maxLocalX));
			if (_contact == ParkSliderContact.Grabbed)
			{
				// Only a grab: a pushed handle that lagged behind would let the fingers sink into it
				x = ApplySoftDetents(x);
			}

			SetHandleX(x);
			int detent = XToDetent(x);
			if (detent == _detent || !HasLeftDetent(x))
			{
				return;
			}

			_detent = detent;
			OnDetentChange?.Invoke(detent);
		}

		/// <summary>
		/// Releases the handle: it settles on its detent (the one shown while dragging), and that level is committed.
		/// </summary>
		public void EndDrag()
		{
			if (!_isDragging)
			{
				return;
			}

			_isDragging = false;
			SnapToAsync(DetentToX(_detent), ++_snapId, LifetimeToken).Forget();
			OnLevelCommit?.Invoke(_detent);
		}

		/// <summary>Moves the handle to a detent without raising events (to follow level changes made elsewhere).</summary>
		public void SetDetent(int detent, bool isAnimated)
		{
			if (_isDragging)
			{
				return;
			}

			_detent = ParkLevels.Clamp(detent);
			int snapId = ++_snapId;
			if (isAnimated && isActiveAndEnabled)
			{
				SnapToAsync(DetentToX(_detent), snapId, LifetimeToken).Forget();
				return;
			}

			SetHandleX(DetentToX(_detent));
		}

		/// <summary>Called by the input every frame with how a hand is touching the handle.</summary>
		public void SetContact(ParkSliderContact contact)
		{
			if (contact == _contact)
			{
				return;
			}

			_contact = contact;
			OnContactChange?.Invoke(contact);
		}

		#endregion

		#region Internal

		internal void Configure(Transform handle, float minLocalX, float maxLocalX)
		{
			_handle = handle;
			_minLocalX = minLocalX;
			_maxLocalX = maxLocalX;
		}

		#endregion

		#region Private

		private float ToLocalX(Vector3 worldPoint)
		{
			return transform.InverseTransformPoint(worldPoint).x;
		}

		private float DetentToX(int detent)
		{
			return Mathf.Lerp(_minLocalX, _maxLocalX, detent / (float)(ParkLevels.Count - 1));
		}

		private int XToDetent(float x)
		{
			return ParkLevels.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(_minLocalX, _maxLocalX, x) * (ParkLevels.Count - 1)));
		}

		/// <summary>Whether the handle is past halfway to the next detent, plus the hysteresis.</summary>
		private bool HasLeftDetent(float x)
		{
			return _detent < 0 || Mathf.Abs(x - DetentToX(_detent)) >= StepMeters * (0.5f + _detentHysteresis);
		}

		/// <summary>
		/// Holds the handle on the nearest detent until the hand is the soft detent distance past it, then lets it
		/// catch up, reaching the hand again halfway between two detents, so the handle never jumps.
		/// </summary>
		private float ApplySoftDetents(float x)
		{
			float halfStepMeters = StepMeters * 0.5f;
			if (_softDetentMeters <= 0f || _softDetentMeters >= halfStepMeters)
			{
				return x;
			}

			float detentX = DetentToX(XToDetent(x));
			float offsetMeters = x - detentX;
			float stretchedMeters = Mathf.Max(0f, Mathf.Abs(offsetMeters) - _softDetentMeters)
				* halfStepMeters / (halfStepMeters - _softDetentMeters);
			return detentX + Mathf.Sign(offsetMeters) * stretchedMeters;
		}

		private void SetHandleX(float x)
		{
			Vector3 position = _handle.localPosition;
			position.x = x;
			_handle.localPosition = position;
		}

		private async UniTask SnapToAsync(float targetX, int snapId, CancellationToken cancellationToken)
		{
			try
			{
				float startX = HandleX;
				float elapsed = 0f;
				while (elapsed < _snapDuration)
				{
					await UniTask.NextFrame(cancellationToken);
					if (snapId != _snapId)
					{
						return;
					}

					elapsed += Time.deltaTime;
					SetHandleX(Mathf.LerpUnclamped(startX, targetX, ParkEasing.OutCubic(Mathf.Clamp01(elapsed / _snapDuration))));
				}
			}
			catch (OperationCanceledException)
			{
				// Destroyed while settling.
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
			}
		}

		#endregion
	}
}
