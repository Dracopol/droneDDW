using UnityEngine;

namespace DroneDDW.Admin
{
	// A black plane just in front of the headset camera that darkens everything behind the menu (passthrough too)
	// while the session is paused. It draws after the world's transparent objects (render queue 3500) and before the
	// menu (3600), so only the menu stays bright.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Pause Dimmer")]
	internal sealed class PauseDimmer : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private AdminMenuSettings _settings;
		[SerializeField] private Renderer _renderer;

		[Tooltip("The headset camera; the plane moves under it when the scene starts")]
		[SerializeField] private Transform _head;

		#endregion

		#region Fields

		// In front of the camera's near plane (0.01 m), behind anything the hands could hold up
		private const float DistanceMeters = 0.1f;

		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

		private MaterialPropertyBlock _propertyBlock;
		private float _opacity;

		#endregion

		#region Properties

		internal bool IsDimmed { get; set; }

		// 0 shows the world, 1 hides it
		internal float Opacity => _opacity;

		#endregion

		#region Setup

		private void Awake()
		{
			_propertyBlock = new MaterialPropertyBlock();
			Transform dimmer = transform;
			dimmer.SetParent(_head, false);
			dimmer.localPosition = new Vector3(0f, 0f, DistanceMeters);
			dimmer.localRotation = Quaternion.identity;
			Apply();
		}

		#endregion

		#region Update

		private void Update()
		{
			float target = IsDimmed ? _settings.DimOpacity : 0f;
			_opacity = Mathf.MoveTowards(_opacity, target, Time.unscaledDeltaTime / _settings.DimSeconds);
			Apply();
		}

		#endregion

		#region Internal

		internal void Configure(AdminMenuSettings settings, Renderer dimmerRenderer, Transform head)
		{
			_settings = settings;
			_renderer = dimmerRenderer;
			_head = head;
		}

		internal void ConfigureHead(Transform head)
		{
			_head = head;
		}

		#endregion

		#region Private

		private void Apply()
		{
			_renderer.enabled = _opacity > 0.001f;
			_propertyBlock.SetColor(BaseColorId, new Color(0f, 0f, 0f, _opacity));
			_renderer.SetPropertyBlock(_propertyBlock);
		}

		#endregion
	}
}
