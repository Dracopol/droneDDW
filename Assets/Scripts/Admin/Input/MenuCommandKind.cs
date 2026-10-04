namespace DroneDDW.Admin
{
	internal enum MenuCommandKind
	{
		None,

		// The left controller's menu button (Esc): opens the menu, or closes it and resumes
		Toggle,

		// A thumbstick push (arrow keys, WASD): moves the hover
		Move,

		// Trigger, grip or A / X (Enter, Space): clicks the hovered button
		Click,

		// B or Y (Backspace): one screen back, or cancels a dialog
		Back,
	}
}
