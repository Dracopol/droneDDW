namespace DroneDDW.Admin
{
	// One frame's input for the menu
	internal readonly struct MenuCommand
	{
		#region Setup

		internal MenuCommand(MenuCommandKind kind, MenuHand hand, MenuDirection direction = MenuDirection.Up)
		{
			Kind = kind;
			Hand = hand;
			Direction = direction;
		}

		#endregion

		#region Properties

		internal static MenuCommand None => new(MenuCommandKind.None, MenuHand.None);

		internal MenuCommandKind Kind { get; }
		internal MenuHand Hand { get; }

		// Only for Move
		internal MenuDirection Direction { get; }

		#endregion
	}
}
