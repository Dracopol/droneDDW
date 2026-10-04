using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.Admin.Tests
{
	// With the default settings: press at 0.6, release under 0.35, repeat after 0.4 s every 0.13 s
	internal sealed class StickRepeatTests
	{
		#region Fields

		private AdminMenuSettings _settings;
		private StickRepeat _stick;

		#endregion

		#region Setup

		[SetUp]
		public void Create()
		{
			_settings = ScriptableObject.CreateInstance<AdminMenuSettings>();
			_stick = new StickRepeat();
		}

		[TearDown]
		public void Destroy()
		{
			Object.DestroyImmediate(_settings);
		}

		#endregion

		#region Tests

		[Test]
		public void Push_MovesOnce()
		{
			Assert.IsTrue(_stick.TryRead(new Vector2(0f, -0.8f), 0f, _settings, out MenuDirection direction));
			Assert.AreEqual(MenuDirection.Down, direction);
			Assert.IsFalse(_stick.TryRead(new Vector2(0f, -0.8f), 0.1f, _settings, out _));
		}

		[Test]
		public void SmallPush_DoesNothing()
		{
			Assert.IsFalse(_stick.TryRead(new Vector2(0.5f, 0f), 0f, _settings, out _));
		}

		[Test]
		public void Hold_RepeatsAfterTheDelay()
		{
			Vector2 down = new(0f, -1f);
			_stick.TryRead(down, 0f, _settings, out _);

			Assert.IsFalse(_stick.TryRead(down, 0.39f, _settings, out _));
			Assert.IsTrue(_stick.TryRead(down, 0.41f, _settings, out _));
			Assert.IsFalse(_stick.TryRead(down, 0.5f, _settings, out _));
			Assert.IsTrue(_stick.TryRead(down, 0.55f, _settings, out _));
		}

		[Test]
		public void Release_ReArms()
		{
			_stick.TryRead(new Vector2(0f, 1f), 0f, _settings, out _);

			// Still above the release threshold: no new move
			Assert.IsFalse(_stick.TryRead(new Vector2(0f, 0.4f), 0.05f, _settings, out _));
			Assert.IsFalse(_stick.TryRead(new Vector2(0f, 0.2f), 0.1f, _settings, out _));
			Assert.IsTrue(_stick.TryRead(new Vector2(0f, 0.9f), 0.15f, _settings, out MenuDirection direction));
			Assert.AreEqual(MenuDirection.Up, direction);
		}

		[Test]
		public void Diagonal_FollowsTheStrongerAxis()
		{
			Assert.IsTrue(_stick.TryRead(new Vector2(0.9f, 0.7f), 0f, _settings, out MenuDirection direction));
			Assert.AreEqual(MenuDirection.Right, direction);
		}

		[Test]
		public void TurningToAnotherDirection_MovesAtOnce()
		{
			_stick.TryRead(new Vector2(0f, -1f), 0f, _settings, out _);

			Assert.IsTrue(_stick.TryRead(new Vector2(-1f, 0f), 0.05f, _settings, out MenuDirection direction));
			Assert.AreEqual(MenuDirection.Left, direction);
		}

		[Test]
		public void Clear_NeedsTheStickBackFirst()
		{
			_stick.TryRead(new Vector2(0f, -1f), 0f, _settings, out _);
			_stick.Clear();

			Assert.IsTrue(_stick.TryRead(new Vector2(0f, -1f), 0.05f, _settings, out _));
		}

		#endregion
	}
}
