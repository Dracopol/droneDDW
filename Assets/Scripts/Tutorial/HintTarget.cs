using UnityEngine;

namespace DroneDDW.Tutorial
{
	// Something the tutorial points at when the player is stuck
	internal class HintTarget : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Shown while the hint is active, e.g. a glow around the stamp or ghost hands")]
		[SerializeField] private GameObject _highlight;

		#endregion

		#region Setup

		private void Awake()
		{
			SetHighlighted(false);
		}

		#endregion

		#region Internal

		internal void SetHighlighted(bool isHighlighted)
		{
			if (_highlight != null)
				_highlight.SetActive(isHighlighted);
		}

		#endregion
	}
}
