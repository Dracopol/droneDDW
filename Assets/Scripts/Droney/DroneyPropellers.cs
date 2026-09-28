using UnityEngine;

namespace DroneDDW.Droney
{
	// Spins the propellers around Droney's up axis, diagonal pairs turn opposite ways like on a real drone
	internal class DroneyPropellers : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Renderer[] _clockwisePropellers;
		[SerializeField] private Renderer[] _counterClockwisePropellers;
		[SerializeField] private float _degreesPerSecond = 1440f;

		#endregion

		#region Fields

		// Each propeller's hub, in its own space, so the spin doesn't depend on where the mesh's pivot is
		private Vector3[] _clockwiseHubs;
		private Vector3[] _counterClockwiseHubs;

		#endregion

		#region Setup

		private void Awake()
		{
			_clockwiseHubs = GetHubs(_clockwisePropellers);
			_counterClockwiseHubs = GetHubs(_counterClockwisePropellers);
		}

		#endregion

		#region Update

		private void Update()
		{
			float degrees = _degreesPerSecond * Time.deltaTime;
			Spin(_clockwisePropellers, _clockwiseHubs, degrees);
			Spin(_counterClockwisePropellers, _counterClockwiseHubs, -degrees);
		}

		#endregion

		#region Private

		private static Vector3[] GetHubs(Renderer[] propellers)
		{
			Vector3[] hubs = new Vector3[propellers.Length];

			for (int index = 0; index < propellers.Length; index++)
				hubs[index] = propellers[index].transform.InverseTransformPoint(propellers[index].bounds.center);

			return hubs;
		}

		private void Spin(Renderer[] propellers, Vector3[] hubs, float degrees)
		{
			for (int index = 0; index < propellers.Length; index++)
			{
				Transform propeller = propellers[index].transform;
				propeller.RotateAround(propeller.TransformPoint(hubs[index]), transform.up, degrees);
			}
		}

		#endregion
	}
}
