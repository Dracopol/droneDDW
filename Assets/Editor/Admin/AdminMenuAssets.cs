using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDDW.Admin.Editor
{
	// What the admin menu prefab is built from, loaded or made by AdminMenuAssetBuilder
	internal sealed class AdminMenuAssets
	{
		#region Properties

		internal AdminMenuSettings Settings { get; set; }

		internal TMP_FontAsset RegularFont { get; set; }
		internal TMP_FontAsset SemiBoldFont { get; set; }

		// The fonts' materials at the menu's render queue
		internal Material RegularText { get; set; }
		internal Material SemiBoldText { get; set; }

		// For the menu's images: UI/Default at the menu's render queue
		internal Material Ui { get; set; }

		internal Material Dimmer { get; set; }

		// A white rounded rectangle (9-slice) for panels, buttons, badges and the handle
		internal Sprite RoundedRect { get; set; }

		// The table's border, with the panel colour outside its rounded corners (9-slice), drawn over the rows
		internal Sprite TableFrame { get; set; }

		internal Sprite PlayIcon { get; set; }
		internal Sprite ResetIcon { get; set; }
		internal Sprite HistoryIcon { get; set; }
		internal Sprite LocationIcon { get; set; }
		internal Sprite LanguageIcon { get; set; }
		internal Sprite SaveIcon { get; set; }
		internal Sprite BackIcon { get; set; }

		internal InputActionReference ToggleAction { get; set; }
		internal InputActionReference NavigateAction { get; set; }
		internal InputActionReference ClickAction { get; set; }
		internal InputActionReference BackAction { get; set; }

		#endregion
	}
}
