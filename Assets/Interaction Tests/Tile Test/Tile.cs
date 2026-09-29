using UnityEngine;

namespace DroneDDW.Tiles
{
	// A floor tile the player can stand on, shows the occupied material while they are on it
	internal class Tile : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Renderer _visual;
		[SerializeField] private Material _occupiedMaterial;

		#endregion

		#region Fields

		private Transform _visualTransform;
		private Material _idleMaterial;

		#endregion

		#region Setup

		private void Awake()
		{
			_visualTransform = _visual.transform;
			_idleMaterial = _visual.sharedMaterial;
		}

		#endregion

		#region Internal

		// Checks against the visual's footprint in its own space, so height is ignored and rotated tiles still work
		internal bool ContainsFloorPoint(Vector3 worldPosition)
		{
			Vector3 localPosition = _visualTransform.InverseTransformPoint(worldPosition);
			Bounds localBounds = _visual.localBounds;

			return localPosition.x >= localBounds.min.x && localPosition.x <= localBounds.max.x
				&& localPosition.z >= localBounds.min.z && localPosition.z <= localBounds.max.z;
		}

		internal void SetOccupied(bool isOccupied)
		{
			_visual.sharedMaterial = isOccupied ? _occupiedMaterial : _idleMaterial;
		}

		#endregion
	}
}
