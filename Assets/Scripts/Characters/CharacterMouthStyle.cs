namespace DroneDDW.Characters
{
	// How a portrait's mouth follows the voice
	internal enum CharacterMouthStyle
	{
		// Swaps between the closed and the open portrait
		Flap = 0,

		// Keeps the closed portrait and opens the mouth gradually with the voice (experimental)
		Smooth = 1,
	}
}
