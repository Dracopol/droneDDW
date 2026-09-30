using UnityEngine;

namespace DroneDDW.Buttons
{
	// Moves a button's cap, the spring button only needs to know how far down it is
	internal abstract class ButtonCap : MonoBehaviour
	{
		#region Properties

		// 0 while the cap is up at rest, 1 when it's down at the bottom stop
		internal abstract float PressDepth01 { get; }

		#endregion
	}
}
