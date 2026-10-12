using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Meta.XR;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneDDW.TicketScan
{
	// Reads tickets from the headset's own camera images (Meta's Passthrough Camera Access) with ZXing. It asks for the
	// camera permission, then takes each new camera image back from the GPU and decodes it on a worker thread, one
	// image at a time. Every code found is reported with how far away it was, worked out from the angle between its
	// finder squares and the printed size of a module (0.743 mm on the tickets).
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Ticket Scan/Camera Ticket Scanner")]
	internal sealed class CameraTicketScanner : TicketScanner
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

		[Tooltip("The headset camera stream")]
		[SerializeField] private PassthroughCameraAccess _cameraAccess;

		[Tooltip("One module (square) of the printed code, in millimetres: 0.743 on the tickets")]
		[SerializeField] private float _moduleSizeMillimeters = 0.743f;

		[Tooltip("Whether camera images come back bottom row first, as Unity stores textures. If the panel says codes "
			+ "read mirrored, switch this.")]
		[SerializeField] private bool _isImageBottomUp = true;

		#endregion

		#region Fields

		private const string CameraPermission = OVRPermissionsRequester.PassthroughCameraAccessPermission;
		private const int BytesPerPixel = 4;
		private const float ScanRateWindowSeconds = 2f;

		private readonly QrImageDecoder _decoder = new();
		private readonly StringBuilder _status = new();

		private PermissionState _permission;
		private bool _isScanningRequested;
		private bool _isSupported = true;
		private bool _canReadBack = true;
		private byte[] _pixels = Array.Empty<byte>();
		private DateTime _lastImageTime;
		private QrDecode _lastDecode;
		private bool _hasDecoded;
		private bool _hasFound;
		private bool _wasLastFoundMirrored;
		private bool _wasLastFoundInverted;
		private int _windowScanCount;
		private float _windowStartSeconds;
		private float _scansPerSecond;

		#endregion

		#region Properties

		public override string Status => BuildStatus();

		public override Texture Preview => _cameraAccess.IsPlaying ? _cameraAccess.GetTexture() : null;

		// Switches the camera stream itself on and off; scanning carries on with the next image once it plays again
		public override bool IsScanning
		{
			get => _isScanningRequested;
			set
			{
				_isScanningRequested = value;
				_cameraAccess.enabled = value;
			}
		}

		#endregion

		#region Setup

		// The camera component's own switch says whether the scene starts scanning
		private void Awake()
		{
			_isScanningRequested = _cameraAccess.enabled;
		}

		private void Start()
		{
			_canReadBack = SystemInfo.supportsAsyncGPUReadback;
			if (!_canReadBack)
			{
				Debug.LogWarning($"[Ticket Scan] The camera scanner can't run here: {BuildStatus()}", this);
				return;
			}

			// Meta's check goes by the headset model, so it only explains a camera that doesn't start
			_isSupported = PassthroughCameraAccess.IsSupported;
			if (!_isSupported)
			{
				Debug.LogWarning("[Ticket Scan] Meta's check says this headset can't share its camera images; trying "
					+ "anyway.", this);
			}

			ScanAsync(destroyCancellationToken).Forget();
		}

		#endregion

		#region Internal

		internal void Configure(PassthroughCameraAccess cameraAccess)
		{
			_cameraAccess = cameraAccess;
		}

		#endregion

		#region Private

		private async UniTaskVoid ScanAsync(CancellationToken cancellationToken)
		{
			try
			{
				_permission = PermissionState.Asking;
				bool isGranted = await AndroidPermissionUtil.RequestAsync(CameraPermission, cancellationToken);
				_permission = isGranted ? PermissionState.Granted : PermissionState.Denied;
				if (!isGranted)
				{
					Debug.LogWarning("[Ticket Scan] No camera permission, so the camera scanner can't read tickets.",
						this);
					return;
				}

				_windowStartSeconds = Time.realtimeSinceStartup;
				while (!cancellationToken.IsCancellationRequested)
				{
					await ScanNextImageAsync(cancellationToken);
				}
			}
			catch (OperationCanceledException)
			{
				// The scene closed
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
				Debug.LogError("[Ticket Scan] The camera scanner stopped after the error above.", this);
			}
		}

		// Waits for a camera image newer than the last one read, reads it back from the GPU, and decodes it on a worker
		// thread
		private async UniTask ScanNextImageAsync(CancellationToken cancellationToken)
		{
			// After the camera's own Update, which hands its newest image to the texture
			await UniTask.WaitUntil(IsNewImageReady, PlayerLoopTiming.PreLateUpdate, cancellationToken);
			_lastImageTime = _cameraAccess.Timestamp;
			Texture image = _cameraAccess.GetTexture();
			if (image == null)
			{
				// The camera stopped in between, e.g. the headset was taken off
				return;
			}

			AsyncGPUReadbackRequest request = await AsyncGPUReadback.Request(image, 0)
				.ToUniTask(PlayerLoopTiming.Update, cancellationToken);
			int width = request.width;
			int height = request.height;
			if (request.hasError || !CopyPixels(request, width, height))
			{
				return;
			}

			bool isBottomUp = _isImageBottomUp;
			QrDecode decode = await UniTask.RunOnThreadPool(() => _decoder.Decode(_pixels, width, height, isBottomUp),
				cancellationToken: cancellationToken);

			// Back on the main thread
			CountScan(decode);
			if (decode.IsFound)
			{
				float distanceMeters = EstimateDistanceMeters(decode, width, height);
				RaiseRead(new TicketRead(decode.Text, distanceMeters, decode.ModulePixels, decode.DecodeMilliseconds,
					Time.realtimeSinceStartup));
			}
		}

		private bool IsNewImageReady()
		{
			return _cameraAccess.IsPlaying && _cameraAccess.Timestamp != _lastImageTime;
		}

		// The read-back pixels into the reused buffer; false (and a warning) when they aren't 4 bytes each
		private bool CopyPixels(AsyncGPUReadbackRequest request, int width, int height)
		{
			NativeArray<byte> data = request.GetData<byte>();
			if (data.Length != width * height * BytesPerPixel)
			{
				Debug.LogWarning($"[Ticket Scan] The camera image came back as {data.Length} bytes for {width} x "
					+ $"{height} pixels, not 4 bytes a pixel, so it can't be decoded.", this);
				return false;
			}

			if (_pixels.Length != data.Length)
			{
				_pixels = new byte[data.Length];
			}

			data.CopyTo(_pixels);
			return true;
		}

		private void CountScan(QrDecode decode)
		{
			_lastDecode = decode;
			_hasDecoded = true;
			if (decode.IsFound)
			{
				_hasFound = true;
				_wasLastFoundInverted = decode.IsInverted;
				_wasLastFoundMirrored = decode.IsMirrored;
			}

			_windowScanCount++;
			float now = Time.realtimeSinceStartup;
			float windowSeconds = now - _windowStartSeconds;
			if (windowSeconds >= ScanRateWindowSeconds)
			{
				_scansPerSecond = _windowScanCount / windowSeconds;
				_windowScanCount = 0;
				_windowStartSeconds = now;
			}
		}

		// Metres from the camera to the code: the finder squares' spacing on paper against the angle between them as
		// the camera saw them. NaN when the module size or the camera's lens data are missing.
		private float EstimateDistanceMeters(QrDecode decode, int width, int height)
		{
			float spacingModules = decode.FinderSpacingModules;
			if (float.IsNaN(spacingModules) || !_cameraAccess.IsPlaying)
			{
				return float.NaN;
			}

			Vector3 topLeft = GetRayDirection(decode.TopLeft, width, height);
			Vector3 topRight = GetRayDirection(decode.TopRight, width, height);
			Vector3 bottomLeft = GetRayDirection(decode.BottomLeft, width, height);
			float angleDegrees = (Vector3.Angle(topLeft, topRight) + Vector3.Angle(topLeft, bottomLeft)) * 0.5f;
			if (angleDegrees <= 0f)
			{
				return float.NaN;
			}

			float spacingMeters = spacingModules * _moduleSizeMillimeters * 0.001f;
			return spacingMeters * 0.5f / Mathf.Tan(angleDegrees * 0.5f * Mathf.Deg2Rad);
		}

		// Image pixels (from the top-left, y down) to the camera's viewport (from the bottom-left), then to a ray
		private Vector3 GetRayDirection(Vector2 imagePoint, int width, int height)
		{
			Vector2 viewportPoint = new(imagePoint.x / width, 1f - imagePoint.y / height);
			return _cameraAccess.ViewportPointToRay(viewportPoint).direction;
		}

		private string BuildStatus()
		{
			_status.Clear();
			if (!_canReadBack)
			{
				return "This device can't read camera images back from the GPU.";
			}

			switch (_permission)
			{
				case PermissionState.Asking:
					return "Waiting for the camera permission...";
				case PermissionState.Denied:
					return "No camera permission. Allow it in the headset's Settings > Privacy & safety > App "
						+ "permissions, then restart the scene.";
				case PermissionState.Granted:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(_permission), _permission, null);
			}

			if (!_isScanningRequested)
			{
				return "Scanning is off.";
			}

			if (!_cameraAccess.enabled || !_cameraAccess.IsPlaying)
			{
				_status.Append(_cameraAccess.enabled
					? "Starting the camera..."
					: "The camera didn't start (see the console).");
				if (!_isSupported)
				{
					_status.Append(" Meta's check says this headset can't share its camera images: that needs a "
						+ "Quest 3 or 3S on Horizon OS v74+, or Meta Horizon Link v85+ in the Editor.");
				}
				else if (Application.isEditor)
				{
					_status.Append(" In the Editor it needs Meta Horizon Link v85+ with passthrough over Link on.");
				}

				return _status.ToString();
			}

			Vector2Int resolution = _cameraAccess.CurrentResolution;
			_status.Append(_cameraAccess.CameraPosition).Append(" camera ").Append(resolution.x).Append('x')
				.Append(resolution.y);
			if (_hasDecoded)
			{
				_status.Append(" · ").Append(_scansPerSecond.ToString("0.0", CultureInfo.InvariantCulture))
					.Append(" scans/s · last ")
					.Append(_lastDecode.DecodeMilliseconds.ToString("0", CultureInfo.InvariantCulture)).Append(" ms");
			}

			if (_hasFound)
			{
				_status.Append(_wasLastFoundInverted ? " · inverted" : " · normal colours");
				if (_wasLastFoundMirrored)
				{
					_status.Append(" · MIRRORED: switch Is Image Bottom Up");
				}
			}

			return _status.ToString();
		}

		#endregion
	}
}
