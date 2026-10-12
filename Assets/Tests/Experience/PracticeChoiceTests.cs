using NUnit.Framework;

namespace DroneDDW.Experience.Tests
{
	internal sealed class PracticeChoiceTests
	{
		#region Tests

		// The slider's notches show the orb's look A, its centre and look B
		[TestCase(0, 0)]
		[TestCase(1, 2)]
		[TestCase(2, 4)]
		[TestCase(-1, 0)]
		[TestCase(3, 4)]
		public void ToOrbStep_Detent_IsTheOrbsStep(int detent, int expected)
		{
			Assert.That(PracticeChoice.ToOrbStep(detent), Is.EqualTo(expected));
		}

		#endregion
	}
}
