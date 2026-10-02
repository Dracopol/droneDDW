using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Feeds the park's water shader (DroneDDW/Parks/Water): small waves always, and up to eight expanding ripple rings
	/// started wherever something touches the water (a fish, a frog, a duck's wake, the fountain). The oldest ring makes
	/// way for a new one. Globals, so the water material keeps URP Lit's own per-material data.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Water Surface")]
	public sealed class ParkWaterSurface : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField, Min(0.05f), Tooltip("How fast a ring spreads, metres per second.")]
		private float _ringSpeedMetersPerSecond = 0.6f;

		[SerializeField, Min(0.1f)] private float _ringSeconds = 3f;
		[SerializeField, Min(0.02f)] private float _ringWavelengthMeters = 0.25f;

		[SerializeField, Range(0f, 2f), Tooltip("Strength of the small waves that always move across the water.")]
		private float _waves = 1f;

		#endregion

		#region Fields

		private const int Rings = 8;

		private static readonly int RipplesId = Shader.PropertyToID("_ParkRipples");
		private static readonly int StrengthsId = Shader.PropertyToID("_ParkRippleStrengths");
		private static readonly int ShapeId = Shader.PropertyToID("_ParkWaterShape");

		private readonly Vector4[] _ripples = new Vector4[Rings];
		private readonly float[] _strengths = new float[Rings];
		private int _next;
		private bool _isDirty = true;

		#endregion

		#region Setup

		private void OnEnable()
		{
			Shader.SetGlobalVector(ShapeId, new Vector4(_ringSpeedMetersPerSecond, _ringSeconds, _ringWavelengthMeters, _waves));
			_isDirty = true;
		}

		private void OnDisable()
		{
			// Leave the water still for whatever renders it next
			for (int i = 0; i < Rings; i++)
			{
				_strengths[i] = 0f;
			}

			Shader.SetGlobalFloatArray(StrengthsId, _strengths);
			Shader.SetGlobalVector(ShapeId, Vector4.zero);
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			if (!_isDirty)
			{
				return;
			}

			_isDirty = false;
			Shader.SetGlobalVectorArray(RipplesId, _ripples);
			Shader.SetGlobalFloatArray(StrengthsId, _strengths);
		}

		#endregion

		#region Public

		/// <summary>Starts a ring on the water. Strength 1 is a fish or frog; a duck's wake is weaker.</summary>
		public void AddRipple(Vector3 position, float strength)
		{
			_ripples[_next] = new Vector4(position.x, position.y, position.z, Time.timeSinceLevelLoad);
			_strengths[_next] = strength;
			_next = (_next + 1) % Rings;
			_isDirty = true;
		}

		#endregion
	}
}
