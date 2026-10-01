using System.Text;
using TMPro;
using UnityEngine;

namespace DroneDDW.FingerGrab
{
	// A label by the hand for tuning the finger grab on the headset: for each finger, its tip's distance to the thumb tip
	// and its knuckle's distance to the thumb's knuckle, against their grab and release distances, and which pair holds
	// the grab. Shown or hidden by the Finger Grab Settings toggle.
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

		// One line per finger: its tip pair, then its knuckle pair
		private static readonly (string Finger, GrabPair Tip, GrabPair Knuckle)[] Rows =
		{
			("Index", GrabPair.IndexTip, GrabPair.IndexKnuckle),
			("Middle", GrabPair.MiddleTip, GrabPair.MiddleKnuckle),
			("Ring", GrabPair.RingTip, GrabPair.RingKnuckle),
			("Little", GrabPair.LittleTip, GrabPair.LittleKnuckle),
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

			_text.Append(", cm: tip | knuckle");
			foreach ((string finger, GrabPair tip, GrabPair knuckle) in Rows)
			{
				_text.Append('\n').Append(finger).Append(' ');
				AppendPair(tip);
				_text.Append(" | ");
				AppendPair(knuckle);
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

		// "2.4 (3.0/4.5)": distance, then grab / release distance. Green with "holding" while the pair holds the grab,
		// yellow while it is closed, grey "off" when it is switched off.
		private void AppendPair(GrabPair pair)
		{
			FingerGrabThreshold threshold = _reader.GetThreshold(pair);
			if (!threshold.IsEnabled)
			{
				_text.Append("<color=").Append(OffColor).Append(">off</color>");
				return;
			}

			bool isHolding = _reader.HoldingPair == pair;
			string color = null;
			if (isHolding)
			{
				color = HoldingColor;
			}
			else if (_reader.IsPairClosed(pair))
			{
				color = ClosedColor;
			}

			if (color != null)
			{
				_text.Append("<color=").Append(color).Append('>');
			}

			_text.AppendFormat("{0:0.0} ({1:0.0}/{2:0.0})",
				_reader.GetDistanceMeters(pair) * CentimetersPerMeter,
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
