using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DroneDDW.Admin.Sessions;
using DroneDDW.TicketScan;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DroneDDW.Admin
{
	// The admin menu from the Figma mockups, for whoever runs the session. The left controller's menu button opens it
	// and pauses the session; either thumbstick moves the pale blue hover, trigger or grip clicks, A, B, X or Y goes
	// back. Screens:
	// - Check-in, when the scene's Session Control waits for a participant: it opens by itself, shows the ticket
	//   scanner's camera image and the number it reads (or the one typed on the number pad), and Start session hands
	//   the participant to the session.
	// - Check-in menu: the menu button on check-in or the number pad opens it, with Resume (back to check-in) and View
	//   session history; the menu button again goes back to check-in from it or the history
	// - Number pad: 0-9, Delete and OK, to type the participant number when the ticket doesn't scan
	// - Paused: Resume (closes the menu), Reset player location, View session history, Cancel session (back to
	//   check-in, nothing saved; "end" in the code), and Test save current session when the Testing checkbox is on
	// - Session history: the rows of Sessions.csv, each with Delete
	// - Cancel session / delete dialogs, which open on Go back so a double click can't confirm them
	// During a session the menu button closes the menu and resumes from any screen. Everything runs on unscaled time,
	// since the session's time stands still while the menu is open.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Admin Menu")]
	internal sealed class AdminMenu : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private AdminMenuSettings _settings;
		[SerializeField] private AdminMenuInput _input;
		[SerializeField] private AdminMenuPlacement _placement;
		[SerializeField] private PauseDimmer _dimmer;

		[Tooltip("The scene's session: paused while the menu is open, reset and recentred by its buttons")]
		[SerializeField] private SessionControl _sessionControl;

		[Header("Testing")]
		[Tooltip("Shows \"Test save current session\" on the Paused screen. It saves a test row (Id 0, group A, "
			+ "neutral, now) to the real session history. Turn off for real sessions.")]
		[SerializeField] private bool _isTestSaveShown;

		[Header("Canvas")]
		[Tooltip("The world-space canvas, laid out in the mockups' pixels and scaled by the settings")]
		[SerializeField] private RectTransform _canvas;

		[SerializeField] private CanvasGroup _canvasGroup;

		[Header("Screens")]
		[SerializeField] private AdminMenuScreen _pauseScreen;
		[SerializeField] private AdminMenuScreen _historyScreen;
		[SerializeField] private SessionHistoryScreen _history;
		[SerializeField] private AdminMenuScreen _deleteScreen;

		[Header("Paused")]
		[SerializeField] private AdminMenuButton _resumeButton;
		[SerializeField] private AdminMenuButton _historyButton;
		[SerializeField] private AdminMenuButton _locationButton;
		[SerializeField] private AdminMenuButton _testSaveButton;

		[Tooltip("The Paused screen's panel, built tall enough for the test save button; one pitch shorter without it")]
		[SerializeField] private RectTransform _pausePanel;

		[Tooltip("The distance between the Paused screen's buttons, in the menu's pixels")]
		[SerializeField] private float _pauseButtonPitch;

		[Header("Dialogs")]
		[SerializeField] private AdminMenuButton _deleteCancelButton;
		[SerializeField] private AdminMenuButton _deleteConfirmButton;

		[Tooltip("The delete dialog's Id, Date and Time lines")]
		[SerializeField] private TMP_Text _deleteDetails;

		[Tooltip("The delete dialog's Id, Date and Time names")]
		[SerializeField] private Color _detailsNameColor;

		[Header("End session")]
		[SerializeField] private AdminMenuButton _endButton;
		[SerializeField] private AdminMenuScreen _endScreen;
		[SerializeField] private AdminMenuButton _endCancelButton;
		[SerializeField] private AdminMenuButton _endConfirmButton;

		[Header("Check-in")]
		[Tooltip("Reads the participant's ticket while the check-in screen shows; set per scene. Without one the "
			+ "number can still be typed.")]
		[SerializeField] private TicketScanner _ticketScanner;

		[SerializeField] private AdminMenuScreen _checkInScreen;
		[SerializeField] private AdminMenuButton _typeNumberButton;
		[SerializeField] private AdminMenuButton _startSessionButton;

		[Tooltip("The participant number, large")]
		[SerializeField] private TMP_Text _participantNumber;

		[Tooltip("What to do, or what the scanner is doing")]
		[SerializeField] private TMP_Text _checkInStatus;

		[Tooltip("The scanner's camera image")]
		[SerializeField] private RawImage _checkInPreview;

		[Tooltip("The largest the camera image may be, in the menu's pixels; it keeps its shape inside")]
		[SerializeField] private Vector2 _checkInPreviewMaxSize;

		[Header("Check-in menu")]
		[SerializeField] private AdminMenuScreen _checkInMenuScreen;
		[SerializeField] private AdminMenuButton _checkInResumeButton;
		[SerializeField] private AdminMenuButton _checkInHistoryButton;

		[Header("Number pad")]
		[SerializeField] private AdminMenuScreen _numberPadScreen;

		[Tooltip("The digit keys, 0 to 9 in order")]
		[SerializeField] private AdminMenuButton[] _digitButtons;

		[SerializeField] private AdminMenuButton _deleteDigitButton;
		[SerializeField] private AdminMenuButton _numberPadOkButton;

		[Tooltip("The number typed so far")]
		[SerializeField] private TMP_Text _numberPadEntry;

		#endregion

		#region Fields

		private const float MetersPerMillimeter = 0.001f;
		private const string NoSessionsMessage = "No sessions yet";
		private const string UnreadableMessage = "Couldn't read the session history (see the log)";
		private const string SaveFailedMessage = "Couldn't save, see the log";
		private const float SavedLabelSeconds = 2.5f;

		// The test save button's row: Id 0, group A, the middle choice
		private const string TestSessionId = "0";
		private const string TestSessionGroup = "A";
		private const string TestSessionFormChoice = "neutral";

		// Participant numbers are ticket numbers; longer ones aren't (as in the ticket scanner)
		private const int MaxParticipantDigits = 6;
		private const string NoNumber = "---";
		private const float CheckInRefreshSeconds = 0.25f;
		private const string ScanInstruction = "Hold the participant's ticket up to the headset, QR code first";
		private const string TypeInstruction = "Type the participant number";
		private const string OtherCodeMessage = "That QR code isn't a ticket";

		private readonly List<Vector2> _centers = new();
		private readonly List<AdminMenuButton> _shownButtons = new();
		private float _pausePanelFullHeight;
		private AdminMenuScreen _screen;
		private AdminMenuButton _hovered;
		private SessionRecord _pendingDelete;
		private string _sessionsFolderOverride;
		private string _participantId = string.Empty;
		private string _typedNumber = string.Empty;
		private float _nextCheckInRefresh;
		private float _otherCodeUntil;
		private Texture _shownPreview;

		#endregion

		#region Properties

		internal bool IsOpen { get; private set; }

		// The check-in screen (or its number pad) is up, waiting for the admin to start the session
		internal bool IsCheckingIn { get; private set; }

		// The number Start session would start with: read from the ticket or typed, empty before either
		internal string ParticipantNumber => _participantId;

		// What the Play mode check looks at
		internal AdminMenuScreen CurrentScreen => _screen;
		internal AdminMenuButton Hovered => _hovered;
		internal SessionHistoryScreen History => _history;
		internal PauseDimmer Dimmer => _dimmer;
		internal AdminMenuScreen PauseScreen => _pauseScreen;
		internal AdminMenuScreen HistoryScreen => _historyScreen;
		internal AdminMenuScreen DeleteScreen => _deleteScreen;
		internal AdminMenuButton ResumeButton => _resumeButton;
		internal AdminMenuButton HistoryButton => _historyButton;
		internal AdminMenuButton LocationButton => _locationButton;
		internal AdminMenuButton EndButton => _endButton;
		internal AdminMenuScreen EndScreen => _endScreen;
		internal AdminMenuButton EndCancelButton => _endCancelButton;
		internal AdminMenuButton EndConfirmButton => _endConfirmButton;
		internal AdminMenuScreen CheckInScreen => _checkInScreen;
		internal AdminMenuScreen CheckInMenuScreen => _checkInMenuScreen;
		internal AdminMenuButton CheckInResumeButton => _checkInResumeButton;
		internal AdminMenuButton CheckInHistoryButton => _checkInHistoryButton;
		internal AdminMenuButton TypeNumberButton => _typeNumberButton;
		internal AdminMenuButton StartSessionButton => _startSessionButton;
		internal AdminMenuScreen NumberPadScreen => _numberPadScreen;
		internal IReadOnlyList<AdminMenuButton> DigitButtons => _digitButtons;
		internal AdminMenuButton NumberPadOkButton => _numberPadOkButton;
		internal AdminMenuButton TestSaveButton => _testSaveButton;
		internal float PausePanelHeight => _pausePanel.sizeDelta.y;
		internal AdminMenuButton DeleteCancelButton => _deleteCancelButton;
		internal AdminMenuButton DeleteConfirmButton => _deleteConfirmButton;
		internal string DeleteDetailsText => _deleteDetails.text;

		#endregion

		#region Setup

		private void Awake()
		{
			_pauseScreen.Hide();
			_historyScreen.Hide();
			_deleteScreen.Hide();
			_endScreen.Hide();
			_checkInScreen.Hide();
			_checkInMenuScreen.Hide();
			_numberPadScreen.Hide();
			_canvasGroup.alpha = 0f;
			_canvas.gameObject.SetActive(false);
			_pausePanelFullHeight = _pausePanel.sizeDelta.y;
			ApplyTestSaveVisibility();
			if (_sessionControl == null)
			{
				Debug.LogWarning("[Admin] The admin menu has no Session Control: it can't pause, reset or recentre.",
					this);
			}

			Debug.Log($"[Admin] Session history file: {CreateHistory().SessionsPath}", this);
		}

		// After every Awake, so Session Control knows whether Reset session brought a participant along
		private void Start()
		{
			if (_sessionControl != null && _sessionControl.IsCheckingIn)
			{
				OpenCheckIn();
			}
			else
			{
				SetScanning(false);
			}
		}

		private void OnEnable()
		{
			if (_ticketScanner != null)
			{
				_ticketScanner.OnRead += HandleTicketRead;
			}

			if (_sessionControl != null)
			{
				_sessionControl.OnSessionStart += HandleSessionStart;
			}
		}

		private void OnDisable()
		{
			if (_ticketScanner != null)
			{
				_ticketScanner.OnRead -= HandleTicketRead;
			}

			if (_sessionControl != null)
			{
				_sessionControl.OnSessionStart -= HandleSessionStart;
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			ApplyScale();
			FadeCanvas();

			MenuCommand command = _input.Read(Time.unscaledTime, _settings);
			if (IsCheckingIn)
			{
				RefreshCheckIn();
				if (command.Kind == MenuCommandKind.Toggle)
				{
					ToggleCheckInMenu(command.Hand);
				}
				else
				{
					DoCommand(command);
				}

				return;
			}

			if (!IsOpen)
			{
				if (command.Kind == MenuCommandKind.Toggle)
				{
					Open(command.Hand);
				}

				return;
			}

			DoCommand(command);
		}

		#endregion

		#region Event handlers

		// A session started without the menu's Start session (e.g. by the Play mode check): check-in is over
		private void HandleSessionStart()
		{
			if (IsCheckingIn)
			{
				CloseCheckIn();
			}
		}

		// A ticket read while the check-in screen shows becomes the participant number; the hover moves on to Start
		// session, so one click starts it
		private void HandleTicketRead(TicketRead read)
		{
			if (!IsCheckingIn || _screen != _checkInScreen)
			{
				return;
			}

			if (!read.IsTicket)
			{
				_otherCodeUntil = Time.unscaledTime + 2f;
				return;
			}

			string number = read.Number.ToString(CultureInfo.InvariantCulture);
			if (number == _participantId)
			{
				return;
			}

			_participantId = number;
			Debug.Log($"[Admin] Check-in: read ticket {number}.", this);
			ShowCheckIn(_hovered == _typeNumberButton || _hovered == null ? _startSessionButton : _hovered);
		}

		#endregion

		#region Internal

		internal void Configure(AdminMenuSettings settings, AdminMenuInput input, AdminMenuPlacement placement,
			PauseDimmer dimmer, RectTransform canvas, CanvasGroup canvasGroup, Color detailsNameColor)
		{
			_settings = settings;
			_input = input;
			_placement = placement;
			_dimmer = dimmer;
			_canvas = canvas;
			_canvasGroup = canvasGroup;
			_detailsNameColor = detailsNameColor;
		}

		internal void ConfigureScreens(AdminMenuScreen pauseScreen, AdminMenuScreen historyScreen,
			SessionHistoryScreen history, AdminMenuScreen deleteScreen, TMP_Text deleteDetails)
		{
			_pauseScreen = pauseScreen;
			_historyScreen = historyScreen;
			_history = history;
			_deleteScreen = deleteScreen;
			_deleteDetails = deleteDetails;
		}

		internal void ConfigureButtons(AdminMenuButton resumeButton, AdminMenuButton historyButton,
			AdminMenuButton locationButton, AdminMenuButton testSaveButton, AdminMenuButton deleteCancelButton,
			AdminMenuButton deleteConfirmButton)
		{
			_resumeButton = resumeButton;
			_historyButton = historyButton;
			_locationButton = locationButton;
			_testSaveButton = testSaveButton;
			_deleteCancelButton = deleteCancelButton;
			_deleteConfirmButton = deleteConfirmButton;
		}

		internal void ConfigurePausePanel(RectTransform pausePanel, float pauseButtonPitch)
		{
			_pausePanel = pausePanel;
			_pauseButtonPitch = pauseButtonPitch;
		}

		internal void ConfigureEndSession(AdminMenuButton endButton, AdminMenuScreen endScreen,
			AdminMenuButton endCancelButton, AdminMenuButton endConfirmButton)
		{
			_endButton = endButton;
			_endScreen = endScreen;
			_endCancelButton = endCancelButton;
			_endConfirmButton = endConfirmButton;
		}

		internal void ConfigureCheckIn(AdminMenuScreen checkInScreen, AdminMenuButton typeNumberButton,
			AdminMenuButton startSessionButton, TMP_Text participantNumber, TMP_Text checkInStatus,
			RawImage checkInPreview, Vector2 checkInPreviewMaxSize)
		{
			_checkInScreen = checkInScreen;
			_typeNumberButton = typeNumberButton;
			_startSessionButton = startSessionButton;
			_participantNumber = participantNumber;
			_checkInStatus = checkInStatus;
			_checkInPreview = checkInPreview;
			_checkInPreviewMaxSize = checkInPreviewMaxSize;
		}

		internal void ConfigureCheckInMenu(AdminMenuScreen checkInMenuScreen, AdminMenuButton checkInResumeButton,
			AdminMenuButton checkInHistoryButton)
		{
			_checkInMenuScreen = checkInMenuScreen;
			_checkInResumeButton = checkInResumeButton;
			_checkInHistoryButton = checkInHistoryButton;
		}

		internal void ConfigureNumberPad(AdminMenuScreen numberPadScreen, AdminMenuButton[] digitButtons,
			AdminMenuButton deleteDigitButton, AdminMenuButton numberPadOkButton, TMP_Text numberPadEntry)
		{
			_numberPadScreen = numberPadScreen;
			_digitButtons = digitButtons;
			_deleteDigitButton = deleteDigitButton;
			_numberPadOkButton = numberPadOkButton;
			_numberPadEntry = numberPadEntry;
		}

		// The scene's ticket scanner, which the check-in screen switches on while it shows; set where the prefab is
		// placed, like the head
		internal void ConfigureTicketScanner(TicketScanner ticketScanner)
		{
			_ticketScanner = ticketScanner;
		}

		// Whether the Paused screen shows the test save button; takes effect the next time the menu opens
		internal void ConfigureTestSave(bool isShown)
		{
			_isTestSaveShown = isShown;
		}

		// The scene's session and headset camera; set on the prefab's instance in each scene
		internal void ConfigureScene(SessionControl sessionControl, Transform head)
		{
			_sessionControl = sessionControl;
			_placement.ConfigureHead(head);
			_dimmer.ConfigureHead(head);
		}

		// The Play mode check points the history at a temporary folder, so it never touches real sessions
		internal void UseSessionsFolder(string folder)
		{
			_sessionsFolderOverride = folder;
		}

		// Opens the menu on the Paused screen and pauses the session
		internal void Open(MenuHand hand)
		{
			if (IsCheckingIn)
			{
				return;
			}

			IsOpen = true;
			_input.ClearStick();
			if (_sessionControl != null)
			{
				_sessionControl.Pause();
			}

			_dimmer.IsDimmed = true;
			_canvas.gameObject.SetActive(true);
			ApplyTestSaveVisibility();
			_placement.PlaceInFront();
			_placement.IsFollowing = true;
			ShowScreen(_pauseScreen, _resumeButton);
			Pulse(hand, isClick: true);
			Debug.Log("[Admin] Menu open, session paused.", this);
		}

		// Closes the menu and resumes the session
		internal void Close(MenuHand hand)
		{
			IsOpen = false;
			_placement.IsFollowing = false;
			_dimmer.IsDimmed = false;
			_pendingDelete = null;
			if (_sessionControl != null)
			{
				_sessionControl.Resume();
			}

			Pulse(hand, isClick: true);
			Debug.Log("[Admin] Menu closed, session resumed.", this);
		}

		#endregion

		#region Private

		private void ApplyScale()
		{
			float metersPerPixel = _settings.MillimetersPerPixel * MetersPerMillimeter;
			_canvas.localScale = new Vector3(metersPerPixel, metersPerPixel, metersPerPixel);
		}

		// Fades the whole menu in on open and out on close; once faded out, it stops drawing
		private void FadeCanvas()
		{
			if (!_canvas.gameObject.activeSelf)
			{
				return;
			}

			_canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, IsOpen ? 1f : 0f,
				Time.unscaledDeltaTime / _settings.FadeSeconds);
			if (IsOpen || _canvasGroup.alpha > 0f)
			{
				return;
			}

			SetHovered(null, MenuHand.None);
			if (_screen != null)
			{
				_screen.Hide();
				_screen = null;
			}

			_canvas.gameObject.SetActive(false);
		}

		private void ShowScreen(AdminMenuScreen screen, AdminMenuButton hovered)
		{
			if (_screen != null && _screen != screen)
			{
				_screen.Hide();
			}

			_screen = screen;

			// Hover first, so the screen's buttons appear already lit
			SetHovered(hovered, MenuHand.None);
			screen.Show();
			if (screen == _historyScreen)
			{
				_history.ScrollToShow(hovered, isInstant: true);
			}
		}

		private void DoCommand(MenuCommand command)
		{
			switch (command.Kind)
			{
				case MenuCommandKind.None:
					break;
				case MenuCommandKind.Toggle:
					Close(command.Hand);
					break;
				case MenuCommandKind.Move:
					MoveHover(command.Direction, command.Hand);
					break;
				case MenuCommandKind.Click:
					Click(command.Hand);
					break;
				case MenuCommandKind.Back:
					Back(command.Hand);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command.Kind), command.Kind, null);
			}
		}

		// Opens on the check-in screen, without pausing (nothing runs yet) or dimming, and switches the scanner on
		private void OpenCheckIn()
		{
			IsOpen = true;
			IsCheckingIn = true;
			_input.ClearStick();
			_canvas.gameObject.SetActive(true);
			_placement.PlaceInFront();
			_placement.IsFollowing = true;
			SetScanning(true);
			ShowCheckIn(_typeNumberButton);
			Debug.Log("[Admin] Check-in: waiting for the participant's ticket.", this);
		}

		// The check-in screen with the current number; Start session only shows once there is one
		private void ShowCheckIn(AdminMenuButton hovered)
		{
			bool hasNumber = _participantId.Length > 0;
			_participantNumber.text = hasNumber ? _participantId : NoNumber;
			_startSessionButton.SetShown(hasNumber);
			ShowScreen(_checkInScreen, hasNumber || hovered != _startSessionButton ? hovered : _typeNumberButton);
			_nextCheckInRefresh = 0f;
			RefreshCheckIn();
		}

		// The menu button during check-in: from check-in or the number pad to the check-in menu, from anywhere else
		// (the check-in menu, the history, the delete dialog) back to check-in. The scanner stays on meanwhile.
		private void ToggleCheckInMenu(MenuHand hand)
		{
			if (_screen == _checkInScreen || _screen == _numberPadScreen)
			{
				ShowScreen(_checkInMenuScreen, _checkInResumeButton);
			}
			else
			{
				ReturnToCheckIn();
			}

			Pulse(hand, isClick: true);
		}

		// Check-in again, the hover on Start session when there is a number
		private void ReturnToCheckIn()
		{
			_pendingDelete = null;
			ShowCheckIn(_participantId.Length > 0 ? _startSessionButton : _typeNumberButton);
		}

		// Back from the history to the menu that opened it
		private void ReturnFromHistory()
		{
			if (IsCheckingIn)
			{
				ShowScreen(_checkInMenuScreen, _checkInHistoryButton);
			}
			else
			{
				ShowScreen(_pauseScreen, _historyButton);
			}
		}

		// A few times a second: what the scanner sees and what to do
		private void RefreshCheckIn()
		{
			float now = Time.unscaledTime;
			if (now < _nextCheckInRefresh || _screen != _checkInScreen)
			{
				return;
			}

			_nextCheckInRefresh = now + CheckInRefreshSeconds;
			Texture preview = _ticketScanner != null && _ticketScanner.IsScanning ? _ticketScanner.Preview : null;
			ShowCheckInPreview(preview);
			if (now < _otherCodeUntil)
			{
				_checkInStatus.text = OtherCodeMessage;
			}
			else if (_ticketScanner == null)
			{
				_checkInStatus.text = TypeInstruction;
			}
			else
			{
				_checkInStatus.text = preview != null ? ScanInstruction : _ticketScanner.Status;
			}
		}

		// The camera image at its own shape, as large as fits; nothing when there is none
		private void ShowCheckInPreview(Texture texture)
		{
			if (texture == _shownPreview)
			{
				return;
			}

			_shownPreview = texture;
			_checkInPreview.texture = texture;
			_checkInPreview.enabled = texture != null;
			if (texture == null || texture.width <= 0 || texture.height <= 0)
			{
				return;
			}

			float scale = Mathf.Min(_checkInPreviewMaxSize.x / texture.width, _checkInPreviewMaxSize.y / texture.height);
			_checkInPreview.rectTransform.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
		}

		private void SetScanning(bool isScanning)
		{
			if (_ticketScanner != null)
			{
				_ticketScanner.IsScanning = isScanning;
			}
		}

		// The number pad starts from the current number, hover on 1
		private void ShowNumberPad()
		{
			_typedNumber = _participantId;
			ShowTypedNumber();
			ShowScreen(_numberPadScreen, _digitButtons[1]);
		}

		private void ShowTypedNumber()
		{
			_numberPadEntry.text = _typedNumber.Length > 0 ? _typedNumber : NoNumber;
		}

		private bool TryGetDigit(AdminMenuButton button, out int digit)
		{
			digit = Array.IndexOf(_digitButtons, button);
			return digit >= 0;
		}

		private void TypeDigit(int digit)
		{
			if (_typedNumber.Length < MaxParticipantDigits)
			{
				_typedNumber += digit.ToString(CultureInfo.InvariantCulture);
				ShowTypedNumber();
			}
		}

		private void DeleteDigit()
		{
			if (_typedNumber.Length > 0)
			{
				_typedNumber = _typedNumber.Substring(0, _typedNumber.Length - 1);
				ShowTypedNumber();
			}
		}

		// OK: the typed number (without leading zeros, as tickets print it) becomes the participant number
		private void AcceptTypedNumber()
		{
			_participantId = _typedNumber.Length > 0
				? int.Parse(_typedNumber, NumberStyles.None, CultureInfo.InvariantCulture)
					.ToString(CultureInfo.InvariantCulture)
				: string.Empty;
			if (_participantId.Length > 0)
			{
				Debug.Log($"[Admin] Check-in: typed participant {_participantId}.", this);
			}

			ShowCheckIn(_participantId.Length > 0 ? _startSessionButton : _typeNumberButton);
		}

		// Closes the menu, switches the scanner off and hands the participant to the session
		private void StartSession()
		{
			if (_participantId.Length == 0)
			{
				return;
			}

			CloseCheckIn();
			if (_sessionControl == null)
			{
				Debug.LogWarning("[Admin] Start session needs a Session Control in the scene.", this);
				return;
			}

			_sessionControl.StartSession(_participantId);
		}

		private void CloseCheckIn()
		{
			IsCheckingIn = false;
			IsOpen = false;
			_placement.IsFollowing = false;
			SetScanning(false);
		}

		private void EndSession()
		{
			if (_sessionControl == null)
			{
				Debug.LogWarning("[Admin] End session needs a Session Control in the scene.", this);
				ShowScreen(_pauseScreen, _endButton);
				return;
			}

			Close(MenuHand.None);
			_sessionControl.EndSession();
		}

		private IReadOnlyList<AdminMenuButton> GetScreenButtons()
		{
			return _screen == _historyScreen ? _history.Buttons : _screen.Buttons;
		}

		private void SetHovered(AdminMenuButton button, MenuHand hand)
		{
			if (button == _hovered)
			{
				return;
			}

			if (_hovered != null)
			{
				_hovered.IsHovered = false;
			}

			_hovered = button;
			if (button == null)
			{
				return;
			}

			button.IsHovered = true;
			if (_screen == _historyScreen)
			{
				_history.ScrollToShow(button, isInstant: false);
			}

			Pulse(hand, isClick: false);
		}

		// Between the screen's shown buttons; hidden ones (the test save button when it's off) are skipped
		private void MoveHover(MenuDirection direction, MenuHand hand)
		{
			_shownButtons.Clear();
			foreach (AdminMenuButton button in GetScreenButtons())
			{
				if (button.IsShown)
				{
					_shownButtons.Add(button);
				}
			}

			int from = -1;
			_centers.Clear();
			for (int index = 0; index < _shownButtons.Count; index++)
			{
				RectTransform body = _shownButtons[index].Body;
				Vector3 center = _canvas.InverseTransformPoint(body.TransformPoint(body.rect.center));
				_centers.Add(center);
				if (_shownButtons[index] == _hovered)
				{
					from = index;
				}
			}

			if (from < 0)
			{
				SetHovered(_shownButtons.Count > 0 ? _shownButtons[0] : null, hand);
				return;
			}

			int to = MenuNavigationUtil.FindNeighbor(_centers, from, direction);
			if (to >= 0)
			{
				SetHovered(_shownButtons[to], hand);
			}
		}

		// Shows or hides the test save button; without it the Paused panel is one button shorter (it stays centred,
		// and the handle under it follows)
		private void ApplyTestSaveVisibility()
		{
			_testSaveButton.SetShown(_isTestSaveShown);
			float height = _isTestSaveShown ? _pausePanelFullHeight : _pausePanelFullHeight - _pauseButtonPitch;
			_pausePanel.sizeDelta = new Vector2(_pausePanel.sizeDelta.x, height);
			if (!_isTestSaveShown && _hovered == _testSaveButton)
			{
				SetHovered(_resumeButton, MenuHand.None);
			}
		}

		// Adds a test row to the session history (Id 0, group A, neutral, now) and says so on the button for a moment
		private void SaveTestSession()
		{
			DateTime now = DateTime.Now;
			(string Column, string Value)[] cells =
			{
				(SessionColumns.Id, TestSessionId),
				(SessionColumns.Group, TestSessionGroup),
				(SessionColumns.FormChoice, TestSessionFormChoice),
				(SessionColumns.Date, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
				(SessionColumns.Time, now.ToString("HH:mm", CultureInfo.InvariantCulture)),
			};

			SessionHistory sessions = CreateHistory();
			try
			{
				List<string> missingColumns = sessions.Append(cells);
				if (missingColumns.Count > 0)
				{
					Debug.LogWarning($"[Admin] {sessions.SessionsPath} has no {string.Join(", ", missingColumns)} "
						+ "column, so the test row leaves those out.", this);
				}

				string time = now.ToString("HH:mm", CultureInfo.InvariantCulture);
				_testSaveButton.ShowTemporaryLabel($"Saved 000 at {time}", SavedLabelSeconds);
				Debug.Log($"[Admin] Test session saved to {sessions.SessionsPath}.", this);
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Debug.LogError($"[Admin] Couldn't save the test session to {sessions.SessionsPath}: "
					+ exception.Message, this);
				_testSaveButton.ShowTemporaryLabel(SaveFailedMessage, SavedLabelSeconds);
			}
		}

		private void Click(MenuHand hand)
		{
			AdminMenuButton button = _hovered;
			if (button == null)
			{
				return;
			}

			button.PlayPress();
			Pulse(hand, isClick: true);
			if (button == _resumeButton)
			{
				Close(MenuHand.None);
			}
			else if (button == _endButton)
			{
				ShowScreen(_endScreen, _endCancelButton);
			}
			else if (button == _endCancelButton)
			{
				ShowScreen(_pauseScreen, _endButton);
			}
			else if (button == _endConfirmButton)
			{
				EndSession();
			}
			else if (button == _typeNumberButton)
			{
				ShowNumberPad();
			}
			else if (button == _startSessionButton)
			{
				StartSession();
			}
			else if (TryGetDigit(button, out int digit))
			{
				TypeDigit(digit);
			}
			else if (button == _deleteDigitButton)
			{
				DeleteDigit();
			}
			else if (button == _numberPadOkButton)
			{
				AcceptTypedNumber();
			}
			else if (button == _historyButton)
			{
				ShowHistory(-1);
			}
			else if (button == _locationButton)
			{
				ResetPlayerLocation();
			}
			else if (button == _testSaveButton)
			{
				SaveTestSession();
			}
			else if (button == _history.BackButton)
			{
				ReturnFromHistory();
			}
			else if (button == _checkInResumeButton)
			{
				ReturnToCheckIn();
			}
			else if (button == _checkInHistoryButton)
			{
				ShowHistory(-1);
			}
			else if (button == _deleteCancelButton)
			{
				ReturnToHistory();
			}
			else if (button == _deleteConfirmButton)
			{
				ConfirmDelete();
			}
			else if (_history.TryGetRecord(button, out SessionRecord record))
			{
				AskDelete(record);
			}
		}

		// A / B / X / Y: history back to the menu that opened it, dialogs go back. On Paused and the check-in menu it
		// does nothing: resuming stays a deliberate click.
		private void Back(MenuHand hand)
		{
			if (_screen == _historyScreen)
			{
				ReturnFromHistory();
			}
			else if (_screen == _endScreen)
			{
				ShowScreen(_pauseScreen, _endButton);
			}
			else if (_screen == _numberPadScreen)
			{
				ShowCheckIn(_typeNumberButton);
			}
			else if (_screen == _deleteScreen)
			{
				ReturnToHistory();
			}
			else
			{
				return;
			}

			Pulse(hand, isClick: true);
		}

		// Reads Sessions.csv fresh. hoveredRow: the row whose Delete button gets the hover, or -1 for Go back.
		private void ShowHistory(int hoveredRow)
		{
			List<SessionRecord> records;
			string emptyMessage = NoSessionsMessage;
			SessionHistory sessions = CreateHistory();
			try
			{
				records = sessions.Load();
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Debug.LogError($"[Admin] Couldn't read {sessions.SessionsPath}: {exception.Message}", this);
				records = new List<SessionRecord>();
				emptyMessage = UnreadableMessage;
			}

			_history.ShowRecords(records, emptyMessage);
			ShowScreen(_historyScreen, _history.GetDeleteButton(hoveredRow));
		}

		private void AskDelete(SessionRecord record)
		{
			_pendingDelete = record;
			string name = ColorUtility.ToHtmlStringRGB(_detailsNameColor);
			_deleteDetails.text = $"<color=#{name}>Id</color>  <b>{record.DisplayId}</b>\n"
				+ $"<color=#{name}>Date</color>  {record.GetDisplay(SessionColumns.Date)}\n"
				+ $"<color=#{name}>Time</color>  {record.GetDisplay(SessionColumns.Time)}";
			ShowScreen(_deleteScreen, _deleteCancelButton);
		}

		// Back from the delete dialog, the hover on the row it was about
		private void ReturnToHistory()
		{
			int row = _pendingDelete != null ? _history.IndexOf(_pendingDelete) : -1;
			_pendingDelete = null;
			ShowScreen(_historyScreen, _history.GetDeleteButton(row));
		}

		// Moves the row to Deleted Sessions.csv; the hover goes to the row that takes its place
		private void ConfirmDelete()
		{
			SessionRecord record = _pendingDelete;
			_pendingDelete = null;
			int row = _history.IndexOf(record);
			SessionHistory sessions = CreateHistory();
			try
			{
				if (sessions.Delete(record, DateTime.Now))
				{
					Debug.Log($"[Admin] Session {record.DisplayId} moved to {sessions.DeletedPath}.", this);
				}
				else
				{
					Debug.LogWarning($"[Admin] Session {record.DisplayId} wasn't in {sessions.SessionsPath} any more.",
						this);
				}
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Debug.LogError($"[Admin] Couldn't delete session {record.DisplayId}: {exception.Message}", this);
			}

			ShowHistory(row);
		}

		// The menu stays open and moves in front of the head's new place
		private void ResetPlayerLocation()
		{
			if (_sessionControl == null)
			{
				Debug.LogWarning("[Admin] Reset player location needs a Session Control in the scene.", this);
				return;
			}

			if (_sessionControl.ResetPlayerLocation())
			{
				_placement.PlaceInFront();
			}
		}

		private SessionHistory CreateHistory()
		{
			return SessionHistory.FromSettings(_settings, _sessionsFolderOverride);
		}

		private void Pulse(MenuHand hand, bool isClick)
		{
			if (isClick)
			{
				ControllerHapticsUtil.Pulse(hand, _settings.ClickHapticAmplitude, _settings.ClickHapticSeconds);
			}
			else
			{
				ControllerHapticsUtil.Pulse(hand, _settings.HoverHapticAmplitude, _settings.HoverHapticSeconds);
			}
		}

		#endregion
	}
}
