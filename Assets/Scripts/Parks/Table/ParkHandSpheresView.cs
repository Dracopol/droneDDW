#if PARKS_XRI
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// Testing aid: draws the spheres the hands push the slider with, at their real size (bone radius plus the touch
	/// margin), in one colour, and the ones touching the knob in another. Tune the radii on
	/// <see cref="ParkHandBones"/> and the margin and spacing on <see cref="ParkSliderHandDriver"/> while it shows
	/// them. Draws instanced meshes, so it adds no objects or colliders; switch the component off to hide them.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Hand Spheres View")]
	public sealed class ParkHandSpheresView : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkSliderHandDriver _driver;

		[SerializeField, Tooltip("A sphere 1 m across, such as Unity's built-in sphere.")]
		private Mesh _sphereMesh;

		[SerializeField, Tooltip("A transparent material with GPU instancing on and a _BaseColor property.")]
		private Material _material;

		[SerializeField] private Color _idleColor = new Color(0.2f, 0.75f, 1f, 0.3f);
		[SerializeField] private Color _touchingColor = new Color(1f, 0.45f, 0.1f, 0.6f);

		#endregion

		#region Fields

		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

		private readonly List<ParkHandSphere> _spheres = new List<ParkHandSphere>();
		private readonly List<Matrix4x4> _idleMatrices = new List<Matrix4x4>();
		private readonly List<Matrix4x4> _touchingMatrices = new List<Matrix4x4>();
		private MaterialPropertyBlock _idleProperties;
		private MaterialPropertyBlock _touchingProperties;

		#endregion

		#region Update

		private void LateUpdate()
		{
			if (_driver == null || _sphereMesh == null || _material == null)
			{
				return;
			}

			_driver.CollectSpheres(_spheres);
			_idleMatrices.Clear();
			_touchingMatrices.Clear();
			foreach (ParkHandSphere sphere in _spheres)
			{
				Matrix4x4 matrix = Matrix4x4.TRS(sphere.WorldPosition, Quaternion.identity,
					Vector3.one * (sphere.RadiusMeters * 2f));
				(sphere.IsTouching ? _touchingMatrices : _idleMatrices).Add(matrix);
			}

			Draw(_idleMatrices, ref _idleProperties, _idleColor);
			Draw(_touchingMatrices, ref _touchingProperties, _touchingColor);
		}

		#endregion

		#region Internal

		internal void Configure(ParkSliderHandDriver driver, Mesh sphereMesh, Material material)
		{
			_driver = driver;
			_sphereMesh = sphereMesh;
			_material = material;
		}

		#endregion

		#region Private

		private void Draw(List<Matrix4x4> matrices, ref MaterialPropertyBlock properties, Color color)
		{
			if (matrices.Count == 0)
			{
				return;
			}

			properties ??= new MaterialPropertyBlock();
			properties.SetColor(BaseColorId, color);
			RenderParams renderParams = new RenderParams(_material)
			{
				matProps = properties,
				shadowCastingMode = ShadowCastingMode.Off,
				receiveShadows = false,
				layer = gameObject.layer,
			};
			Graphics.RenderMeshInstanced(renderParams, _sphereMesh, 0, matrices);
		}

		#endregion
	}
}
#endif
