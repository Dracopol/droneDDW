using DroneDDW.Admin.Sessions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DroneDDW.Admin
{
	// One row of the session history table. Rows alternate two shades; the row whose Delete button is hovered lights
	// up, so it's clear which session a delete would remove.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Session Row View")]
	internal sealed class SessionRowView : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private AdminMenuSettings _settings;
		[SerializeField] private Image _background;
		[SerializeField] private TMP_Text _id;
		[SerializeField] private Image _groupBadge;
		[SerializeField] private TMP_Text _group;
		[SerializeField] private TMP_Text _firstTime;
		[SerializeField] private TMP_Text _formChoice;
		[SerializeField] private TMP_Text _date;
		[SerializeField] private TMP_Text _time;
		[SerializeField] private AdminMenuButton _deleteButton;

		[Tooltip("The first, third... rows")]
		[SerializeField] private Color _oddRowColor;

		[SerializeField] private Color _evenRowColor;

		[Tooltip("The group's letter on its badge")]
		[SerializeField] private Color _groupColor;

		[Tooltip("The dash shown for a missing group")]
		[SerializeField] private Color _missingColor;

		#endregion

		#region Fields

		private Color _rowColor;
		private float _hoverAmount;

		#endregion

		#region Properties

		internal SessionRecord Record { get; private set; }
		internal AdminMenuButton DeleteButton => _deleteButton;

		#endregion

		#region Update

		private void Update()
		{
			_hoverAmount = Mathf.MoveTowards(_hoverAmount, _deleteButton.IsHovered ? 1f : 0f,
				Time.unscaledDeltaTime / _settings.HoverSeconds);
			_background.color = Color.Lerp(_rowColor, _settings.HoverRowColor, _hoverAmount);
		}

		#endregion

		#region Internal

		internal void Configure(AdminMenuSettings settings, Image background, TMP_Text id, Image groupBadge,
			TMP_Text group, TMP_Text firstTime, TMP_Text formChoice, TMP_Text date, TMP_Text time,
			AdminMenuButton deleteButton, Color oddRowColor, Color evenRowColor, Color groupColor, Color missingColor)
		{
			_settings = settings;
			_background = background;
			_id = id;
			_groupBadge = groupBadge;
			_group = group;
			_firstTime = firstTime;
			_formChoice = formChoice;
			_date = date;
			_time = time;
			_deleteButton = deleteButton;
			_oddRowColor = oddRowColor;
			_evenRowColor = evenRowColor;
			_groupColor = groupColor;
			_missingColor = missingColor;
		}

		// Shows the session as row `index` (from 0), its top edge `topPixels` below the list's top
		internal void Show(SessionRecord record, int index, float topPixels)
		{
			Record = record;
			((RectTransform)transform).anchoredPosition = new Vector2(0f, -topPixels);
			_rowColor = index % 2 == 0 ? _oddRowColor : _evenRowColor;
			_hoverAmount = 0f;
			_background.color = _rowColor;

			_id.text = record.DisplayId;
			string group = record.Get(SessionColumns.Group);
			_groupBadge.enabled = group.Length > 0;
			_group.text = group.Length > 0 ? group : SessionRecord.Missing;
			_group.color = group.Length > 0 ? _groupColor : _missingColor;
			_firstTime.text = record.GetDisplay(SessionColumns.FirstTime);
			_formChoice.text = record.GetDisplay(SessionColumns.FormChoice);
			_date.text = record.GetDisplay(SessionColumns.Date);
			_time.text = record.GetDisplay(SessionColumns.Time);
			gameObject.SetActive(true);
		}

		internal void Hide()
		{
			Record = null;
			_deleteButton.IsHovered = false;
			gameObject.SetActive(false);
		}

		#endregion
	}
}
