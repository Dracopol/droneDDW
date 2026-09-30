using DroneDDW.Parks.Levels;
using TMPro;
using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// The name of the level the slider's knob is on ("Balanced", "Nature skewed" ...), shown while a hand moves the
	/// knob and for a moment after. It floats above and beyond the knob, where the hand doesn't cover it, turned to
	/// face the viewer. Put it on an empty that holds the label as a child: this moves the empty, never the label.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Level Label")]
	public sealed class ParkLevelLabel : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkLevelSlider _slider;
		[SerializeField] private TMP_Text _label;

		[SerializeField, Tooltip("The XR camera. Falls back to Camera.main when left empty.")]
		private Transform _head;

		[SerializeField, Tooltip("Where the label floats from the knob, in the slider's space: up, and away from the "
			+ "viewer.")]
		private Vector3 _offsetMeters = new Vector3(0f, 0.1f, 0.06f);

		[SerializeField, Min(0f), Tooltip("How long the label stays after the knob is let go.")]
		private float _holdSeconds = 1.5f;

		[SerializeField, Min(0.01f), Tooltip("Seconds the label takes to fade in or out.")]
		private float _fadeSeconds = 0.2f;

		#endregion

		#region Fields

		private Transform _transform;
		private float _alpha;
		private float _lastMoveTime = float.NegativeInfinity;
		private int _shownDetent = -1;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
			ApplyAlpha();
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			if (_slider.IsDragging)
			{
				_lastMoveTime = Time.time;
			}

			float targetAlpha = Time.time - _lastMoveTime < _holdSeconds ? 1f : 0f;
			_alpha = Mathf.MoveTowards(_alpha, targetAlpha, Time.deltaTime / _fadeSeconds);
			ApplyAlpha();
			if (_alpha <= 0f)
			{
				return;
			}

			int detent = _slider.Detent;
			if (detent != _shownDetent)
			{
				_shownDetent = detent;
				_label.text = ParkLevels.GetName(detent);
			}

			Place();
		}

		#endregion

		#region Internal

		internal void Configure(ParkLevelSlider slider, TMP_Text label, Transform head)
		{
			_slider = slider;
			_label = label;
			_head = head;
		}

		#endregion

		#region Private

		private void ApplyAlpha()
		{
			_label.alpha = _alpha;
			_label.enabled = _alpha > 0f;
		}

		private void Place()
		{
			Vector3 position = _slider.Handle.position + _slider.transform.TransformVector(_offsetMeters);
			Transform head = ResolveHead();
			Quaternion rotation = head != null
				? Quaternion.LookRotation(position - head.position)
				: _transform.rotation;
			_transform.SetPositionAndRotation(position, rotation);
		}

		private Transform ResolveHead()
		{
			if (_head != null)
			{
				return _head;
			}

			Camera main = Camera.main;
			return main != null ? main.transform : null;
		}

		#endregion
	}
}
