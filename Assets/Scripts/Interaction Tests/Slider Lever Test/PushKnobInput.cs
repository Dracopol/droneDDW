using DroneDDW.InteractionTests;
using UnityEngine;

namespace DroneDDW.StepControls
{
	// Any part of a tracked hand shoves the knob along its path, from either side.
	// A hand on each side holds it in between, so it can also be squeezed and carried.
	internal class PushKnobInput : KnobInput
	{
		#region Serialized fields

		[SerializeField] private KnobPath _path;
		[SerializeField] private HandSkeleton[] _hands;

		[Tooltip("How close a hand counts as touching, so resting against the knob keeps holding it")]
		[Min(0f)]
		[SerializeField] private float _touchMarginMeters = 0.005f;

		[Tooltip("Spacing of the spheres sampled along each hand bone")]
		[Min(0.002f)]
		[SerializeField] private float _sampleSpacingMeters = 0.01f;

		#endregion

		#region Internal

		internal override bool TryGetTarget(float currentPosition01, out float targetPosition01)
		{
			// The knob can't be below a hand touching it from the 0% side, or above one touching it from the other side
			float lowestAllowed01 = float.NegativeInfinity;
			float highestAllowed01 = float.PositiveInfinity;
			bool isTouched = false;

			foreach (HandSkeleton hand in _hands)
			{
				if (!hand.IsTracked)
				{
					continue;
				}

				foreach (HandBone bone in hand.Bones)
				{
					Vector3 start = bone.Start.position;
					Vector3 end = bone.End.position;
					float radiusMeters = bone.RadiusMeters + _touchMarginMeters;
					float lengthMeters = Vector3.Distance(start, end);
					int segmentCount = Mathf.Max(1, Mathf.CeilToInt(lengthMeters / _sampleSpacingMeters));

					for (int sample = 0; sample <= segmentCount; sample++)
					{
						Vector3 point = Vector3.Lerp(start, end, (float)sample / segmentCount);

						if (!_path.TryGetContact(point, radiusMeters, out float center01, out float halfWidth01)
							|| Mathf.Abs(center01 - currentPosition01) >= halfWidth01)
						{
							continue;
						}

						isTouched = true;

						if (center01 < currentPosition01)
						{
							lowestAllowed01 = Mathf.Max(lowestAllowed01, center01 + halfWidth01);
						}
						else
						{
							highestAllowed01 = Mathf.Min(highestAllowed01, center01 - halfWidth01);
						}
					}
				}
			}

			// Squeezed tighter than the knob is wide, it sits in the middle of the grip
			targetPosition01 = lowestAllowed01 > highestAllowed01
				? (lowestAllowed01 + highestAllowed01) * 0.5f
				: Mathf.Clamp(currentPosition01, lowestAllowed01, highestAllowed01);
			targetPosition01 = Mathf.Clamp01(targetPosition01);
			return isTouched;
		}

		#endregion
	}
}
