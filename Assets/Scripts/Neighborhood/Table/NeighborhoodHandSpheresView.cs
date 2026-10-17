#if NEIGHBORHOOD_XRI
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// Testing aid: draws the spheres the hands push the slider with, at their real size (bone radius plus the touch
	/// margin), in one colour and the ones touching the knob in another, plus the knob's two boxes: the push box the
	/// spheres push against and the grab box the finger grab reaches for. Tune them all in the shared push settings
	/// (Neighborhood Push Settings) while it draws. Draws instanced meshes, so it adds no objects or colliders;
	/// switch the component off to hide everything.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Hand Spheres View")]
	public sealed class NeighborhoodHandSpheresView : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private NeighborhoodSliderHandDriver _driver;

		[SerializeField, Tooltip("A sphere 1 m across, such as Unity's built-in sphere.")]
		private Mesh _sphereMesh;

		[SerializeField, Tooltip("A cube 1 m across, such as Unity's built-in cube.")]
		private Mesh _boxMesh;

		[SerializeField, Tooltip("A transparent material with GPU instancing on and a _BaseColor property.")]
		private Material _material;

		[Header("Spheres")]
		[SerializeField] private Color _idleColor = new Color(0.2f, 0.75f, 1f, 0.3f);
		[SerializeField] private Color _touchingColor = new Color(1f, 0.45f, 0.1f, 0.6f);

		[Header("Knob boxes")]
		[SerializeField, Tooltip("Draws the box the spheres push against and the box the finger grab reaches for.")]
		private bool _isShowingBoxes = true;

		[SerializeField] private Color _pushBoxColor = new Color(1f, 0.85f, 0.2f, 0.2f);
		[SerializeField] private Color _grabBoxColor = new Color(0.3f, 1f, 0.45f, 0.12f);

		#endregion

		#region Fields

		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

		private readonly List<NeighborhoodHandSphere> _spheres = new List<NeighborhoodHandSphere>();
		private readonly List<Matrix4x4> _idleMatrices = new List<Matrix4x4>();
		private readonly List<Matrix4x4> _touchingMatrices = new List<Matrix4x4>();
		private readonly List<Matrix4x4> _pushBoxMatrices = new List<Matrix4x4>();
		private readonly List<Matrix4x4> _grabBoxMatrices = new List<Matrix4x4>();
		private MaterialPropertyBlock _idleProperties;
		private MaterialPropertyBlock _touchingProperties;
		private MaterialPropertyBlock _pushBoxProperties;
		private MaterialPropertyBlock _grabBoxProperties;

		#endregion

		#region Update

		private void LateUpdate()
		{
			if (_driver == null || _material == null)
			{
				return;
			}

			DrawSpheres();
			if (_isShowingBoxes)
			{
				DrawBoxes();
			}
		}

		#endregion

		#region Internal

		internal void Configure(NeighborhoodSliderHandDriver driver, Mesh sphereMesh, Mesh boxMesh, Material material)
		{
			_driver = driver;
			_sphereMesh = sphereMesh;
			_boxMesh = boxMesh;
			_material = material;
		}

		#endregion

		#region Private

		private void DrawSpheres()
		{
			if (_sphereMesh == null)
			{
				return;
			}

			_driver.CollectSpheres(_spheres);
			_idleMatrices.Clear();
			_touchingMatrices.Clear();
			foreach (NeighborhoodHandSphere sphere in _spheres)
			{
				Matrix4x4 matrix = Matrix4x4.TRS(sphere.WorldPosition, Quaternion.identity,
					Vector3.one * (sphere.RadiusMeters * 2f));
				(sphere.IsTouching ? _touchingMatrices : _idleMatrices).Add(matrix);
			}

			Draw(_sphereMesh, _idleMatrices, ref _idleProperties, _idleColor);
			Draw(_sphereMesh, _touchingMatrices, ref _touchingProperties, _touchingColor);
		}

		private void DrawBoxes()
		{
			if (_boxMesh == null)
			{
				return;
			}

			_pushBoxMatrices.Clear();
			_grabBoxMatrices.Clear();
			if (_driver.TryGetPushBoxMatrix(out Matrix4x4 pushBox))
			{
				_pushBoxMatrices.Add(pushBox);
			}

			if (_driver.TryGetGrabBoxMatrix(out Matrix4x4 grabBox))
			{
				_grabBoxMatrices.Add(grabBox);
			}

			Draw(_boxMesh, _pushBoxMatrices, ref _pushBoxProperties, _pushBoxColor);
			Draw(_boxMesh, _grabBoxMatrices, ref _grabBoxProperties, _grabBoxColor);
		}

		private void Draw(Mesh mesh, List<Matrix4x4> matrices, ref MaterialPropertyBlock properties, Color color)
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
			Graphics.RenderMeshInstanced(renderParams, mesh, 0, matrices);
		}

		#endregion
	}
}
#endif
