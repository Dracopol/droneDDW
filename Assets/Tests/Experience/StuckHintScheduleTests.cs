using NUnit.Framework;

namespace DroneDDW.Experience.Tests
{
	internal sealed class StuckHintScheduleTests
	{
		#region Tests

		[Test]
		public void GetDelaySeconds_FirstHint_WaitsTheFirstDelay()
		{
			StuckHintSchedule schedule = new(8f, 15f);

			Assert.That(schedule.GetDelaySeconds(0), Is.EqualTo(8f));
		}

		[TestCase(1)]
		[TestCase(4)]
		public void GetDelaySeconds_LaterHints_WaitTheRepeat(int hint)
		{
			StuckHintSchedule schedule = new(8f, 15f);

			Assert.That(schedule.GetDelaySeconds(hint), Is.EqualTo(15f));
		}

		#endregion
	}
}
