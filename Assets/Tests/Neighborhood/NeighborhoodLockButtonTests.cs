using DroneDDW.Neighborhood.Table;
using NUnit.Framework;

namespace DroneDDW.Neighborhood.Tests
{
	internal sealed class NeighborhoodLockButtonTests
	{
		#region Tests

		// A three second hold counts 3, 2, 1
		[TestCase(0f, 3)]
		[TestCase(0.2f, 3)]
		[TestCase(0.34f, 2)]
		[TestCase(0.66f, 2)]
		[TestCase(0.67f, 1)]
		[TestCase(0.999f, 1)]
		[TestCase(1f, 1)]
		public void GetCountdown_ThreeSecondHold_CountsDown(float progress, int expected)
		{
			Assert.That(NeighborhoodLockButton.GetCountdown(progress, 3f), Is.EqualTo(expected));
		}

		[Test]
		public void GetCountdown_ShortHold_ShowsOne()
		{
			Assert.That(NeighborhoodLockButton.GetCountdown(0f, 0.5f), Is.EqualTo(1));
		}

		#endregion
	}
}
