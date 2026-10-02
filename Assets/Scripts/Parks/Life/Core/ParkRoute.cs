using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>A path through the park as a line of points: a closed loop, or a line driven back and forth.</summary>
	[Serializable]
	public sealed class ParkRoute
	{
		#region Serialized fields

		[SerializeField] private Vector3[] _points = Array.Empty<Vector3>();
		[SerializeField] private bool _isLoop;

		#endregion

		#region Fields

		[NonSerialized] private float[] _distances;

		#endregion

		#region Properties

		public int Count => _points.Length;
		public bool IsLoop => _isLoop;

		public float Length
		{
			get
			{
				EnsureDistances();
				return _distances[_distances.Length - 1];
			}
		}

		#endregion

		#region Setup

		public ParkRoute(Vector3[] points, bool isLoop)
		{
			_points = points ?? Array.Empty<Vector3>();
			_isLoop = isLoop;
		}

		#endregion

		#region Public

		public Vector3 this[int index] => _points[((index % _points.Length) + _points.Length) % _points.Length];

		/// <summary>The point at a distance along the route, and the direction it runs there.</summary>
		public Vector3 Evaluate(float distance, out Vector3 direction)
		{
			if (_points.Length == 0)
			{
				direction = Vector3.forward;
				return Vector3.zero;
			}

			EnsureDistances();
			float length = Length;
			distance = _isLoop ? Mathf.Repeat(distance, length) : Mathf.Clamp(distance, 0f, length);
			int segment = 1;
			while (segment < _distances.Length - 1 && _distances[segment] < distance)
			{
				segment++;
			}

			Vector3 from = this[segment - 1], to = this[segment];
			float span = _distances[segment] - _distances[segment - 1];
			direction = span > 0f ? (to - from) / span : Vector3.forward;
			return span > 0f ? Vector3.Lerp(from, to, (distance - _distances[segment - 1]) / span) : from;
		}

		/// <summary>Draws the route as lines, for a component's scene-view gizmos.</summary>
		public void DrawGizmo()
		{
			int segments = _isLoop ? _points.Length : _points.Length - 1;
			for (int i = 0; i < segments; i++)
			{
				Gizmos.DrawLine(this[i], this[i + 1]);
			}
		}

		/// <summary>Index of the route point nearest to a position.</summary>
		public int Nearest(Vector3 position)
		{
			int best = 0;
			for (int i = 1; i < _points.Length; i++)
			{
				if ((_points[i] - position).sqrMagnitude < (_points[best] - position).sqrMagnitude)
				{
					best = i;
				}
			}

			return best;
		}

		#endregion

		#region Private

		private void EnsureDistances()
		{
			int count = _points.Length + (_isLoop ? 1 : 0);
			if (_distances != null && _distances.Length == Mathf.Max(1, count))
			{
				return;
			}

			_distances = new float[Mathf.Max(1, count)];
			for (int i = 1; i < count; i++)
			{
				_distances[i] = _distances[i - 1] + Vector3.Distance(this[i - 1], this[i]);
			}
		}

		#endregion
	}
}
