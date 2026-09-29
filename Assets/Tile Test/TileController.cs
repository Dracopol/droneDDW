using System;
using UnityEngine;

namespace DroneDDW.Tiles
{
	// Tracks which tile the player is standing on, using the headset's position on the floor
	internal class TileController : MonoBehaviour
	{
		#region Events

		// Passes null when the player steps off every tile
		internal event Action<Tile> OnOccupiedTileChange;

		#endregion

		#region Serialized fields

		[SerializeField] private Transform _head;
		[SerializeField] private Tile[] _tiles;

		#endregion

		#region Fields

		private Tile _occupiedTile;

		#endregion

		#region Update

		private void Update()
		{
			Tile tileUnderHead = FindTileAt(_head.position);
			if (tileUnderHead == _occupiedTile)
			{
				return;
			}

			if (_occupiedTile != null)
			{
				_occupiedTile.SetOccupied(false);
			}

			if (tileUnderHead != null)
			{
				tileUnderHead.SetOccupied(true);
			}

			_occupiedTile = tileUnderHead;
			Debug.Log($"[DDW Tiles] Standing on {(tileUnderHead != null ? tileUnderHead.name : "no tile")}", this);
			OnOccupiedTileChange?.Invoke(tileUnderHead);
		}

		#endregion

		#region Private

		private Tile FindTileAt(Vector3 worldPosition)
		{
			foreach (Tile tile in _tiles)
			{
				if (tile.ContainsFloorPoint(worldPosition))
				{
					return tile;
				}
			}

			return null;
		}

		#endregion
	}
}
