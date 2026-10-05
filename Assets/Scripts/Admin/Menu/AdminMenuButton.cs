using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DroneDDW.Admin
{
	// One of the admin menu's buttons. It isn't clicked by pointing: the menu moves the hover with the thumbsticks and
	// clicks the hovered button. Hovered, it turns pale blue with dark text and grows a little; a click squeezes it.
	// Colours come from the settings, so they can be tuned live.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Admin Menu Button")]
	internal sealed class AdminMenuButton : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private AdminMenuSettings _settings;

		[Tooltip("Scaled by the hover and the click; the button's own rect")]
		[SerializeField] private RectTransform _body;

		[SerializeField] private Image _fill;

		[Tooltip("Optional: drawn behind the fill, a little bigger")]
		[SerializeField] private Image _border;

		[SerializeField] private TMP_Text _label;

		[Tooltip("Optional")]
		[SerializeField] private Image _icon;

		[Tooltip("The rows' Delete buttons: a blue fill and text instead of the dark fill, white text and border")]
		[SerializeField] private bool _isTonal;

		[Tooltip("The icon takes the text colour instead of the icon colour (Go back's arrow)")]
		[SerializeField] private bool _isIconLikeText;

		#endregion

		#region Fields

		private float _hoverAmount;
		private float _pressRemainingSeconds;

		// The label's own text while a temporary one shows, otherwise null
		private string _savedLabel;
		private float _temporaryLabelRemainingSeconds;

		#endregion

		#region Properties

		internal bool IsHovered { get; set; }

		internal RectTransform Body => _body;

		internal string Label => _label.text;

		// Hidden buttons are skipped by the hover
		internal bool IsShown => gameObject.activeSelf;

		#endregion

		#region Setup

		// A screen that just appeared shows its hover at once rather than fading it in
		private void OnEnable()
		{
			Refresh();
		}

		private void OnDisable()
		{
			RestoreLabel();
		}

		#endregion

		#region Update

		// Unscaled time: the menu runs while the session is paused
		private void Update()
		{
			float deltaSeconds = Time.unscaledDeltaTime;
			_hoverAmount = Mathf.MoveTowards(_hoverAmount, IsHovered ? 1f : 0f, deltaSeconds / _settings.HoverSeconds);
			_pressRemainingSeconds = Mathf.Max(0f, _pressRemainingSeconds - deltaSeconds);
			if (_savedLabel != null)
			{
				_temporaryLabelRemainingSeconds -= deltaSeconds;
				if (_temporaryLabelRemainingSeconds <= 0f)
				{
					RestoreLabel();
				}
			}

			Apply();
		}

		#endregion

		#region Internal

		internal void Configure(AdminMenuSettings settings, Image fill, Image border, TMP_Text label, Image icon,
			bool isTonal, bool isIconLikeText)
		{
			_settings = settings;
			_body = (RectTransform)transform;
			_fill = fill;
			_border = border;
			_label = label;
			_icon = icon;
			_isTonal = isTonal;
			_isIconLikeText = isIconLikeText;

			// So the prefab shows the real colours in the editor too
			Refresh();
		}

		// Shows the hover state at once, without fading
		internal void Refresh()
		{
			_hoverAmount = IsHovered ? 1f : 0f;
			_pressRemainingSeconds = 0f;
			Apply();
		}

		// The click's squeeze; the menu decides what the click does
		internal void PlayPress()
		{
			_pressRemainingSeconds = _settings.PressSeconds;
		}

		internal void SetShown(bool isShown)
		{
			gameObject.SetActive(isShown);
		}

		// Shows other text on the button for a while, e.g. "Saved 000 at 14:32", then its own label again
		internal void ShowTemporaryLabel(string text, float durationSeconds)
		{
			_savedLabel ??= _label.text;
			_label.text = text;
			_temporaryLabelRemainingSeconds = durationSeconds;
		}

		#endregion

		#region Private

		private void RestoreLabel()
		{
			if (_savedLabel == null)
			{
				return;
			}

			_label.text = _savedLabel;
			_savedLabel = null;
		}

		private void Apply()
		{
			Color fill = _isTonal ? _settings.TonalFillColor : _settings.ButtonFillColor;
			Color border = _isTonal ? _settings.TonalFillColor : _settings.ButtonBorderColor;
			Color text = _isTonal ? _settings.TonalTextColor : _settings.ButtonTextColor;
			Color icon = _isIconLikeText ? text : _settings.ButtonIconColor;

			_fill.color = Color.Lerp(fill, _settings.HoverFillColor, _hoverAmount);
			if (_border != null)
			{
				_border.color = Color.Lerp(border, _settings.HoverFillColor, _hoverAmount);
			}

			_label.color = Color.Lerp(text, _settings.HoverTextColor, _hoverAmount);
			if (_icon != null)
			{
				_icon.color = Color.Lerp(icon, _settings.HoverTextColor, _hoverAmount);
			}

			// The squeeze goes in and back out over the press time
			float press = 0f;
			if (_pressRemainingSeconds > 0f)
			{
				press = Mathf.Sin(Mathf.PI * (1f - _pressRemainingSeconds / _settings.PressSeconds));
			}

			float scale = Mathf.Lerp(1f, _settings.HoverScale, _hoverAmount) * Mathf.Lerp(1f, _settings.PressScale, press);
			_body.localScale = new Vector3(scale, scale, 1f);
		}

		#endregion
	}
}
