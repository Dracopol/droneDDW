using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Admin
{
	// One of the menu's panels (paused, history, the two dialogs). One shows at a time and fades in when it appears.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Admin Menu Screen")]
	internal sealed class AdminMenuScreen : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private AdminMenuSettings _settings;
		[SerializeField] private CanvasGroup _canvasGroup;

		[Tooltip("The buttons the hover moves between (the history adds its rows' buttons itself)")]
		[SerializeField] private AdminMenuButton[] _buttons;

		#endregion

		#region Properties

		internal IReadOnlyList<AdminMenuButton> Buttons => _buttons;

		internal bool IsShown => gameObject.activeSelf;

		#endregion

		#region Update

		private void Update()
		{
			_canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f,
				Time.unscaledDeltaTime / _settings.FadeSeconds);
		}

		#endregion

		#region Internal

		internal void Configure(AdminMenuSettings settings, CanvasGroup canvasGroup, AdminMenuButton[] buttons)
		{
			_settings = settings;
			_canvasGroup = canvasGroup;
			_buttons = buttons;
		}

		internal void Show()
		{
			_canvasGroup.alpha = 0f;
			gameObject.SetActive(true);
		}

		internal void Hide()
		{
			gameObject.SetActive(false);
		}

		#endregion
	}
}
