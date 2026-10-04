using System.Collections.Generic;
using DroneDDW.Admin.Sessions;
using TMPro;
using UnityEngine;

namespace DroneDDW.Admin
{
	// The session history table: one row per session with its Delete button, a message when there are none, and the
	// session count below. Rows that don't fit scroll into view as the hover reaches them.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Session History Screen")]
	internal sealed class SessionHistoryScreen : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private AdminMenuSettings _settings;
		[SerializeField] private AdminMenuButton _backButton;

		[Tooltip("The table's body, which clips the rows (RectMask2D)")]
		[SerializeField] private RectTransform _viewport;

		[Tooltip("Holds the rows; moved up to scroll. Anchored to the viewport's top.")]
		[SerializeField] private RectTransform _content;

		[Tooltip("Inactive; copied for each row")]
		[SerializeField] private SessionRowView _rowTemplate;

		[SerializeField] private TMP_Text _emptyLabel;
		[SerializeField] private TMP_Text _footer;

		[Tooltip("In the menu's pixels")]
		[Min(1f)]
		[SerializeField] private float _rowHeightPixels = 68f;

		#endregion

		#region Fields

		private readonly List<SessionRowView> _rows = new();
		private readonly List<AdminMenuButton> _buttons = new();
		private int _rowCount;
		private float _targetScrollPixels;
		private float _scrollVelocity;

		#endregion

		#region Properties

		// Go back, then the rows' Delete buttons from the top
		internal IReadOnlyList<AdminMenuButton> Buttons => _buttons;

		internal AdminMenuButton BackButton => _backButton;
		internal int RowCount => _rowCount;

		// How far the list is scrolled down, in the menu's pixels
		internal float ScrollPixels => _content.anchoredPosition.y;

		internal string FooterText => _footer.text;

		#endregion

		#region Update

		private void Update()
		{
			Vector2 position = _content.anchoredPosition;
			position.y = Mathf.SmoothDamp(position.y, _targetScrollPixels, ref _scrollVelocity,
				_settings.ScrollSmoothSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
			_content.anchoredPosition = position;
		}

		#endregion

		#region Internal

		internal void Configure(AdminMenuSettings settings, AdminMenuButton backButton, RectTransform viewport,
			RectTransform content, SessionRowView rowTemplate, TMP_Text emptyLabel, TMP_Text footer,
			float rowHeightPixels)
		{
			_settings = settings;
			_backButton = backButton;
			_viewport = viewport;
			_content = content;
			_rowTemplate = rowTemplate;
			_emptyLabel = emptyLabel;
			_footer = footer;
			_rowHeightPixels = rowHeightPixels;
		}

		// Fills the table; emptyMessage shows when there are no sessions
		internal void ShowRecords(IReadOnlyList<SessionRecord> records, string emptyMessage)
		{
			_rowCount = records.Count;
			while (_rows.Count < _rowCount)
			{
				SessionRowView row = Instantiate(_rowTemplate, _content);
				row.name = $"Session Row {_rows.Count + 1}";
				_rows.Add(row);
			}

			_buttons.Clear();
			_buttons.Add(_backButton);
			for (int index = 0; index < _rows.Count; index++)
			{
				if (index < _rowCount)
				{
					_rows[index].Show(records[index], index, index * _rowHeightPixels);
					_buttons.Add(_rows[index].DeleteButton);
				}
				else
				{
					_rows[index].Hide();
				}
			}

			_content.sizeDelta = new Vector2(_content.sizeDelta.x, _rowCount * _rowHeightPixels);
			_emptyLabel.enabled = _rowCount == 0;
			_emptyLabel.text = emptyMessage;
			_footer.text = _rowCount == 1 ? "1 session" : $"{_rowCount} sessions";
			_targetScrollPixels = Mathf.Clamp(_targetScrollPixels, 0f, MaxScrollPixels());
			SnapScroll();
		}

		internal bool TryGetRecord(AdminMenuButton button, out SessionRecord record)
		{
			for (int index = 0; index < _rowCount; index++)
			{
				if (_rows[index].DeleteButton == button)
				{
					record = _rows[index].Record;
					return true;
				}
			}

			record = null;
			return false;
		}

		// The row showing this session, or -1
		internal int IndexOf(SessionRecord record)
		{
			for (int index = 0; index < _rowCount; index++)
			{
				if (_rows[index].Record == record)
				{
					return index;
				}
			}

			return -1;
		}

		// The Delete button of row `index`, clamped to the rows there are; Go back when there are none
		internal AdminMenuButton GetDeleteButton(int index)
		{
			if (_rowCount == 0 || index < 0)
			{
				return _backButton;
			}

			return _rows[Mathf.Min(index, _rowCount - 1)].DeleteButton;
		}

		// Scrolls just enough to show the button's row; Go back scrolls to the top
		internal void ScrollToShow(AdminMenuButton button, bool isInstant)
		{
			int index = -1;
			for (int row = 0; row < _rowCount; row++)
			{
				if (_rows[row].DeleteButton == button)
				{
					index = row;
				}
			}

			if (index < 0)
			{
				_targetScrollPixels = 0f;
			}
			else
			{
				float top = index * _rowHeightPixels;
				float bottom = top + _rowHeightPixels;
				float viewHeight = _viewport.rect.height;
				if (top < _targetScrollPixels)
				{
					_targetScrollPixels = top;
				}
				else if (bottom > _targetScrollPixels + viewHeight)
				{
					_targetScrollPixels = bottom - viewHeight;
				}
			}

			_targetScrollPixels = Mathf.Clamp(_targetScrollPixels, 0f, MaxScrollPixels());
			if (isInstant)
			{
				SnapScroll();
			}
		}

		#endregion

		#region Private

		private float MaxScrollPixels()
		{
			return Mathf.Max(0f, _rowCount * _rowHeightPixels - _viewport.rect.height);
		}

		private void SnapScroll()
		{
			_scrollVelocity = 0f;
			_content.anchoredPosition = new Vector2(_content.anchoredPosition.x, _targetScrollPixels);
		}

		#endregion
	}
}
