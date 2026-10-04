using System;
using System.Collections.Generic;
using System.IO;
using DroneDDW.Admin.Sessions;
using TMPro;
using UnityEngine;

namespace DroneDDW.Admin
{
	// The admin menu from the Figma mockups, for whoever runs the session. The left controller's menu button opens it
	// and pauses the session; either thumbstick moves the pale blue hover, trigger, grip or A / X clicks, B or Y goes back.
	// Screens:
	// - Paused: Resume (closes the menu), Reset session, View session history, Reset player location, Change player
	//   language (no function yet)
	// - Session history: the rows of Sessions.csv, each with Delete
	// - Reset / delete dialogs, which open on Cancel so a double click can't confirm them
	// The menu button closes the menu and resumes from any screen. Everything runs on unscaled time, since the
	// session's time stands still while the menu is open.
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

		[Header("Canvas")]
		[Tooltip("The world-space canvas, laid out in the mockups' pixels and scaled by the settings")]
		[SerializeField] private RectTransform _canvas;

		[SerializeField] private CanvasGroup _canvasGroup;

		[Header("Screens")]
		[SerializeField] private AdminMenuScreen _pauseScreen;
		[SerializeField] private AdminMenuScreen _historyScreen;
		[SerializeField] private SessionHistoryScreen _history;
		[SerializeField] private AdminMenuScreen _resetScreen;
		[SerializeField] private AdminMenuScreen _deleteScreen;

		[Header("Paused")]
		[SerializeField] private AdminMenuButton _resumeButton;
		[SerializeField] private AdminMenuButton _resetButton;
		[SerializeField] private AdminMenuButton _historyButton;
		[SerializeField] private AdminMenuButton _locationButton;
		[SerializeField] private AdminMenuButton _languageButton;

		[Header("Dialogs")]
		[SerializeField] private AdminMenuButton _resetCancelButton;
		[SerializeField] private AdminMenuButton _resetConfirmButton;
		[SerializeField] private AdminMenuButton _deleteCancelButton;
		[SerializeField] private AdminMenuButton _deleteConfirmButton;

		[Tooltip("The delete dialog's Id, Date and Time lines")]
		[SerializeField] private TMP_Text _deleteDetails;

		[Tooltip("The delete dialog's Id, Date and Time names")]
		[SerializeField] private Color _detailsNameColor;

		#endregion

		#region Fields

		private const float MetersPerMillimeter = 0.001f;
		private const string NoSessionsMessage = "No sessions yet";
		private const string UnreadableMessage = "Couldn't read the session history (see the log)";

		private readonly List<Vector2> _centers = new();
		private AdminMenuScreen _screen;
		private AdminMenuButton _hovered;
		private SessionRecord _pendingDelete;
		private string _sessionsFolderOverride;

		#endregion

		#region Properties

		internal bool IsOpen { get; private set; }

		// What the Play mode check looks at
		internal AdminMenuScreen CurrentScreen => _screen;
		internal AdminMenuButton Hovered => _hovered;
		internal SessionHistoryScreen History => _history;
		internal PauseDimmer Dimmer => _dimmer;
		internal AdminMenuScreen PauseScreen => _pauseScreen;
		internal AdminMenuScreen HistoryScreen => _historyScreen;
		internal AdminMenuScreen ResetScreen => _resetScreen;
		internal AdminMenuScreen DeleteScreen => _deleteScreen;
		internal AdminMenuButton ResumeButton => _resumeButton;
		internal AdminMenuButton ResetButton => _resetButton;
		internal AdminMenuButton HistoryButton => _historyButton;
		internal AdminMenuButton LocationButton => _locationButton;
		internal AdminMenuButton LanguageButton => _languageButton;
		internal AdminMenuButton ResetCancelButton => _resetCancelButton;
		internal AdminMenuButton ResetConfirmButton => _resetConfirmButton;
		internal AdminMenuButton DeleteCancelButton => _deleteCancelButton;
		internal AdminMenuButton DeleteConfirmButton => _deleteConfirmButton;
		internal string DeleteDetailsText => _deleteDetails.text;

		#endregion

		#region Setup

		private void Awake()
		{
			_pauseScreen.Hide();
			_historyScreen.Hide();
			_resetScreen.Hide();
			_deleteScreen.Hide();
			_canvasGroup.alpha = 0f;
			_canvas.gameObject.SetActive(false);
			if (_sessionControl == null)
			{
				Debug.LogWarning("[Admin] The admin menu has no Session Control: it can't pause, reset or recentre.",
					this);
			}

			Debug.Log($"[Admin] Session history file: {CreateHistory().SessionsPath}", this);
		}

		#endregion

		#region Update

		private void Update()
		{
			ApplyScale();
			FadeCanvas();

			MenuCommand command = _input.Read(Time.unscaledTime, _settings);
			if (!IsOpen)
			{
				if (command.Kind == MenuCommandKind.Toggle)
				{
					Open(command.Hand);
				}

				return;
			}

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
			SessionHistoryScreen history, AdminMenuScreen resetScreen, AdminMenuScreen deleteScreen,
			TMP_Text deleteDetails)
		{
			_pauseScreen = pauseScreen;
			_historyScreen = historyScreen;
			_history = history;
			_resetScreen = resetScreen;
			_deleteScreen = deleteScreen;
			_deleteDetails = deleteDetails;
		}

		internal void ConfigureButtons(AdminMenuButton resumeButton, AdminMenuButton resetButton,
			AdminMenuButton historyButton, AdminMenuButton locationButton, AdminMenuButton languageButton,
			AdminMenuButton resetCancelButton, AdminMenuButton resetConfirmButton, AdminMenuButton deleteCancelButton,
			AdminMenuButton deleteConfirmButton)
		{
			_resumeButton = resumeButton;
			_resetButton = resetButton;
			_historyButton = historyButton;
			_locationButton = locationButton;
			_languageButton = languageButton;
			_resetCancelButton = resetCancelButton;
			_resetConfirmButton = resetConfirmButton;
			_deleteCancelButton = deleteCancelButton;
			_deleteConfirmButton = deleteConfirmButton;
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
			IsOpen = true;
			_input.ClearStick();
			if (_sessionControl != null)
			{
				_sessionControl.Pause();
			}

			_dimmer.IsDimmed = true;
			_canvas.gameObject.SetActive(true);
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

		private void MoveHover(MenuDirection direction, MenuHand hand)
		{
			IReadOnlyList<AdminMenuButton> buttons = GetScreenButtons();
			int from = -1;
			_centers.Clear();
			for (int index = 0; index < buttons.Count; index++)
			{
				RectTransform body = buttons[index].Body;
				Vector3 center = _canvas.InverseTransformPoint(body.TransformPoint(body.rect.center));
				_centers.Add(center);
				if (buttons[index] == _hovered)
				{
					from = index;
				}
			}

			if (from < 0)
			{
				SetHovered(buttons.Count > 0 ? buttons[0] : null, hand);
				return;
			}

			int to = MenuNavigationUtil.FindNeighbor(_centers, from, direction);
			if (to >= 0)
			{
				SetHovered(buttons[to], hand);
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
			else if (button == _resetButton)
			{
				ShowScreen(_resetScreen, _resetCancelButton);
			}
			else if (button == _historyButton)
			{
				ShowHistory(-1);
			}
			else if (button == _locationButton)
			{
				ResetPlayerLocation();
			}
			else if (button == _languageButton)
			{
				Debug.Log("[Admin] Change player language doesn't do anything yet.", this);
			}
			else if (button == _history.BackButton)
			{
				ShowScreen(_pauseScreen, _historyButton);
			}
			else if (button == _resetCancelButton)
			{
				ShowScreen(_pauseScreen, _resetButton);
			}
			else if (button == _resetConfirmButton)
			{
				ResetSession();
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

		// B / Y: history back to Paused, dialogs cancel. On Paused it does nothing: resuming stays a deliberate click.
		private void Back(MenuHand hand)
		{
			if (_screen == _historyScreen)
			{
				ShowScreen(_pauseScreen, _historyButton);
			}
			else if (_screen == _resetScreen)
			{
				ShowScreen(_pauseScreen, _resetButton);
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

		private void ResetSession()
		{
			if (_sessionControl == null)
			{
				Debug.LogWarning("[Admin] Reset session needs a Session Control in the scene.", this);
				ShowScreen(_pauseScreen, _resetButton);
				return;
			}

			Close(MenuHand.None);
			_sessionControl.ResetSession();
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
			string folder = _sessionsFolderOverride
				?? Path.Combine(Application.persistentDataPath, _settings.SessionsFolder);
			return new SessionHistory(folder, _settings.SessionsFileName, _settings.DeletedSessionsFileName,
				_settings.Separator);
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
