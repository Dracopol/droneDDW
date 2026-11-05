using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

namespace DroneDDW.Recording
{
	/// <summary>
	/// Spectator camera for recording gameplay. Created at runtime by the editor recording tools, survives scene loads,
	/// follows the XR head with smoothing and renders into its own RenderTexture (never to the screen or headset).
	/// </summary>
	public class RecordingCamera : MonoBehaviour
	{
		#region Fields

		private const string ObjectName = "Recording Camera";
		private const float HeadSearchIntervalSeconds = 0.5f;

		private Camera _camera;
		private Transform _transform;
		private Camera _headCamera;
		private float _fieldOfViewDegrees;
		private float _positionSmoothingSeconds;
		private float _rotationSmoothingSeconds;
		private float _nextHeadSearchTime;

		#endregion

		#region Properties

		internal RenderTexture RenderTexture { get; private set; }

		#endregion

		#region Setup

		private void OnDestroy()
		{
			if (RenderTexture == null)
			{
				return;
			}

			RenderTexture.Release();
			Destroy(RenderTexture);
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			if (_headCamera == null)
			{
				TryAttachToHead();
				return;
			}

			Transform head = _headCamera.transform;
			Vector3 targetPosition = head.position;
			Quaternion targetRotation = Quaternion.LookRotation(head.forward, Vector3.up);

			_transform.SetPositionAndRotation(
				Vector3.Lerp(_transform.position, targetPosition, GetBlend(_positionSmoothingSeconds)),
				Quaternion.Slerp(_transform.rotation, targetRotation, GetBlend(_rotationSmoothingSeconds)));
		}

		#endregion

		#region Internal

		internal static RecordingCamera Create(
			int widthPixels,
			int heightPixels,
			float fieldOfViewDegrees,
			float positionSmoothingSeconds,
			float rotationSmoothingSeconds)
		{
			GameObject cameraObject = new(ObjectName);
			DontDestroyOnLoad(cameraObject);

			RecordingCamera recordingCamera = cameraObject.AddComponent<RecordingCamera>();
			recordingCamera.Initialize(
				widthPixels,
				heightPixels,
				fieldOfViewDegrees,
				positionSmoothingSeconds,
				rotationSmoothingSeconds);
			return recordingCamera;
		}

		internal void Dispose()
		{
			Destroy(gameObject);
		}

		#endregion

		#region Private

		private void Initialize(
			int widthPixels,
			int heightPixels,
			float fieldOfViewDegrees,
			float positionSmoothingSeconds,
			float rotationSmoothingSeconds)
		{
			_transform = transform;
			_fieldOfViewDegrees = fieldOfViewDegrees;
			_positionSmoothingSeconds = positionSmoothingSeconds;
			_rotationSmoothingSeconds = rotationSmoothingSeconds;

			RenderTexture = new RenderTexture(
				widthPixels,
				heightPixels,
				GraphicsFormat.R8G8B8A8_SRGB,
				GraphicsFormat.D24_UNorm_S8_UInt);
			RenderTexture.name = ObjectName;
			RenderTexture.Create();

			_camera = gameObject.AddComponent<Camera>();
			_camera.enabled = false;

			TryAttachToHead();
		}

		private void TryAttachToHead()
		{
			if (Time.unscaledTime < _nextHeadSearchTime)
			{
				return;
			}

			_nextHeadSearchTime = Time.unscaledTime + HeadSearchIntervalSeconds;
			_headCamera = FindHeadCamera();
			if (_headCamera == null)
			{
				_camera.enabled = false;
				return;
			}

			// Match the head camera's look (background, culling, clipping), then make it a plain off-screen camera
			_camera.CopyFrom(_headCamera);
			_camera.stereoTargetEye = StereoTargetEyeMask.None;
			_camera.targetTexture = RenderTexture;
			_camera.fieldOfView = _fieldOfViewDegrees;
			_camera.depth = _headCamera.depth - 1f;

			UniversalAdditionalCameraData headData = _headCamera.GetUniversalAdditionalCameraData();
			UniversalAdditionalCameraData recordingData = _camera.GetUniversalAdditionalCameraData();
			recordingData.renderType = CameraRenderType.Base;
			recordingData.allowXRRendering = false;
			recordingData.renderPostProcessing = headData.renderPostProcessing;
			recordingData.antialiasing = headData.antialiasing;
			recordingData.volumeLayerMask = headData.volumeLayerMask;
			recordingData.renderShadows = headData.renderShadows;

			_transform.SetPositionAndRotation(
				_headCamera.transform.position,
				Quaternion.LookRotation(_headCamera.transform.forward, Vector3.up));
			_camera.enabled = true;
		}

		private Camera FindHeadCamera()
		{
			Camera mainCamera = Camera.main;
			if (mainCamera != null && mainCamera != _camera)
			{
				return mainCamera;
			}

			Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
			foreach (Camera camera in cameras)
			{
				if (camera != _camera && camera.isActiveAndEnabled && camera.stereoTargetEye != StereoTargetEyeMask.None)
				{
					return camera;
				}
			}

			return null;
		}

		private static float GetBlend(float smoothingSeconds)
		{
			if (smoothingSeconds <= 0f)
			{
				return 1f;
			}

			// Frame-rate independent exponential smoothing
			return 1f - Mathf.Exp(-Time.deltaTime / smoothingSeconds);
		}

		#endregion
	}
}
