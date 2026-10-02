using System;
using UnityEngine;

namespace DroneDDW.Tutorial
{
	// Something the tutorial points out when the player is stuck: the hint marker floats above it and Droney flies over
	internal class HintTarget : MonoBehaviour
	{
		#region Events

		// Raised each time a stuck hint for this target starts
		internal event Action OnHintBegin;

		#endregion

		#region Serialized fields

		[Tooltip("How high above this object the hint marker floats")]
		[Min(0f)]
		[SerializeField] private float _markerHeightMeters = 0.15f;

		#endregion

		#region Properties

		internal Vector3 Position => transform.position;
		internal float MarkerHeightMeters => _markerHeightMeters;

		#endregion

		#region Internal

		internal void BeginHint()
		{
			OnHintBegin?.Invoke();
		}

		#endregion
	}
}
