using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// One value for each of the five park levels, from full nature to full efficiency. Evaluated at a fractional
	/// level, so life eases from one level's value to the next while the park changes.
	/// </summary>
	[Serializable]
	public struct ParkLevelValues
	{
		#region Serialized fields

		[SerializeField] private float _fullNature;
		[SerializeField] private float _natureSkewed;
		[SerializeField] private float _balanced;
		[SerializeField] private float _efficiencySkewed;
		[SerializeField] private float _fullEfficiency;

		#endregion

		#region Setup

		public ParkLevelValues(float fullNature, float natureSkewed, float balanced, float efficiencySkewed,
			float fullEfficiency)
		{
			_fullNature = fullNature;
			_natureSkewed = natureSkewed;
			_balanced = balanced;
			_efficiencySkewed = efficiencySkewed;
			_fullEfficiency = fullEfficiency;
		}

		#endregion

		#region Public

		/// <summary>The value at a level from 0 to 4, blending the two whole levels around it.</summary>
		public float Evaluate(float level)
		{
			level = Mathf.Clamp(level, 0f, 4f);
			int below = Mathf.FloorToInt(level);
			int above = Mathf.Min(4, below + 1);
			return Mathf.Lerp(At(below), At(above), level - below);
		}

		#endregion

		#region Private

		private float At(int level)
		{
			switch (level)
			{
				case 0:
					return _fullNature;
				case 1:
					return _natureSkewed;
				case 2:
					return _balanced;
				case 3:
					return _efficiencySkewed;
				default:
					return _fullEfficiency;
			}
		}

		#endregion
	}
}
