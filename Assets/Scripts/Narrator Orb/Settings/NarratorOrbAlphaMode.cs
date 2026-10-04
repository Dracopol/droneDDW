namespace DroneDDW.NarratorOrb.Settings
{
	// What the orb writes into the alpha channel, which decides how it sits on the passthrough
	internal enum NarratorOrbAlphaMode
	{
		// Adds the glow's brightness (times the alpha gain), so bright parts also cover the passthrough behind them
		Brightness = 0,

		// Leaves alpha alone: the glow is pure light added over the passthrough
		Untouched = 1,

		// Adds alpha the way the web preview's blending does for colour (alpha times alpha)
		SameAsColor = 2,
	}
}
