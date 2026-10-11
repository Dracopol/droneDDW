using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace DroneDDW.TicketScan
{
	// Reads tickets with Horizon OS's own QR code tracking, through Meta's MR Utility Kit (MRUK). It asks for the
	// spatial data permission, which MRUK waits for before it turns QR tracking on, then reports every tracked QR code
	// ten times a second. The system doesn't share its camera images, so there is no preview and no module size. The
	// distance is approximate: MRUK places codes as if the rig were an OVRCameraRig, so they are compared with the
	// head in the rig's tracking space.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Ticket Scan/Tracker Ticket Scanner")]
	internal sealed class TrackerTicketScanner : TicketScanner
	{
		#region Entities

		private enum PermissionState
		{
			Asking,
			Granted,
			Denied,
		}

		#endregion

		#region Serialized fields

		[Tooltip("MRUK, with QR code tracking on in its Scene Settings > Tracker Configuration")]
		[SerializeField] private MRUK _mruk;

		[Tooltip("The headset camera")]
		[SerializeField] private Transform _head;

		[Tooltip("The head's parent in the XR rig (Camera Offset): MRUK's positions are in this space")]
		[SerializeField] private Transform _trackingSpace;

		#endregion

		#region Fields

		private const string ScenePermission = OVRPermissionsRequester.ScenePermission;
		private const float ReadIntervalSeconds = 0.1f;
		private const float MetersToCentimeters = 100f;

		private readonly List<MRUKTrackable> _trackables = new();
		private readonly StringBuilder _status = new();

		private PermissionState _permission;
		private int _trackedCount;
		private Rect? _lastCodeRect;

		#endregion

		#region Properties

		internal override string Status => BuildStatus();

		#endregion

		#region Setup

		private void Start()
		{
			ReadAsync(destroyCancellationToken).Forget();
		}

		#endregion

		#region Internal

		internal void Configure(MRUK mruk, Transform head, Transform trackingSpace)
		{
			_mruk = mruk;
			_head = head;
			_trackingSpace = trackingSpace;
		}

		#endregion

		#region Private

		private async UniTaskVoid ReadAsync(CancellationToken cancellationToken)
		{
			try
			{
				_permission = PermissionState.Asking;
				bool isGranted = await AndroidPermissionUtil.RequestAsync(ScenePermission, cancellationToken);
				_permission = isGranted ? PermissionState.Granted : PermissionState.Denied;
				if (!isGranted)
				{
					Debug.LogWarning("[Ticket Scan] No spatial data permission, so MRUK won't track QR codes.", this);
					return;
				}

				while (!cancellationToken.IsCancellationRequested)
				{
					ReadTrackedCodes();
					await UniTask.Delay(TimeSpan.FromSeconds(ReadIntervalSeconds), DelayType.Realtime,
						cancellationToken: cancellationToken);
				}
			}
			catch (OperationCanceledException)
			{
				// The scene closed
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
				Debug.LogError("[Ticket Scan] The tracker scanner stopped after the error above.", this);
			}
		}

		private void ReadTrackedCodes()
		{
			_mruk.GetTrackables(_trackables);
			_trackedCount = 0;
			foreach (MRUKTrackable trackable in _trackables)
			{
				if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode || !trackable.IsTracked)
				{
					continue;
				}

				_trackedCount++;
				_lastCodeRect = trackable.PlaneRect;
				string payload = trackable.MarkerPayloadString ?? DecodePayloadBytes(trackable.MarkerPayloadBytes);
				if (payload != null)
				{
					RaiseRead(new TicketRead(payload, EstimateDistanceMeters(trackable.transform.position), float.NaN,
						float.NaN, Time.realtimeSinceStartup));
				}
			}
		}

		// MRUK's world is the rig's tracking space, since it has no OVRCameraRig to move it with
		private float EstimateDistanceMeters(Vector3 codePosition)
		{
			Vector3 headPosition = _trackingSpace != null
				? _trackingSpace.InverseTransformPoint(_head.position)
				: _head.position;
			return Vector3.Distance(headPosition, codePosition);
		}

		// The payload as UTF-8 text, or null when it isn't any
		private static string DecodePayloadBytes(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return null;
			}

			try
			{
				return new UTF8Encoding(false, true).GetString(bytes);
			}
			catch (ArgumentException)
			{
				return null;
			}
		}

		private string BuildStatus()
		{
			switch (_permission)
			{
				case PermissionState.Asking:
					return "Waiting for the spatial data permission...";
				case PermissionState.Denied:
					return "No spatial data permission. Allow it in the headset's Settings > Privacy & safety > App "
						+ "permissions, then restart the scene.";
				case PermissionState.Granted:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(_permission), _permission, null);
			}

			_status.Clear();
			bool isTracking = _mruk.TrackerConfiguration.QRCodeTrackingEnabled;
			if (!_mruk.QRCodeTrackingSupported)
			{
				_status.Append("QR tracking isn't supported here");
			}
			else
			{
				_status.Append(isTracking ? "QR tracking on" : "Turning QR tracking on...");
			}

			_status.Append(" · ").Append(_trackedCount).Append(_trackedCount == 1 ? " code" : " codes")
				.Append(" tracked");
			if (_lastCodeRect.HasValue)
			{
				Vector2 size = _lastCodeRect.Value.size * MetersToCentimeters;
				_status.Append(" · last code ").Append(size.x.ToString("0.0", CultureInfo.InvariantCulture))
					.Append(" x ").Append(size.y.ToString("0.0", CultureInfo.InvariantCulture)).Append(" cm");
			}

			return _status.ToString();
		}

		#endregion
	}
}
