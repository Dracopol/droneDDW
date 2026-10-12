using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// Two small arrows either side of the slider's handle that bob outwards, showing it slides left and right. Hidden
	/// until <see cref="Show"/>; the experience shows them on the practice table until the slider has been moved once.
	/// The arrows ride along with the handle.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Slider Hint")]
	public sealed class NeighborhoodSliderHint : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField, Tooltip("The arrows: the one pointing left first, then the one pointing right.")]
		private Transform[] _arrows = new Transform[0];

		[SerializeField, Min(0f), Tooltip("How far the arrows bob outwards.")]
		private float _bobMeters = 0.008f;

		[SerializeField, Min(0.05f), Tooltip("Bobs per second.")]
		private float _bobHertz = 1.2f;

		#endregion

		#region Fields

		private Vector3[] _restPositions;

		#endregion

		#region Setup

		private void Awake()
		{
			_restPositions = new Vector3[_arrows.Length];
			for (int arrow = 0; arrow < _arrows.Length; arrow++)
			{
				_restPositions[arrow] = _arrows[arrow].localPosition;
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			float outwards = _bobMeters * (0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI * _bobHertz));
			for (int arrow = 0; arrow < _arrows.Length; arrow++)
			{
				float side = Mathf.Sign(_restPositions[arrow].x);
				_arrows[arrow].localPosition = _restPositions[arrow] + Vector3.right * (side * outwards);
			}
		}

		#endregion

		#region Public

		public void Show()
		{
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			gameObject.SetActive(false);
		}

		#endregion

		#region Internal

		internal void Configure(Transform[] arrows)
		{
			_arrows = arrows;
		}

		#endregion
	}
}
