using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDDW.TicketScan.TestScene
{
	// Runs a ticket scan test scene: shows on the panel what the scene's scanner reads, plays a sound for each new read
	// (Lock In for a ticket, Stamp Rejected for any other QR code) and times how long the first ticket read takes. A
	// new read is a different code, or the same one after more than 1.5 s without it. Space or A / X clears the reads
	// and starts the timer again; R or B / Y places the panel in front of you again. The panel moves in front of your
	// head, below your eyes, when the headset reports its first pose.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Ticket Scan/Ticket Scan Test Controls")]
	internal sealed class TicketScanTestControls : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private TicketScanner _scanner;
		[SerializeField] private TicketScanPanel _panel;

		[Tooltip("The headset camera: the panel is placed in front of it")]
		[SerializeField] private Transform _head;

		[SerializeField] private AudioSource _audio;

		[Tooltip("Plays for each new ticket read")]
		[SerializeField] private AudioClip _ticketClip;

		[Tooltip("Plays for each new QR code that isn't a ticket")]
		[SerializeField] private AudioClip _otherCodeClip;

		[Tooltip("Clears the reads and starts timing the first read again")]
		[SerializeField] private InputAction _clearAction;

		[Tooltip("Places the panel in front of the head again")]
		[SerializeField] private InputAction _recenterAction;

		#endregion

		#region Fields

		// Ahead of the head and below its eyes, so a ticket held up in front of the face isn't hidden behind the panel
		internal const float PanelDistanceMeters = 0.6f;
		internal const float PanelDropMeters = 0.3f;

		// Below this the head looks almost straight up or down and its yaw is unreliable
		private const float MinFlatForwardLength = 0.2f;

		private const float NewReadGapSeconds = 1.5f;
		private const float FreshSeconds = 1f;
		private const float RefreshIntervalSeconds = 0.2f;
		private const int LogLength = 5;
		private const int LoggedPayloadLength = 48;

		private readonly List<string> _logLines = new();

		private bool _isPlaced;
		private bool _hasRead;
		private TicketRead _lastRead;
		private float _attemptStartSeconds;
		private float _firstReadSeconds = float.NaN;
		private float _nextRefreshSeconds;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_scanner.OnRead += HandleRead;
			_clearAction.Enable();
			_recenterAction.Enable();
		}

		private void OnDisable()
		{
			_scanner.OnRead -= HandleRead;
			_clearAction.Disable();
			_recenterAction.Disable();
		}

		private void Start()
		{
			Clear();
		}

		#endregion

		#region Update

		private void Update()
		{
			// The camera sits exactly on its parent's origin until the headset reports its first pose
			if (!_isPlaced && _head.localPosition != Vector3.zero)
			{
				_isPlaced = true;
				PlaceInFrontOfHead();
			}

			if (_clearAction.WasPressedThisFrame())
			{
				Clear();
			}

			if (_recenterAction.WasPressedThisFrame())
			{
				PlaceInFrontOfHead();
			}

			float now = Time.realtimeSinceStartup;
			if (now >= _nextRefreshSeconds)
			{
				_nextRefreshSeconds = now + RefreshIntervalSeconds;
				Refresh(now);
			}
		}

		#endregion

		#region Event handlers

		private void HandleRead(TicketRead read)
		{
			bool isNew = !_hasRead || read.Payload != _lastRead.Payload
				|| read.TimeSeconds - _lastRead.TimeSeconds > NewReadGapSeconds;
			_hasRead = true;
			_lastRead = read;
			if (isNew)
			{
				RecordNewRead(read);
			}

			_panel.ShowRead(read, 0f, true);
		}

		#endregion

		#region Internal

		internal void Configure(TicketScanner scanner, TicketScanPanel panel, Transform head, AudioSource audioSource,
			AudioClip ticketClip, AudioClip otherCodeClip, InputAction clearAction, InputAction recenterAction)
		{
			_scanner = scanner;
			_panel = panel;
			_head = head;
			_audio = audioSource;
			_ticketClip = ticketClip;
			_otherCodeClip = otherCodeClip;
			_clearAction = clearAction;
			_recenterAction = recenterAction;
		}

		// Space or A / X: forgets the reads and times the first read again from now
		internal void Clear()
		{
			_hasRead = false;
			_logLines.Clear();
			_attemptStartSeconds = Time.realtimeSinceStartup;
			_firstReadSeconds = float.NaN;
			_panel.ShowWaiting();
			_panel.ShowLog(string.Empty);
			Refresh(_attemptStartSeconds);
		}

		// Ahead of the head at the panel's distance and drop, turned with the head's yaw only, tilted to face the eyes
		internal void PlaceInFrontOfHead()
		{
			Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
			if (forward.magnitude < MinFlatForwardLength)
			{
				forward = Vector3.ProjectOnPlane(_head.up, Vector3.up);
			}

			Vector3 headPosition = _head.position;
			Vector3 position = headPosition + forward.normalized * PanelDistanceMeters
				+ Vector3.down * PanelDropMeters;
			_panel.PlaceAt(position, Quaternion.LookRotation(position - headPosition, Vector3.up));
			Debug.Log($"[Ticket Scan] Test scene: placed the panel in front of the head at {headPosition}.", this);
		}

		#endregion

		#region Private

		private void RecordNewRead(TicketRead read)
		{
			_audio.PlayOneShot(read.IsTicket ? _ticketClip : _otherCodeClip);
			bool isFirstTicket = read.IsTicket && float.IsNaN(_firstReadSeconds);
			if (isFirstTicket)
			{
				_firstReadSeconds = read.TimeSeconds - _attemptStartSeconds;
			}

			string line = FormatLogLine(read);
			_logLines.Insert(0, line);
			if (_logLines.Count > LogLength)
			{
				_logLines.RemoveAt(LogLength);
			}

			_panel.ShowLog(string.Join("\n", _logLines));
			string firstNote = isFirstTicket
				? $" (first ticket, {_firstReadSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s after start)"
				: string.Empty;
			Debug.Log($"[Ticket Scan] Read {line}{firstNote}", this);
		}

		private void Refresh(float now)
		{
			_panel.ShowStatus(_scanner.Status);
			_panel.ShowPreview(_scanner.Preview);
			if (_hasRead)
			{
				float ageSeconds = now - _lastRead.TimeSeconds;
				_panel.ShowRead(_lastRead, ageSeconds, ageSeconds < FreshSeconds);
			}

			_panel.ShowFirstRead(float.IsNaN(_firstReadSeconds)
				? $"Waiting for a ticket: {(now - _attemptStartSeconds).ToString("0", CultureInfo.InvariantCulture)} s"
				: $"First ticket read after {_firstReadSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s");
		}

		// "14:03:12  ticket 104 · ~21 cm · 3.1 px/module · 38 ms", or the start of another code's text
		private static string FormatLogLine(TicketRead read)
		{
			string time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
			if (!read.IsTicket)
			{
				string payload = read.Payload.Length > LoggedPayloadLength
					? read.Payload.Substring(0, LoggedPayloadLength) + "…"
					: read.Payload;
				return $"{time}  not a ticket: {payload}";
			}

			string number = read.Number.ToString("000", CultureInfo.InvariantCulture);
			string measurements = TicketScanPanel.FormatMeasurements(read);
			return measurements.Length > 0
				? $"{time}  ticket {number} · {measurements}"
				: $"{time}  ticket {number}";
		}

		#endregion
	}
}
