using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.Admin.Tests
{
	// Button centres as the menu sees them: mockup pixels, y up (so lower buttons have lower y)
	internal sealed class MenuNavigationTests
	{
		#region Fields

		// The Paused screen's five buttons, 104 px apart
		private static readonly List<Vector2> PauseList = new()
		{
			new(0f, 0f), new(0f, -104f), new(0f, -208f), new(0f, -312f), new(0f, -416f),
		};

		// The history: Go back top left, then the rows' Delete buttons far to the right, 68 px apart
		private static readonly List<Vector2> History = new()
		{
			new(-571f, 413f), new(560f, 254f), new(560f, 186f), new(560f, 118f),
		};

		// A dialog's Cancel and confirm buttons side by side
		private static readonly List<Vector2> Dialog = new() { new(-170f, 0f), new(170f, 0f) };

		#endregion

		#region Tests

		[TestCase(0, MenuDirection.Down, 1)]
		[TestCase(2, MenuDirection.Down, 3)]
		[TestCase(3, MenuDirection.Up, 2)]
		[TestCase(4, MenuDirection.Down, -1)]
		[TestCase(0, MenuDirection.Up, -1)]
		[TestCase(1, MenuDirection.Left, -1)]
		[TestCase(1, MenuDirection.Right, -1)]
		public void List_MovesOneStepWithoutWrapping(int from, MenuDirection direction, int expected)
		{
			Assert.AreEqual(expected, MenuNavigationUtil.FindNeighbor(PauseList, from, direction));
		}

		[TestCase(0, MenuDirection.Down, 1)]
		[TestCase(1, MenuDirection.Up, 0)]
		[TestCase(1, MenuDirection.Down, 2)]
		[TestCase(3, MenuDirection.Up, 2)]
		[TestCase(3, MenuDirection.Down, -1)]
		public void History_ReachesGoBackAndTheRows(int from, MenuDirection direction, int expected)
		{
			Assert.AreEqual(expected, MenuNavigationUtil.FindNeighbor(History, from, direction));
		}

		[TestCase(0, MenuDirection.Right, 1)]
		[TestCase(1, MenuDirection.Left, 0)]
		[TestCase(0, MenuDirection.Left, -1)]
		[TestCase(0, MenuDirection.Up, -1)]
		[TestCase(1, MenuDirection.Down, -1)]
		public void Dialog_MovesSideways(int from, MenuDirection direction, int expected)
		{
			Assert.AreEqual(expected, MenuNavigationUtil.FindNeighbor(Dialog, from, direction));
		}

		#endregion
	}
}
