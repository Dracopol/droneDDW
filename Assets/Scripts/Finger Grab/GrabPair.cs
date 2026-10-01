namespace DroneDDW.FingerGrab
{
	// The pairs of joints that grab when they come close, in hand order: the thumb tip with each fingertip (a pinch),
	// and the thumb's knuckle with each finger's middle knuckle (a fist, or the thumb pressed along the fingers)
	internal enum GrabPair
	{
		IndexTip,
		MiddleTip,
		RingTip,
		LittleTip,
		IndexKnuckle,
		MiddleKnuckle,
		RingKnuckle,
		LittleKnuckle,
	}
}
