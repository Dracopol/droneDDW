using UnityEngine;

namespace DroneDDW.Admin
{
	// How the admin menu sits, moves, answers the controllers and looks, and where the session history lives. The menu
	// reads it every frame, so changes apply live in Play mode and are kept, like any asset.
	[CreateAssetMenu(fileName = "Admin Menu Settings", menuName = "DroneDDW/Admin/Admin Menu Settings")]
	internal sealed class AdminMenuSettings : ScriptableObject
	{
		#region Serialized fields

		[Header("Placement")]
		[Tooltip("How far in front of the head the menu opens, in metres")]
		[Range(0.4f, 2f)]
		[SerializeField] private float _distanceMeters = 0.85f;

		[Tooltip("How far below eye level the menu's centre sits, in metres")]
		[Range(-0.3f, 0.5f)]
		[SerializeField] private float _dropMeters = 0.08f;

		[Tooltip("The menu stays locked in front of the head; this smooths its following, in seconds (0 = rigid). A "
			+ "little keeps it from shaking with every small head movement.")]
		[Range(0f, 0.3f)]
		[SerializeField] private float _lockSmoothSeconds = 0.05f;

		[Tooltip("The menu is laid out in the mockups' pixels; this sets their size in the world, in millimetres")]
		[Range(0.2f, 1.5f)]
		[SerializeField] private float _millimetersPerPixel = 0.5f;

		[Header("Thumbsticks")]
		[Tooltip("How far a stick has to be pushed to move the hover (0 to 1)")]
		[Range(0.1f, 1f)]
		[SerializeField] private float _stickPressThreshold = 0.6f;

		[Tooltip("How far back the stick has to come before the next push counts (0 to 1)")]
		[Range(0f, 1f)]
		[SerializeField] private float _stickReleaseThreshold = 0.35f;

		[Tooltip("Holding the stick starts repeating after this long, in seconds")]
		[Min(0.05f)]
		[SerializeField] private float _repeatDelaySeconds = 0.4f;

		[Tooltip("Time between repeats while the stick is held, in seconds")]
		[Min(0.03f)]
		[SerializeField] private float _repeatIntervalSeconds = 0.13f;

		[Header("Animation")]
		[Tooltip("How long a screen or the whole menu takes to fade in, in seconds")]
		[Min(0.01f)]
		[SerializeField] private float _fadeSeconds = 0.15f;

		[Tooltip("How long a button takes to light up or go dark when the hover moves, in seconds")]
		[Min(0.01f)]
		[SerializeField] private float _hoverSeconds = 0.08f;

		[Tooltip("How much bigger the hovered button gets")]
		[Range(1f, 1.1f)]
		[SerializeField] private float _hoverScale = 1.015f;

		[Tooltip("How far a click squeezes the button")]
		[Range(0.8f, 1f)]
		[SerializeField] private float _pressScale = 0.96f;

		[Tooltip("How long a click's squeeze lasts, in seconds")]
		[Min(0.01f)]
		[SerializeField] private float _pressSeconds = 0.14f;

		[Tooltip("How long the history list takes to scroll to the hovered row, in seconds")]
		[Min(0.01f)]
		[SerializeField] private float _scrollSmoothSeconds = 0.08f;

		[Header("Dimming")]
		[Tooltip("How dark the world behind the menu gets while the session is paused (0 = off, 1 = black)")]
		[Range(0f, 1f)]
		[SerializeField] private float _dimOpacity = 0.6f;

		[Tooltip("How long the dimming takes to fade in or out, in seconds")]
		[Min(0.01f)]
		[SerializeField] private float _dimSeconds = 0.25f;

		[Header("Haptics")]
		[Tooltip("Buzz strength when the hover moves (0 = off, 1 = strongest)")]
		[Range(0f, 1f)]
		[SerializeField] private float _hoverHapticAmplitude = 0.15f;

		[Min(0f)]
		[SerializeField] private float _hoverHapticSeconds = 0.02f;

		[Tooltip("Buzz strength on a click (0 = off, 1 = strongest)")]
		[Range(0f, 1f)]
		[SerializeField] private float _clickHapticAmplitude = 0.4f;

		[Min(0f)]
		[SerializeField] private float _clickHapticSeconds = 0.04f;

		[Header("Session history")]
		[Tooltip("Folder under the app's data folder (on the Quest: Android/data/<package>/files) holding the files")]
		[SerializeField] private string _sessionsFolder = "Sessions";

		[SerializeField] private string _sessionsFileName = "Sessions.csv";

		[Tooltip("Deleted sessions are moved here, with the time they were deleted, so a mistake can be undone")]
		[SerializeField] private string _deletedSessionsFileName = "Deleted Sessions.csv";

		[Tooltip("Between columns in new files. ';' opens in columns in Excel with Dutch regional settings, ',' with "
			+ "US/UK ones. Existing files keep the separator they were written with.")]
		[SerializeField] private char _separator = ';';

		[Header("Button colours")]
		[SerializeField] private Color _buttonFillColor = new Color32(0x1E, 0x2A, 0x3A, 0xFF);
		[SerializeField] private Color _buttonBorderColor = new Color32(0x34, 0x42, 0x55, 0xFF);
		[SerializeField] private Color _buttonTextColor = new Color32(0xF2, 0xF6, 0xFC, 0xFF);
		[SerializeField] private Color _buttonIconColor = new Color32(0xB9, 0xD6, 0xFF, 0xFF);

		[Tooltip("The rows' Delete buttons when they aren't hovered")]
		[SerializeField] private Color _tonalFillColor = new Color32(0x24, 0x3B, 0x56, 0xFF);

		[SerializeField] private Color _tonalTextColor = new Color32(0xB9, 0xD6, 0xFF, 0xFF);

		[Tooltip("The hovered button's fill and border")]
		[SerializeField] private Color _hoverFillColor = new Color32(0xB9, 0xD6, 0xFF, 0xFF);

		[Tooltip("The hovered button's text and icon")]
		[SerializeField] private Color _hoverTextColor = new Color32(0x12, 0x26, 0x3F, 0xFF);

		[Tooltip("The background of the row whose Delete button is hovered")]
		[SerializeField] private Color _hoverRowColor = new Color32(0x24, 0x31, 0x44, 0xFF);

		#endregion

		#region Properties

		internal float DistanceMeters => _distanceMeters;
		internal float DropMeters => _dropMeters;
		internal float LockSmoothSeconds => _lockSmoothSeconds;
		internal float MillimetersPerPixel => _millimetersPerPixel;

		internal float StickPressThreshold => _stickPressThreshold;

		// Never above the press threshold, or a held stick would release and press again every frame
		internal float StickReleaseThreshold => Mathf.Min(_stickReleaseThreshold, _stickPressThreshold);

		internal float RepeatDelaySeconds => _repeatDelaySeconds;
		internal float RepeatIntervalSeconds => _repeatIntervalSeconds;

		internal float FadeSeconds => _fadeSeconds;
		internal float HoverSeconds => _hoverSeconds;
		internal float HoverScale => _hoverScale;
		internal float PressScale => _pressScale;
		internal float PressSeconds => _pressSeconds;
		internal float ScrollSmoothSeconds => _scrollSmoothSeconds;

		internal float DimOpacity => _dimOpacity;
		internal float DimSeconds => _dimSeconds;

		internal float HoverHapticAmplitude => _hoverHapticAmplitude;
		internal float HoverHapticSeconds => _hoverHapticSeconds;
		internal float ClickHapticAmplitude => _clickHapticAmplitude;
		internal float ClickHapticSeconds => _clickHapticSeconds;

		internal string SessionsFolder => _sessionsFolder;
		internal string SessionsFileName => _sessionsFileName;
		internal string DeletedSessionsFileName => _deletedSessionsFileName;
		internal char Separator => _separator;

		internal Color ButtonFillColor => _buttonFillColor;
		internal Color ButtonBorderColor => _buttonBorderColor;
		internal Color ButtonTextColor => _buttonTextColor;
		internal Color ButtonIconColor => _buttonIconColor;
		internal Color TonalFillColor => _tonalFillColor;
		internal Color TonalTextColor => _tonalTextColor;
		internal Color HoverFillColor => _hoverFillColor;
		internal Color HoverTextColor => _hoverTextColor;
		internal Color HoverRowColor => _hoverRowColor;

		#endregion
	}
}
