using UnityEngine;
using UnityEngine.XR.Hands;

namespace DroneDDW.Experience
{
	// A see-through hand the participant lines their own hand up with (ported from the Questionnaire tutorial's)
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Ghost Hand")]
	internal sealed class GhostHand : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Handedness _handedness;

		[Tooltip("The ghost's own palm bone: the participant's palm has to reach it")]
		[SerializeField] private Transform _palm;

		[SerializeField] private Renderer _renderer;

		[SerializeField] private Color _waitingColor = new(1f, 1f, 1f, 0.35f);
		[SerializeField] private Color _lockedColor = new(0.35f, 1f, 0.45f, 0.6f);

		#endregion

		#region Fields

		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

		private MaterialPropertyBlock _propertyBlock;

		#endregion

		#region Properties

		internal Handedness Handedness => _handedness;
		internal Pose PalmPose => new(_palm.position, _palm.rotation);

		#endregion

		#region Setup

		private void Awake()
		{
			_propertyBlock = new MaterialPropertyBlock();
			SetVisible(false);
		}

		#endregion

		#region Internal

		internal void Configure(Handedness handedness, Transform palm, Renderer handRenderer)
		{
			_handedness = handedness;
			_palm = palm;
			_renderer = handRenderer;
		}

		internal void SetVisible(bool isVisible)
		{
			_renderer.enabled = isVisible;
		}

		// 0 while waiting for the participant's hand, 1 once it has held in place long enough
		internal void SetProgress(float progress)
		{
			_propertyBlock.SetColor(BaseColorId, Color.Lerp(_waitingColor, _lockedColor, progress));
			_renderer.SetPropertyBlock(_propertyBlock);
		}

		#endregion
	}
}
