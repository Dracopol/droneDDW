using System.Text;
using TMPro;
using UnityEngine;

namespace DroneDDW.Interaction
{
	// A label by the hand for tuning the finger grab on the headset: each finger's distance to the thumb against its
	// grab and release distances, and which finger holds the grab. Shown or hidden by the Finger Grab Settings toggle.
	internal class FingerGrabReadout : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private FingerGrabButtonReader _reader;
		[SerializeField] private TextMeshPro _label;

		[Tooltip("The XR camera. The label turns to face it.")]
		[SerializeField] private Transform _head;

		[Tooltip("Where the label sits relative to the thumb tip, in world directions")]
		[SerializeField] private Vector3 _offsetMeters = new(0f, 0.1f, 0f);

		#endregion

		#region Fields

		private const float CentimetersPerMeter = 100f;
		private const string HoldingColor = "#3CD070";
		private const string ClosedColor = "#F2C94C";
		private const string OffColor = "#808080";

		private static readonly GrabFinger[] Fingers =
		{
			GrabFinger.Index,
			GrabFinger.Middle,
			GrabFinger.Ring,
			GrabFinger.Little,
		};

		private readonly StringBuilder _text = new();
		private Transform _transform;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			bool isVisible = _reader.Settings.IsReadoutVisible;
			_label.renderer.enabled = isVisible;
			if (!isVisible)
			{
				return;
			}

			_text.Clear();
			_text.Append(_reader.Handedness).Append(" hand");

			if (!_reader.IsTracked)
			{
				_text.Append(": not tracked");
				_label.SetText(_text);
				return;
			}

			foreach (GrabFinger finger in Fingers)
			{
				AppendFinger(finger);
			}

			_label.SetText(_text);
			PlaceLabel();
		}

		#endregion

		#region Internal

		internal void Configure(FingerGrabButtonReader reader, TextMeshPro label, Transform head)
		{
			_reader = reader;
			_label = label;
			_head = head;
		}

		#endregion

		#region Private

		// "Middle 2.4 cm (3.0 / 4.5)": distance, then grab / release distance. Green while holding the grab,
		// yellow while closed, grey when the finger is switched off.
		private void AppendFinger(GrabFinger finger)
		{
			FingerGrabThreshold threshold = _reader.GetThreshold(finger);
			_text.Append('\n');

			if (!threshold.IsEnabled)
			{
				_text.Append("<color=").Append(OffColor).Append('>').Append(finger).Append(" off</color>");
				return;
			}

			bool isHolding = _reader.HoldingFinger == finger;
			string color = null;
			if (isHolding)
			{
				color = HoldingColor;
			}
			else if (_reader.IsFingerClosed(finger))
			{
				color = ClosedColor;
			}

			if (color != null)
			{
				_text.Append("<color=").Append(color).Append('>');
			}

			_text.AppendFormat("{0} {1:0.0} cm ({2:0.0} / {3:0.0})",
				finger,
				_reader.GetDistanceMeters(finger) * CentimetersPerMeter,
				threshold.GrabDistanceMeters * CentimetersPerMeter,
				threshold.ReleaseDistanceMeters * CentimetersPerMeter);

			if (isHolding)
			{
				_text.Append(" holding");
			}

			if (color != null)
			{
				_text.Append("</color>");
			}
		}

		private void PlaceLabel()
		{
			Vector3 position = _reader.ThumbTipPosition + _offsetMeters;
			_transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - _head.position));
		}

		#endregion
	}
}
