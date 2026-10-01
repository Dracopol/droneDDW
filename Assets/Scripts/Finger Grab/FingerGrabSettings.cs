using System;
using UnityEngine;

namespace DroneDDW.FingerGrab
{
	// The finger grab's tuning, shared by both hands and every scene that uses this asset: each grab pair's grab and
	// release distance, and whether the tuning labels show. An asset, so changes made in Play mode are kept.
	[CreateAssetMenu(fileName = "Finger Grab Settings", menuName = "DroneDDW/Interaction/Finger Grab Settings")]
	internal class FingerGrabSettings : ScriptableObject
	{
		#region Serialized fields

		[Tooltip("Shows the label by each hand with every pair's distance and thresholds")]
		[SerializeField] private bool _isReadoutVisible = true;

		[Header("Thumb tip to fingertip")]
		[SerializeField] private FingerGrabThreshold _index = new(0.03f, 0.045f);
		[SerializeField] private FingerGrabThreshold _middle = new(0.03f, 0.045f);
		[SerializeField] private FingerGrabThreshold _ring = new(0.035f, 0.05f);
		[SerializeField] private FingerGrabThreshold _little = new(0.035f, 0.05f);

		// The thumb's middle joint (where it bends) to each finger's middle knuckle: close in a fist, or with the thumb
		// pressed along the fingers
		[Header("Thumb knuckle to finger knuckle")]
		[SerializeField] private FingerGrabThreshold _indexKnuckle = new(0.025f, 0.035f);
		[SerializeField] private FingerGrabThreshold _middleKnuckle = new(0.025f, 0.035f);
		[SerializeField] private FingerGrabThreshold _ringKnuckle = new(0.03f, 0.04f);
		[SerializeField] private FingerGrabThreshold _littleKnuckle = new(0.03f, 0.04f);

		#endregion

		#region Properties

		internal bool IsReadoutVisible => _isReadoutVisible;

		#endregion

		#region Internal

		internal FingerGrabThreshold GetThreshold(GrabPair pair)
		{
			return pair switch
			{
				GrabPair.IndexTip => _index,
				GrabPair.MiddleTip => _middle,
				GrabPair.RingTip => _ring,
				GrabPair.LittleTip => _little,
				GrabPair.IndexKnuckle => _indexKnuckle,
				GrabPair.MiddleKnuckle => _middleKnuckle,
				GrabPair.RingKnuckle => _ringKnuckle,
				GrabPair.LittleKnuckle => _littleKnuckle,
				_ => throw new ArgumentOutOfRangeException(nameof(pair), pair, null),
			};
		}

		#endregion
	}
}
