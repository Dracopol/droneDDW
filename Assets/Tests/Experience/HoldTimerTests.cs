using NUnit.Framework;

namespace DroneDDW.Experience.Tests
{
	internal sealed class HoldTimerTests
	{
		#region Tests

		[Test]
		public void Update_HeldLongEnough_Completes()
		{
			HoldTimer hold = new(1f);

			Assert.That(hold.Update(true, 0.6f), Is.False);
			Assert.That(hold.Update(true, 0.6f), Is.True);
			Assert.That(hold.Progress, Is.EqualTo(1f));
		}

		[Test]
		public void Update_Break_StartsOver()
		{
			HoldTimer hold = new(1f);
			hold.Update(true, 0.8f);

			hold.Update(false, 0.1f);

			Assert.That(hold.Progress, Is.EqualTo(0f));
			Assert.That(hold.Update(true, 0.5f), Is.False);
		}

		[Test]
		public void Update_AfterComplete_StaysComplete()
		{
			HoldTimer hold = new(0.5f);
			hold.Update(true, 0.5f);

			Assert.That(hold.Update(false, 0.1f), Is.True);
			Assert.That(hold.IsComplete, Is.True);
		}

		[Test]
		public void Update_NoHoldTime_CompletesOnFirstHold()
		{
			HoldTimer hold = new(0f);

			Assert.That(hold.Update(true, 0f), Is.True);
		}

		#endregion
	}
}
