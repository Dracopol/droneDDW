using System.Globalization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DroneDDW.Admin.Editor
{
	// Builds Content/Prefabs/Admin/Admin Menu.prefab from the Figma mockups (1600 x 1120 frames): every position and
	// size below is in the mockups' pixels, measured from the PNGs, and the canvas is scaled to millimetres by the
	// settings. Positions inside a panel are from its top-left corner; text is placed by the middle of its capitals.
	// The check-in, number pad and end session screens came later, without mockups, in the same style. The prefab's
	// scene references (head, session control, ticket scanner) are set where it is placed, e.g. by the scene builders.
	internal sealed class AdminMenuPrefabBuilder
	{
		#region Fields

		internal const string PrefabPath = "Assets/Content/Prefabs/Admin/Admin Menu.prefab";

		private const float CanvasWidth = 1600f;
		private const float CanvasHeight = 1120f;
		private const float Padding = 48f;
		private const float BorderWidth = 2f;
		private const float PanelRadius = 32f;
		private const float ButtonRadius = 14f;

		// The pill under each panel: 29 px below it
		private const float HandleGap = 29f;
		private const float HandleWidth = 88f;
		private const float HandleHeight = 5f;

		private const float TitleSize = 40f;

		// Inter's line height is 1.21 em; the mockups' two-line titles are 46 px apart (1.15 em)
		private const float TitleLineSpacing = -6f;

		// Paused
		private const float PauseWidth = 640f;
		// The mockup's panel with its five buttons: Resume, End session, history, location and test save
		private const float PauseHeight = 742f;
		private const float PauseTitleCapMiddle = 130.5f;
		private const float PauseButtonsTop = 189f;
		private const float PauseButtonPitch = 104f;
		private const float MenuButtonHeight = 88f;
		private const float MenuIconCenter = 42f;
		private const float MenuIconSize = 26f;
		private const float MenuLabelLeft = 76f;
		private const float ButtonLabelSize = 26f;

		// Session history
		private const float HistoryWidth = 1376f;
		private const float HistoryHeight = 962f;
		private const float BackLeft = 40f;
		private const float BackTop = 40f;
		private const float BackWidth = 154f;
		private const float BackHeight = 56f;
		private const float BackIconCenter = 34f;
		private const float BackIconSize = 28f;
		private const float BackLabelLeft = 61f;
		private const float SmallLabelSize = 19f;
		private const float HistoryTitleLeft = 222f;
		private const float HistoryTitleCapMiddle = 67.5f;
		private const float TableLeft = 40f;
		private const float TableTop = 124f;
		private const float TableWidth = 1296f;
		private const float TableHeight = 748f;
		private const float RowHeight = 68f;
		private const float RowLineHeight = 2f;
		private const float CellTextSize = 23f;
		private const float BadgeLeft = 122f;
		private const float BadgeWidth = 46f;
		private const float BadgeHeight = 36f;
		private const float BadgeRadius = 6f;
		private const float BadgeTextSize = 22f;
		private const float DeleteLeft = 1148f;
		private const float DeleteWidth = 120f;
		private const float DeleteHeight = 52f;
		private const float DeleteRadius = 12f;
		private const float FooterCapMiddle = 911.5f;

		// Column text starts, from the table's left edge (the mockup's First time participating column is gone)
		private const float IdColumn = 28f;
		private const float GroupColumn = 123f;
		private const float FormChoiceColumn = 247f;
		private const float DateColumn = 600f;
		private const float TimeColumn = 860f;
		private const float ActionColumn = 1148f;

		// Dialogs
		private const float DialogWidth = 760f;
		private const float ResetDialogHeight = 312f;
		private const float DeleteDialogHeight = 539f;
		private const float DialogButtonWidth = 324f;
		private const float DialogButtonGap = 16f;
		private const float ResetButtonsTop = 175f;
		private const float DeleteButtonsTop = 403f;
		private const float DetailsTop = 176f;
		private const float DetailsHeight = 191f;
		private const float DetailsInset = 24f;
		private const float CaptionCapMiddle = 36f;
		private const float CaptionSize = 18f;
		private const float DetailsTextTop = 68f;
		private const float DetailsTextSize = 24f;

		// Inter's line height is 1.21 em; the mockup's detail lines are 34 px apart
		private const float DetailsLineSpacing = 21f;

		// Check-in: the camera image on the left, the participant number on the right, then what to do and the buttons
		private const float CheckInHeight = 642f;
		private const float CheckInTitleCapMiddle = 82f;
		private const float PreviewTop = 136f;
		private const float PreviewWidth = 360f;
		private const float PreviewHeight = 270f;
		private const float PreviewInset = 4f;
		private const float NumberColumnGap = 32f;
		private const float NumberCaptionCapMiddle = 160f;
		private const float NumberCapMiddle = 240f;
		private const float NumberSize = 96f;
		private const float GroupCapMiddle = 330f;
		private const float StatusTop = 430f;
		private const float StatusHeight = 60f;
		private const float StatusSize = 22f;
		private const float CheckInButtonsTop = 506f;

		// Number pad: the typed number in a box, then 3 x 4 keys
		private const float KeyWidth = 128f;
		private const float KeyHeight = 88f;
		private const float KeyGap = 16f;
		private const float PadContentWidth = 3f * KeyWidth + 2f * KeyGap;
		private const float PadWidth = PadContentWidth + 2f * Padding;
		private const float PadTitleCapMiddle = 82f;
		private const float EntryTop = 122f;
		private const float EntryHeight = 96f;
		private const float EntrySize = 64f;
		private const float KeysTop = 250f;
		private const float PadHeight = KeysTop + 4f * KeyHeight + 3f * KeyGap + Padding;
		private const float DigitSize = 32f;

		private static readonly Color32 TextColor = new(0xF2, 0xF6, 0xFC, 0xFF);
		private static readonly Color32 MutedColor = new(0xAC, 0xB9, 0xCB, 0xFF);
		private static readonly Color32 AccentColor = new(0xB9, 0xD6, 0xFF, 0xFF);
		private static readonly Color32 SurfaceColor = new(0x1E, 0x2A, 0x3A, 0xFF);
		private static readonly Color32 EvenRowColor = new(0x19, 0x24, 0x32, 0xFF);
		private static readonly Color32 RowLineColor = new(0x21, 0x2D, 0x3D, 0xFF);
		private static readonly Color32 BadgeColor = new(0x24, 0x3B, 0x56, 0xFF);
		private static readonly Color32 HandleColor = new(0x58, 0x68, 0x77, 0xFF);

		private readonly AdminMenuAssets _assets;

		#endregion

		#region Setup

		private AdminMenuPrefabBuilder(AdminMenuAssets assets)
		{
			_assets = assets;
		}

		#endregion

		#region Internal

		// Builds the menu in a preview scene, so the open scene isn't touched, and saves it as the prefab
		internal static void Build(AdminMenuAssets assets)
		{
			Scene preview = EditorSceneManager.NewPreviewScene();
			try
			{
				GameObject root = new AdminMenuPrefabBuilder(assets).CreateMenu();
				SceneManager.MoveGameObjectToScene(root, preview);
				AdminMenuAssetBuilder.EnsureFolder(PrefabPath.Substring(0, PrefabPath.LastIndexOf('/')));
				PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
			}
			finally
			{
				EditorSceneManager.ClosePreviewScene(preview);
			}
		}

		#endregion

		#region Private

		private GameObject CreateMenu()
		{
			GameObject root = new("Admin Menu");
			AdminMenuSettings settings = _assets.Settings;

			AdminMenuInput input = root.AddComponent<AdminMenuInput>();
			input.Configure(_assets.ToggleAction, _assets.NavigateAction, _assets.ClickAction, _assets.BackAction);
			AdminMenuPlacement placement = root.AddComponent<AdminMenuPlacement>();
			placement.Configure(settings, null);
			PauseDimmer dimmer = CreateDimmer(root.transform);

			RectTransform canvas = CreateCanvas(root.transform, out CanvasGroup canvasGroup);
			AdminMenu menu = root.AddComponent<AdminMenu>();
			menu.Configure(settings, input, placement, dimmer, canvas, canvasGroup, MutedColor);

			// Paused: built tall enough for the test save button; the menu shrinks it when the button is off
			AdminMenuScreen pauseScreen = CreateScreen(canvas, "Pause Screen", PauseWidth, PauseHeight,
				out RectTransform pausePanel);
			CreateText(pausePanel, "Title", "Session paused by admin", Padding, PauseTitleCapMiddle,
				PauseWidth - 2f * Padding, TitleSize, isSemiBold: false, TextColor, TextAlignmentOptions.CaplineLeft);
			AdminMenuButton resume = CreateMenuButton(pausePanel, "Resume Session", 0, _assets.PlayIcon);
			AdminMenuButton end = CreateMenuButton(pausePanel, "End Session", 1, _assets.EndIcon);
			AdminMenuButton history = CreateMenuButton(pausePanel, "View Session History", 2, _assets.HistoryIcon);
			AdminMenuButton location = CreateMenuButton(pausePanel, "Reset Player Location", 3, _assets.LocationIcon);
			AdminMenuButton testSave = CreateMenuButton(pausePanel, "Test Save Current Session", 4, _assets.SaveIcon);
			pauseScreen.Configure(settings, pauseScreen.GetComponent<CanvasGroup>(),
				new[] { resume, end, history, location, testSave });

			// Session history
			AdminMenuScreen historyScreen = CreateScreen(canvas, "History Screen", HistoryWidth, HistoryHeight,
				out RectTransform historyPanel);
			SessionHistoryScreen historyTable = CreateHistory(historyScreen, historyPanel, out AdminMenuButton back);
			historyScreen.Configure(settings, historyScreen.GetComponent<CanvasGroup>(), new[] { back });

			// Dialogs
			AdminMenuScreen deleteScreen = CreateDialog(canvas, "Delete Session Screen", DeleteDialogHeight,
				"Are you sure you want to delete this session?", DeleteButtonsTop, "Delete session",
				out AdminMenuButton deleteCancel, out AdminMenuButton deleteConfirm, out RectTransform deletePanel);
			TMP_Text details = CreateDetails(deletePanel);
			AdminMenuScreen endScreen = CreateDialog(canvas, "End Session Screen", ResetDialogHeight,
				"Are you sure you want to end this session?", ResetButtonsTop, "End session",
				out AdminMenuButton endCancel, out AdminMenuButton endConfirm, out _);

			menu.ConfigureScreens(pauseScreen, historyScreen, historyTable, deleteScreen, details);
			menu.ConfigureButtons(resume, history, location, testSave, deleteCancel, deleteConfirm);
			menu.ConfigurePausePanel(pausePanel, PauseButtonPitch);
			menu.ConfigureEndSession(end, endScreen, endCancel, endConfirm);
			CreateCheckIn(canvas, menu);
			CreateNumberPad(canvas, menu);
			return root;
		}

		// A quad that the dimmer moves under the headset camera when the scene starts
		private PauseDimmer CreateDimmer(Transform parent)
		{
			GameObject dimmerObject = new("Pause Dimmer");
			dimmerObject.transform.SetParent(parent, false);
			MeshFilter filter = dimmerObject.AddComponent<MeshFilter>();
			filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
			MeshRenderer dimmerRenderer = dimmerObject.AddComponent<MeshRenderer>();
			dimmerRenderer.sharedMaterial = _assets.Dimmer;
			dimmerRenderer.shadowCastingMode = ShadowCastingMode.Off;
			dimmerRenderer.receiveShadows = false;
			dimmerRenderer.lightProbeUsage = LightProbeUsage.Off;
			dimmerRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			dimmerRenderer.enabled = false;
			PauseDimmer dimmer = dimmerObject.AddComponent<PauseDimmer>();
			dimmer.Configure(_assets.Settings, dimmerRenderer, null);
			return dimmer;
		}

		private static RectTransform CreateCanvas(Transform parent, out CanvasGroup canvasGroup)
		{
			GameObject canvasObject = new("Canvas", typeof(RectTransform));
			RectTransform canvas = (RectTransform)canvasObject.transform;
			canvas.SetParent(parent, false);
			Canvas canvasComponent = canvasObject.AddComponent<Canvas>();
			canvasComponent.renderMode = RenderMode.WorldSpace;
			canvasComponent.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
				| AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
			canvas.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
			canvas.localPosition = Vector3.zero;
			canvas.localRotation = Quaternion.identity;
			canvas.localScale = Vector3.one * 0.0005f;
			canvasGroup = canvasObject.AddComponent<CanvasGroup>();
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;
			return canvas;
		}

		// A screen filling the canvas, with its panel centred and the handle under the panel (anchored to the panel's
		// bottom edge, so it follows when the panel changes height)
		private AdminMenuScreen CreateScreen(RectTransform canvas, string name, float panelWidth, float panelHeight,
			out RectTransform panel)
		{
			RectTransform screen = CreateRect(name, canvas);
			screen.anchorMin = Vector2.zero;
			screen.anchorMax = Vector2.one;
			screen.sizeDelta = Vector2.zero;
			CanvasGroup canvasGroup = screen.gameObject.AddComponent<CanvasGroup>();
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;

			panel = CreateRect("Panel", screen);
			panel.sizeDelta = new Vector2(panelWidth, panelHeight);
			CreateImage("Border", panel, _assets.RoundedRect, AdminMenuAssetBuilder.BorderColor, PanelRadius);
			Image fill = CreateImage("Fill", panel, _assets.RoundedRect, AdminMenuAssetBuilder.PanelColor,
				PanelRadius - BorderWidth);
			Inset(fill.rectTransform, BorderWidth);

			RectTransform handle = CreateRect("Handle", panel);
			handle.anchorMin = new Vector2(0.5f, 0f);
			handle.anchorMax = new Vector2(0.5f, 0f);
			handle.pivot = new Vector2(0.5f, 1f);
			handle.sizeDelta = new Vector2(HandleWidth, HandleHeight);
			handle.anchoredPosition = new Vector2(0f, -HandleGap);
			Image handleImage = CreateImage("Pill", handle, _assets.RoundedRect, HandleColor, HandleHeight * 0.5f);
			handleImage.rectTransform.sizeDelta = Vector2.zero;

			return screen.gameObject.AddComponent<AdminMenuScreen>();
		}

		// One of the Paused screen's full-width buttons, from the top
		private AdminMenuButton CreateMenuButton(RectTransform panel, string label, int index, Sprite icon)
		{
			string text = char.ToUpperInvariant(label[0]) + label.Substring(1).ToLowerInvariant();
			RectTransform rect = CreateRect(label, panel);
			Place(rect, Padding, PauseButtonsTop + index * PauseButtonPitch, PauseWidth - 2f * Padding,
				MenuButtonHeight);
			return CreateButtonParts(rect, text, ButtonRadius, hasBorder: true, isTonal: false,
				isIconLikeText: false, icon, MenuIconCenter, MenuIconSize, MenuLabelLeft, ButtonLabelSize);
		}

		private SessionHistoryScreen CreateHistory(AdminMenuScreen screen, RectTransform panel,
			out AdminMenuButton back)
		{
			RectTransform backRect = CreateRect("Go Back", panel);
			Place(backRect, BackLeft, BackTop, BackWidth, BackHeight);
			back = CreateButtonParts(backRect, "Go back", ButtonRadius, hasBorder: true, isTonal: false,
				isIconLikeText: true, _assets.BackIcon, BackIconCenter, BackIconSize, BackLabelLeft, SmallLabelSize);

			CreateText(panel, "Title", "Session history", HistoryTitleLeft, HistoryTitleCapMiddle,
				HistoryWidth - HistoryTitleLeft - Padding, TitleSize, isSemiBold: false, TextColor,
				TextAlignmentOptions.CaplineLeft);

			// The table: plain body and header, the rows clipped by the viewport, the frame over them all
			RectTransform table = CreateRect("Table", panel);
			Place(table, TableLeft, TableTop, TableWidth, TableHeight);
			Image body = CreateImage("Body", table, null, AdminMenuAssetBuilder.PanelColor, 0f);
			body.rectTransform.sizeDelta = Vector2.zero;
			Image header = CreateImage("Header", table, null, SurfaceColor, 0f);
			Place(header.rectTransform, 0f, 0f, TableWidth, RowHeight);
			CreateHeaderLabel(table, "Id", IdColumn);
			CreateHeaderLabel(table, "Group", GroupColumn);
			CreateHeaderLabel(table, "Form choice", FormChoiceColumn);
			CreateHeaderLabel(table, "Date", DateColumn);
			CreateHeaderLabel(table, "Time", TimeColumn);
			CreateHeaderLabel(table, "Action", ActionColumn);

			RectTransform viewport = CreateRect("Viewport", table);
			Place(viewport, 0f, RowHeight, TableWidth, TableHeight - RowHeight);
			viewport.gameObject.AddComponent<RectMask2D>();
			RectTransform content = CreateRect("Content", viewport);
			content.anchorMin = new Vector2(0f, 1f);
			content.anchorMax = Vector2.one;
			content.pivot = new Vector2(0.5f, 1f);
			content.sizeDelta = new Vector2(0f, 0f);
			content.anchoredPosition = Vector2.zero;
			SessionRowView rowTemplate = CreateRowTemplate(content);

			TextMeshProUGUI emptyLabel = CreateText(viewport, "Empty Message", "No sessions yet", 0f,
				(TableHeight - RowHeight) * 0.5f, TableWidth, CellTextSize, isSemiBold: false, MutedColor,
				TextAlignmentOptions.Capline);

			Image frame = CreateImage("Frame", table, _assets.TableFrame, Color.white,
				AdminMenuAssetBuilder.TableCornerRadius);
			frame.rectTransform.sizeDelta = Vector2.zero;

			TextMeshProUGUI footer = CreateText(panel, "Footer", "0 sessions", TableLeft, FooterCapMiddle,
				TableWidth, SmallLabelSize, isSemiBold: false, MutedColor, TextAlignmentOptions.CaplineLeft);

			SessionHistoryScreen historyTable = screen.gameObject.AddComponent<SessionHistoryScreen>();
			historyTable.Configure(_assets.Settings, back, viewport, content, rowTemplate, emptyLabel, footer,
				RowHeight);
			return historyTable;
		}

		private void CreateHeaderLabel(RectTransform table, string text, float left)
		{
			CreateText(table, $"{text} Header", text, left, RowHeight * 0.5f, TableWidth - left, SmallLabelSize,
				isSemiBold: false, MutedColor, TextAlignmentOptions.CaplineLeft);
		}

		// Inactive; the history screen copies it for each session
		private SessionRowView CreateRowTemplate(RectTransform content)
		{
			RectTransform row = CreateRect("Row Template", content);
			row.anchorMin = new Vector2(0f, 1f);
			row.anchorMax = Vector2.one;
			row.pivot = new Vector2(0.5f, 1f);
			row.sizeDelta = new Vector2(0f, RowHeight);
			row.anchoredPosition = Vector2.zero;

			Image background = CreateImage("Background", row, null, AdminMenuAssetBuilder.PanelColor, 0f);
			background.rectTransform.sizeDelta = Vector2.zero;
			Image line = CreateImage("Line", row, null, RowLineColor, 0f);
			Place(line.rectTransform, 0f, 0f, TableWidth, RowLineHeight);

			float middle = RowHeight * 0.5f;
			TextMeshProUGUI id = CreateCell(row, "Id", IdColumn, GroupColumn - IdColumn, MutedColor);
			Image badge = CreateImage("Group Badge", row, _assets.RoundedRect, BadgeColor, BadgeRadius);
			Place(badge.rectTransform, BadgeLeft, middle - BadgeHeight * 0.5f, BadgeWidth, BadgeHeight);
			TextMeshProUGUI group = CreateText(row, "Group", "A", BadgeLeft, middle, BadgeWidth, BadgeTextSize,
				isSemiBold: true, AccentColor, TextAlignmentOptions.Capline);
			TextMeshProUGUI formChoice = CreateCell(row, "Form Choice", FormChoiceColumn,
				DateColumn - FormChoiceColumn, TextColor);
			TextMeshProUGUI date = CreateCell(row, "Date", DateColumn, TimeColumn - DateColumn, TextColor);
			TextMeshProUGUI time = CreateCell(row, "Time", TimeColumn, ActionColumn - TimeColumn, TextColor);

			RectTransform deleteRect = CreateRect("Delete", row);
			Place(deleteRect, DeleteLeft, middle - DeleteHeight * 0.5f, DeleteWidth, DeleteHeight);
			AdminMenuButton delete = CreateButtonParts(deleteRect, "Delete", DeleteRadius, hasBorder: false,
				isTonal: true, isIconLikeText: false, null, 0f, 0f, -1f, BadgeTextSize);

			SessionRowView view = row.gameObject.AddComponent<SessionRowView>();
			view.Configure(_assets.Settings, background, id, badge, group, formChoice, date, time, delete,
				AdminMenuAssetBuilder.PanelColor, EvenRowColor, AccentColor, MutedColor);
			row.gameObject.SetActive(false);
			return view;
		}

		private TextMeshProUGUI CreateCell(RectTransform row, string name, float left, float width, Color color)
		{
			return CreateText(row, name, SessionRecordPlaceholder(name), left, RowHeight * 0.5f, width - 8f,
				CellTextSize, isSemiBold: false, color, TextAlignmentOptions.CaplineLeft);
		}

		// What the template shows in the editor: the mockup's first row
		private static string SessionRecordPlaceholder(string column)
		{
			switch (column)
			{
				case "Id": return "001";
				case "Form Choice": return "Nature";
				case "Date": return "2026-03-12";
				case "Time": return "09:14";
				default: return string.Empty;
			}
		}

		// Check-in: the scanner's camera image, the participant number it read or that was typed, what to do, and
		// Type number / Start session (the menu shows Start session once there is a number)
		private void CreateCheckIn(RectTransform canvas, AdminMenu menu)
		{
			AdminMenuScreen screen = CreateScreen(canvas, "Check-in Screen", DialogWidth, CheckInHeight,
				out RectTransform panel);
			CreateText(panel, "Title", "New session", Padding, CheckInTitleCapMiddle, DialogWidth - 2f * Padding,
				TitleSize, isSemiBold: false, TextColor, TextAlignmentOptions.CaplineLeft);

			RectTransform previewBox = CreateBox(panel, "Camera Image Box", Padding, PreviewTop, PreviewWidth,
				PreviewHeight);
			RectTransform previewRect = CreateRect("Camera Image", previewBox);
			Vector2 previewMaxSize = new(PreviewWidth - 2f * PreviewInset, PreviewHeight - 2f * PreviewInset);
			previewRect.sizeDelta = previewMaxSize;
			RawImage preview = previewRect.gameObject.AddComponent<RawImage>();
			preview.material = _assets.Ui;
			preview.raycastTarget = false;

			// Shown once the camera plays: an image without a texture would draw white
			preview.enabled = false;

			float numberLeft = Padding + PreviewWidth + NumberColumnGap;
			float numberWidth = DialogWidth - numberLeft - Padding;
			CreateText(panel, "Number Caption", "Participant", numberLeft, NumberCaptionCapMiddle, numberWidth,
				SmallLabelSize, isSemiBold: false, MutedColor, TextAlignmentOptions.CaplineLeft);
			TextMeshProUGUI number = CreateText(panel, "Participant Number", "---", numberLeft, NumberCapMiddle,
				numberWidth, NumberSize, isSemiBold: true, TextColor, TextAlignmentOptions.CaplineLeft);
			CreateText(panel, "Group", "Group A", numberLeft, GroupCapMiddle, numberWidth, SmallLabelSize + 3f,
				isSemiBold: true, AccentColor, TextAlignmentOptions.CaplineLeft);

			TextMeshProUGUI status = CreateText(panel, "Status", string.Empty, Padding, 0f, DialogWidth - 2f * Padding,
				StatusSize, isSemiBold: false, MutedColor, TextAlignmentOptions.TopLeft);
			Place(status.rectTransform, Padding, StatusTop, DialogWidth - 2f * Padding, StatusHeight);
			status.textWrappingMode = TextWrappingModes.Normal;

			RectTransform typeRect = CreateRect("Type Number", panel);
			Place(typeRect, Padding, CheckInButtonsTop, DialogButtonWidth, MenuButtonHeight);
			AdminMenuButton type = CreateButtonParts(typeRect, "Type number", ButtonRadius, hasBorder: true,
				isTonal: false, isIconLikeText: false, null, 0f, 0f, -1f, ButtonLabelSize);
			RectTransform startRect = CreateRect("Start Session", panel);
			Place(startRect, Padding + DialogButtonWidth + DialogButtonGap, CheckInButtonsTop, DialogButtonWidth,
				MenuButtonHeight);
			AdminMenuButton start = CreateButtonParts(startRect, "Start session", ButtonRadius, hasBorder: false,
				isTonal: true, isIconLikeText: false, null, 0f, 0f, -1f, ButtonLabelSize);

			screen.Configure(_assets.Settings, screen.GetComponent<CanvasGroup>(), new[] { type, start });
			menu.ConfigureCheckIn(screen, type, start, number, status, preview, previewMaxSize);
		}

		// The number pad: the number typed so far in a box, then 1 2 3 / 4 5 6 / 7 8 9 / Delete 0 OK
		private void CreateNumberPad(RectTransform canvas, AdminMenu menu)
		{
			AdminMenuScreen screen = CreateScreen(canvas, "Number Pad Screen", PadWidth, PadHeight,
				out RectTransform panel);
			CreateText(panel, "Title", "Participant number", Padding, PadTitleCapMiddle, PadContentWidth, TitleSize,
				isSemiBold: false, TextColor, TextAlignmentOptions.CaplineLeft);
			RectTransform entryBox = CreateBox(panel, "Entry", Padding, EntryTop, PadContentWidth, EntryHeight);
			TextMeshProUGUI entry = CreateText(entryBox, "Number", "---", 0f, EntryHeight * 0.5f, PadContentWidth,
				EntrySize, isSemiBold: true, TextColor, TextAlignmentOptions.Capline);

			AdminMenuButton[] digits = new AdminMenuButton[10];
			for (int digit = 1; digit <= 9; digit++)
			{
				digits[digit] = CreateKey(panel, digit.ToString(CultureInfo.InvariantCulture), (digit - 1) % 3, (digit - 1) / 3, isTonal: false);
			}

			AdminMenuButton delete = CreateKey(panel, "Delete", 0, 3, isTonal: false);
			digits[0] = CreateKey(panel, "0", 1, 3, isTonal: false);
			AdminMenuButton ok = CreateKey(panel, "OK", 2, 3, isTonal: true);

			AdminMenuButton[] keys = new AdminMenuButton[12];
			for (int digit = 1; digit <= 9; digit++)
			{
				keys[digit - 1] = digits[digit];
			}

			keys[9] = delete;
			keys[10] = digits[0];
			keys[11] = ok;
			screen.Configure(_assets.Settings, screen.GetComponent<CanvasGroup>(), keys);
			menu.ConfigureNumberPad(screen, digits, delete, ok, entry);
		}

		// One of the number pad's keys, by column and row from the top-left; digits are larger
		private AdminMenuButton CreateKey(RectTransform panel, string label, int column, int row, bool isTonal)
		{
			RectTransform rect = CreateRect($"Key {label}", panel);
			Place(rect, Padding + column * (KeyWidth + KeyGap), KeysTop + row * (KeyHeight + KeyGap), KeyWidth,
				KeyHeight);
			float labelSize = label.Length == 1 ? DigitSize : ButtonLabelSize;
			return CreateButtonParts(rect, label, ButtonRadius, hasBorder: !isTonal, isTonal, isIconLikeText: false,
				null, 0f, 0f, -1f, labelSize);
		}

		// A bordered box like the delete dialog's, e.g. around the camera image
		private RectTransform CreateBox(RectTransform panel, string name, float left, float top, float width,
			float height)
		{
			RectTransform box = CreateRect(name, panel);
			Place(box, left, top, width, height);
			CreateImage("Border", box, _assets.RoundedRect, AdminMenuAssetBuilder.BorderColor, ButtonRadius)
				.rectTransform.sizeDelta = Vector2.zero;
			Image fill = CreateImage("Fill", box, _assets.RoundedRect, SurfaceColor, ButtonRadius - BorderWidth);
			Inset(fill.rectTransform, BorderWidth);
			return box;
		}

		// A dialog: a two-line title, then Cancel and the confirm button side by side
		private AdminMenuScreen CreateDialog(RectTransform canvas, string name, float height, string title,
			float buttonsTop, string confirmLabel, out AdminMenuButton cancel, out AdminMenuButton confirm,
			out RectTransform panel)
		{
			AdminMenuScreen screen = CreateScreen(canvas, name, DialogWidth, height, out panel);
			TextMeshProUGUI titleText = CreateText(panel, "Title", title, Padding, 0f, DialogWidth - 2f * Padding,
				TitleSize, isSemiBold: false, TextColor, TextAlignmentOptions.TopLeft);
			Place(titleText.rectTransform, Padding, Padding, DialogWidth - 2f * Padding, buttonsTop - Padding);
			titleText.textWrappingMode = TextWrappingModes.Normal;
			titleText.lineSpacing = TitleLineSpacing;

			RectTransform cancelRect = CreateRect("Cancel", panel);
			Place(cancelRect, Padding, buttonsTop, DialogButtonWidth, MenuButtonHeight);
			cancel = CreateButtonParts(cancelRect, "Cancel", ButtonRadius, hasBorder: true, isTonal: false,
				isIconLikeText: false, null, 0f, 0f, -1f, ButtonLabelSize);

			RectTransform confirmRect = CreateRect(confirmLabel, panel);
			Place(confirmRect, Padding + DialogButtonWidth + DialogButtonGap, buttonsTop, DialogButtonWidth,
				MenuButtonHeight);
			confirm = CreateButtonParts(confirmRect, confirmLabel, ButtonRadius, hasBorder: true, isTonal: false,
				isIconLikeText: false, null, 0f, 0f, -1f, ButtonLabelSize);

			screen.Configure(_assets.Settings, screen.GetComponent<CanvasGroup>(), new[] { cancel, confirm });
			return screen;
		}

		// The delete dialog's box naming the session
		private TMP_Text CreateDetails(RectTransform panel)
		{
			RectTransform box = CreateRect("Selected Session", panel);
			Place(box, Padding, DetailsTop, DialogWidth - 2f * Padding, DetailsHeight);
			CreateImage("Border", box, _assets.RoundedRect, AdminMenuAssetBuilder.BorderColor, ButtonRadius)
				.rectTransform.sizeDelta = Vector2.zero;
			Image fill = CreateImage("Fill", box, _assets.RoundedRect, SurfaceColor, ButtonRadius - BorderWidth);
			Inset(fill.rectTransform, BorderWidth);

			float width = DialogWidth - 2f * Padding - 2f * DetailsInset;
			CreateText(box, "Caption", "Selected session", DetailsInset, CaptionCapMiddle, width, CaptionSize,
				isSemiBold: false, MutedColor, TextAlignmentOptions.CaplineLeft);
			TextMeshProUGUI details = CreateText(box, "Details", "Id  001\nDate  2026-03-12\nTime  09:14",
				DetailsInset, 0f, width, DetailsTextSize, isSemiBold: false, TextColor, TextAlignmentOptions.TopLeft);
			Place(details.rectTransform, DetailsInset, DetailsTextTop, width,
				DetailsHeight - DetailsTextTop - DetailsInset * 0.5f);
			details.lineSpacing = DetailsLineSpacing;
			return details;
		}

		// A button's border, fill, optional icon and label inside its rect. labelLeft < 0 centres the label.
		private AdminMenuButton CreateButtonParts(RectTransform rect, string label, float radius, bool hasBorder,
			bool isTonal, bool isIconLikeText, Sprite icon, float iconCenter, float iconSize, float labelLeft,
			float labelSize)
		{
			Image border = null;
			if (hasBorder)
			{
				border = CreateImage("Border", rect, _assets.RoundedRect, Color.white, radius);
				border.rectTransform.sizeDelta = Vector2.zero;
			}

			Image fill = CreateImage("Fill", rect, _assets.RoundedRect, Color.white,
				hasBorder ? radius - BorderWidth : radius);
			fill.rectTransform.sizeDelta = Vector2.zero;
			if (hasBorder)
			{
				Inset(fill.rectTransform, BorderWidth);
			}

			Image iconImage = null;
			if (icon != null)
			{
				iconImage = CreateImage("Icon", rect, icon, Color.white, 0f);
				RectTransform iconRect = iconImage.rectTransform;
				iconRect.anchorMin = new Vector2(0f, 0.5f);
				iconRect.anchorMax = new Vector2(0f, 0.5f);
				iconRect.sizeDelta = new Vector2(iconSize, iconSize);
				iconRect.anchoredPosition = new Vector2(iconCenter, 0f);
			}

			float height = rect.sizeDelta.y;
			float width = rect.sizeDelta.x;
			TextMeshProUGUI text = labelLeft < 0f
				? CreateText(rect, "Label", label, 0f, height * 0.5f, width, labelSize, isSemiBold: true, Color.white,
					TextAlignmentOptions.Capline)
				: CreateText(rect, "Label", label, labelLeft, height * 0.5f, width - labelLeft, labelSize,
					isSemiBold: true, Color.white, TextAlignmentOptions.CaplineLeft);

			AdminMenuButton button = rect.gameObject.AddComponent<AdminMenuButton>();
			button.Configure(_assets.Settings, fill, border, text, iconImage, isTonal, isIconLikeText);
			return button;
		}

		// Text whose capitals' middle sits capMiddle below the parent's top edge (Capline alignment), starting at left
		private TextMeshProUGUI CreateText(RectTransform parent, string name, string text, float left,
			float capMiddle, float width, float fontSize, bool isSemiBold, Color color,
			TextAlignmentOptions alignment)
		{
			RectTransform rect = CreateRect(name, parent);
			float height = fontSize * 2f;
			Place(rect, left, capMiddle - height * 0.5f, width, height);
			TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
			label.font = isSemiBold ? _assets.SemiBoldFont : _assets.RegularFont;
			label.fontSharedMaterial = isSemiBold ? _assets.SemiBoldText : _assets.RegularText;
			label.fontSize = fontSize;
			label.color = color;
			label.alignment = alignment;
			label.textWrappingMode = TextWrappingModes.NoWrap;
			label.overflowMode = TextOverflowModes.Overflow;
			label.raycastTarget = false;
			label.text = text;
			return label;
		}

		// A sliced rounded image (or a plain one without a sprite) filling its parent
		private Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color, float cornerRadius)
		{
			RectTransform rect = CreateRect(name, parent);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.sizeDelta = Vector2.zero;
			Image image = rect.gameObject.AddComponent<Image>();
			image.sprite = sprite;
			image.material = _assets.Ui;
			image.color = color;
			image.raycastTarget = false;
			if (sprite != null && cornerRadius > 0f && sprite.border.x > 0f)
			{
				image.type = Image.Type.Sliced;
				image.pixelsPerUnitMultiplier = AdminMenuAssetBuilder.SpriteCornerRadius / cornerRadius;
			}

			return image;
		}

		private static RectTransform CreateRect(string name, Transform parent)
		{
			GameObject rectObject = new(name, typeof(RectTransform));
			RectTransform rect = (RectTransform)rectObject.transform;
			rect.SetParent(parent, false);
			return rect;
		}

		// From the parent's top-left corner, y down, with the pivot in the middle (buttons scale about their centre)
		private static void Place(RectTransform rect, float left, float top, float width, float height)
		{
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(width, height);
			rect.anchoredPosition = new Vector2(left + width * 0.5f, -(top + height * 0.5f));
		}

		private static void Inset(RectTransform rect, float inset)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = new Vector2(inset, inset);
			rect.offsetMax = new Vector2(-inset, -inset);
		}

		#endregion
	}
}
