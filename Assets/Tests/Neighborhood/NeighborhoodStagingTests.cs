using DroneDDW.Neighborhood.Characters;
using DroneDDW.Neighborhood.Editor.Staging;
using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.Neighborhood.Tests
{
	internal sealed class NeighborhoodStagingTests
	{
		#region Fields

		private const float Tolerance = 1e-4f;

		#endregion

		#region Tests

		[Test]
		public void YawFacing_LooksAtTheTarget()
		{
			Assert.That(NeighborhoodStagingMove.YawFacing(Vector3.zero, Vector3.forward), Is.EqualTo(0f).Within(Tolerance));
			Assert.That(NeighborhoodStagingMove.YawFacing(Vector3.zero, Vector3.right), Is.EqualTo(90f).Within(Tolerance));
			Assert.That(NeighborhoodStagingMove.YawFacing(new Vector3(0f, 0f, 5f), Vector3.zero), Is.EqualTo(180f).Within(Tolerance));
		}

		[Test]
		public void Move_PersonLandsOnTheSpot()
		{
			NeighborhoodStagingMove move = new NeighborhoodStagingMove(new Vector3(-12f, 0f, 11f), 135f, new Vector3(-3f, 0f, 7f), 200f);

			Assert.That(Vector3.Distance(move.MovePoint(new Vector3(-12f, 0f, 11f)), new Vector3(-3f, 0f, 7f)), Is.LessThan(Tolerance));
			Assert.That(move.MoveRotation(Quaternion.Euler(0f, 135f, 0f)).eulerAngles.y, Is.EqualTo(200f).Within(1e-3f));
		}

		[Test]
		public void Move_PropsKeepTheirPlaceAroundThePerson()
		{
			Vector3 person = new Vector3(-14f, 0f, -10f);
			Quaternion facing = Quaternion.Euler(0f, 54f, 0f);
			Vector3 lamp = person + facing * new Vector3(-1.5f, 0f, 0.8f) + Vector3.up * 5.8f;
			NeighborhoodStagingMove move = new NeighborhoodStagingMove(person, 54f, new Vector3(2f, 0f, 10f), -169f);

			Vector3 movedLamp = move.MovePoint(lamp);
			Vector3 offsetSeenByThePerson = Quaternion.Inverse(Quaternion.Euler(0f, -169f, 0f)) * (movedLamp - new Vector3(2f, 0f, 10f));

			Assert.That(Vector3.Distance(offsetSeenByThePerson, new Vector3(-1.5f, 5.8f, 0.8f)), Is.LessThan(Tolerance));
		}

		[Test]
		public void Move_MatrixMovesRoutesLikeProps()
		{
			NeighborhoodStagingMove move = new NeighborhoodStagingMove(new Vector3(7f, 0f, 1f), 270f, new Vector3(4f, 0f, 6f), 210f);
			Vector3 point = new Vector3(6f, 9f, 2f);

			Assert.That(Vector3.Distance(move.Matrix.MultiplyPoint3x4(point), move.MovePoint(point)), Is.LessThan(Tolerance));
		}

		[Test]
		public void DefaultSpots_AreInFrontOfTheSeat()
		{
			foreach (NeighborhoodStagingSpot spot in NeighborhoodStaging.CreateDefaultSpots())
			{
				Vector3 place = spot.Position;
				float bearing = Mathf.Atan2(place.x, place.z) * Mathf.Rad2Deg;
				float distance = new Vector2(place.x, place.z).magnitude;

				Assert.That(Mathf.Abs(bearing), Is.LessThanOrEqualTo(45f), $"{spot.Character} is {bearing:0}° off straight ahead");
				Assert.That(distance, Is.InRange(5f, 12f), $"{spot.Character} is {distance:0.0} m from the seat");
			}
		}

		[Test]
		public void TryComputeTurn_TargetToTheRight_TurnsRight()
		{
			bool isTurning = NeighborhoodCharacterLook.TryComputeTurn(Vector3.forward, Quaternion.Euler(0f, 30f, 0f) * Vector3.forward,
				70f, 20f, out float yaw, out float pitch);

			Assert.That(isTurning, Is.True);
			Assert.That(yaw, Is.EqualTo(30f).Within(1e-3f));
			Assert.That(pitch, Is.EqualTo(0f).Within(1e-3f));
		}

		[Test]
		public void TryComputeTurn_TargetBehindAndAbove_StopsAtTheLimits()
		{
			NeighborhoodCharacterLook.TryComputeTurn(Vector3.forward, new Vector3(-1f, 2f, -0.2f), 70f, 20f, out float yaw, out float pitch);

			Assert.That(yaw, Is.EqualTo(-70f).Within(1e-3f));
			Assert.That(pitch, Is.EqualTo(-20f).Within(1e-3f));
		}

		[Test]
		public void TryComputeTurn_FacingStraightDown_DoesNotTurn()
		{
			Assert.That(NeighborhoodCharacterLook.TryComputeTurn(Vector3.down, Vector3.forward, 70f, 20f, out _, out _), Is.False);
		}

		#endregion
	}
}
