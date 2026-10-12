using DroneDDW.Neighborhood.Table;
using NUnit.Framework;

namespace DroneDDW.Neighborhood.Tests
{
	internal sealed class NeighborhoodPushCapTests
	{
		#region Fields

		private const float TravelMeters = 0.02f;
		private const float FrameSeconds = 1f / 90f;
		private const float NoHand = float.PositiveInfinity;

		private NeighborhoodPushCap _cap;

		#endregion

		#region Setup

		[SetUp]
		public void CreateCap()
		{
			_cap = new NeighborhoodPushCap(TravelMeters, new NeighborhoodPushCapTuning());
		}

		#endregion

		#region Tests

		[Test]
		public void New_Untouched_RestsAtTheTop()
		{
			Run(1f, NoHand);

			Assert.That(_cap.HeightMeters, Is.EqualTo(TravelMeters));
			Assert.That(_cap.Depth01, Is.EqualTo(0f));
			Assert.That(_cap.IsTouched, Is.False);
			Assert.That(_cap.IsPressed, Is.False);
		}

		[Test]
		public void ApplyHand_HandPushingDownFromTheTop_FollowsItAndPresses()
		{
			PushToBottom();

			Assert.That(_cap.Depth01, Is.EqualTo(1f).Within(0.001f));
			Assert.That(_cap.IsTouched, Is.True);
			Assert.That(_cap.IsPressed, Is.True);
		}

		[Test]
		public void ApplyHand_HandStaysDown_HoldsTheCapDown()
		{
			PushToBottom();

			Run(1f, 0f);

			Assert.That(_cap.HeightMeters, Is.EqualTo(0f));
			Assert.That(_cap.IsPressed, Is.True);
		}

		[Test]
		public void Spring_HandLetsGo_SpringsBackUpAndReleases()
		{
			PushToBottom();

			Run(0.5f, NoHand);

			Assert.That(_cap.HeightMeters, Is.EqualTo(TravelMeters).Within(0.001f));
			Assert.That(_cap.IsPressed, Is.False);
			Assert.That(_cap.IsTouched, Is.False);
		}

		[Test]
		public void ApplyHand_HandRisesAbovePressDepth_StaysPressedUntilBelowRelease()
		{
			PushToBottom();

			// 60% down: above the release threshold, below the press threshold
			Run(0.5f, TravelMeters * 0.4f);
			Assert.That(_cap.Depth01, Is.EqualTo(0.6f).Within(0.01f));
			Assert.That(_cap.IsPressed, Is.True);

			// 30% down: past the release threshold
			Run(0.5f, TravelMeters * 0.7f);
			Assert.That(_cap.Depth01, Is.EqualTo(0.3f).Within(0.01f));
			Assert.That(_cap.IsPressed, Is.False);
		}

		[Test]
		public void ApplyHand_HandFromUnderneath_DoesNotPush()
		{
			// Well below the cap's top: a hand that came in from the side or from under the table
			Run(0.5f, -0.05f);

			Assert.That(_cap.HeightMeters, Is.EqualTo(TravelMeters));
			Assert.That(_cap.IsInContact, Is.False);
		}

		[Test]
		public void ApplyHand_PushStarted_CountsThroughTheBottomStop()
		{
			PushToBottom();

			Run(0.2f, -0.05f);

			Assert.That(_cap.HeightMeters, Is.EqualTo(0f));
			Assert.That(_cap.IsInContact, Is.True);
		}

		[Test]
		public void Spring_NoTimePassing_HoldsTheCapStill()
		{
			PushToBottom();

			_cap.Spring(0f);

			Assert.That(_cap.HeightMeters, Is.EqualTo(0f));
		}

		[Test]
		public void Latch_HandLetsGo_StaysDownUntilUnlatched()
		{
			PushToBottom();
			_cap.Latch();

			Run(0.5f, NoHand);
			Assert.That(_cap.HeightMeters, Is.EqualTo(0f));
			Assert.That(_cap.IsPressed, Is.True);

			_cap.Unlatch();
			Run(0.5f, NoHand);
			Assert.That(_cap.HeightMeters, Is.EqualTo(TravelMeters).Within(0.001f));
			Assert.That(_cap.IsPressed, Is.False);
		}

		#endregion

		#region Private

		// A fingertip coming down onto the cap's top and on through its travel over a third of a second
		private void PushToBottom()
		{
			const int frames = 30;
			for (int frame = 1; frame <= frames; frame++)
			{
				Step(TravelMeters * (1f - (float)frame / frames));
			}
		}

		private void Run(float seconds, float handUndersideMeters)
		{
			for (float elapsed = 0f; elapsed < seconds; elapsed += FrameSeconds)
			{
				Step(handUndersideMeters);
			}
		}

		// One frame, in the button's order: spring, then the hand
		private void Step(float handUndersideMeters)
		{
			_cap.Spring(FrameSeconds);
			_cap.ApplyHand(handUndersideMeters);
		}

		#endregion
	}
}
