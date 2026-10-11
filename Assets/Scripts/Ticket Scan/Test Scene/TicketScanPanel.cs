using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DroneDDW.TicketScan.TestScene
{
	// The ticket scan test scenes' panel: the code last read in large digits with what the scanner measured, the raw
	// code, the time to the first read, the scanner's state and the last few reads, plus the camera image the scanner
	// reads in the camera scene. TicketScanTestControls fills it in.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Ticket Scan/Ticket Scan Panel")]
	internal sealed class TicketScanPanel : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("The ticket number, large")]
		[SerializeField] private TextMeshProUGUI _number;

		[Tooltip("When the code was read and what the scanner measured")]
		[SerializeField] private TextMeshProUGUI _details;

		[Tooltip("The code's text as read")]
		[SerializeField] private TextMeshProUGUI _payload;

		[SerializeField] private TextMeshProUGUI _firstRead;
		[SerializeField] private TextMeshProUGUI _status;
		[SerializeField] private TextMeshProUGUI _log;

		[Tooltip("The camera image, or none in a scene without one")]
		[SerializeField] private RawImage _preview;

		[Tooltip("The largest the camera image may be on the panel, in panel units (mm); it keeps its shape inside")]
		[SerializeField] private Vector2 _previewMaxSize;

		#endregion

		#region Fields

		private const float MetersToCentimeters = 100f;

		private static readonly Color FreshColor = new(0.42f, 0.9f, 0.55f);
		private static readonly Color StaleColor = new(0.86f, 0.88f, 0.91f);
		private static readonly Color OtherCodeColor = new(1f, 0.62f, 0.38f);
		private static readonly Color WaitingColor = new(0.55f, 0.58f, 0.63f);

		private readonly StringBuilder _text = new();

		private Transform _transform;
		private Texture _shownPreview;

		#endregion

		#region Properties

		// What the panel shows, for the play mode check
		internal string NumberText => _number.text;
		internal string StatusText => _status.text;
		internal string LogText => _log.text;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
		}

		#endregion

		#region Internal

		internal void Configure(TextMeshProUGUI number, TextMeshProUGUI details, TextMeshProUGUI payload,
			TextMeshProUGUI firstRead, TextMeshProUGUI status, TextMeshProUGUI log, RawImage preview,
			Vector2 previewMaxSize)
		{
			_number = number;
			_details = details;
			_payload = payload;
			_firstRead = firstRead;
			_status = status;
			_log = log;
			_preview = preview;
			_previewMaxSize = previewMaxSize;
		}

		internal void PlaceAt(Vector3 position, Quaternion rotation)
		{
			_transform.SetPositionAndRotation(position, rotation);
		}

		internal void ShowWaiting()
		{
			_number.text = "---";
			_number.color = WaitingColor;
			_details.text = "No code read yet";
			_payload.text = string.Empty;
		}

		// The read's number (or that it isn't a ticket), green while fresh, with its age and measurements
		internal void ShowRead(TicketRead read, float ageSeconds, bool isFresh)
		{
			if (read.IsTicket)
			{
				_number.text = read.Number.ToString("000", CultureInfo.InvariantCulture);
				_number.color = isFresh ? FreshColor : StaleColor;
			}
			else
			{
				_number.text = "Not a ticket";
				_number.color = OtherCodeColor;
			}

			_text.Clear();
			_text.Append("read ").Append(ageSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s ago");
			string measurements = FormatMeasurements(read);
			if (measurements.Length > 0)
			{
				_text.Append(" · ").Append(measurements);
			}

			_details.text = _text.ToString();
			_payload.text = read.Payload;
		}

		internal void ShowFirstRead(string text)
		{
			_firstRead.text = text;
		}

		internal void ShowStatus(string text)
		{
			_status.text = text;
		}

		internal void ShowLog(string text)
		{
			_log.text = text;
		}

		// The camera image at its own shape, as large as fits; nothing when texture is null
		internal void ShowPreview(Texture texture)
		{
			if (_preview == null || texture == _shownPreview)
			{
				return;
			}

			_shownPreview = texture;
			_preview.texture = texture;
			_preview.enabled = texture != null;
			if (texture == null || texture.width <= 0 || texture.height <= 0)
			{
				return;
			}

			float scale = Mathf.Min(_previewMaxSize.x / texture.width, _previewMaxSize.y / texture.height);
			_preview.rectTransform.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
		}

		// "~21 cm · 3.1 px/module · 38 ms", leaving out what the scanner couldn't measure
		internal static string FormatMeasurements(TicketRead read)
		{
			StringBuilder text = new();
			if (!float.IsNaN(read.DistanceMeters))
			{
				text.Append('~').Append((read.DistanceMeters * MetersToCentimeters)
					.ToString("0", CultureInfo.InvariantCulture)).Append(" cm");
			}

			if (!float.IsNaN(read.PixelsPerModule))
			{
				AppendSeparator(text);
				text.Append(read.PixelsPerModule.ToString("0.0", CultureInfo.InvariantCulture)).Append(" px/module");
			}

			if (!float.IsNaN(read.DecodeMilliseconds))
			{
				AppendSeparator(text);
				text.Append(read.DecodeMilliseconds.ToString("0", CultureInfo.InvariantCulture)).Append(" ms");
			}

			return text.ToString();
		}

		#endregion

		#region Private

		private static void AppendSeparator(StringBuilder text)
		{
			if (text.Length > 0)
			{
				text.Append(" · ");
			}
		}

		#endregion
	}
}
