namespace DroneDDW.Admin
{
	internal enum MenuCommandKind
	{
		None,

		// The left controller's menu button (Esc): opens the menu, or closes it and resumes
		Toggle,

		// A thumbstick push (arrow keys, WASD): moves the hover
		Move,

		// Trigger or grip (Enter, Space): clicks the hovered button
		Click,

		// A, B, X or Y (Backspace): one screen back, or cancels a dialog
		Back,
	}
}
