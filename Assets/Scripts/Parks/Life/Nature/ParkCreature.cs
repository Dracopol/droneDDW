using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// A small animal of the park: one skinned mesh (a single draw call) with optional wing, head and tail bones. It has
	/// no Update of its own; the wildlife systems move it and set its pose each frame: wings up or down and folded back
	/// along the body, the head pitched (pecking, dabbling), the tail swung (a fish swimming).
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Creature")]
	public sealed class ParkCreature : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private SkinnedMeshRenderer _renderer;

		[SerializeField, Tooltip("Hinged at the shoulder; the wing reaches out along -X.")]
		private Transform _wingLeft;

		[SerializeField, Tooltip("Hinged at the shoulder; the wing reaches out along +X.")]
		private Transform _wingRight;

		[SerializeField, Tooltip("Hinged at the neck.")]
		private Transform _head;

		[SerializeField] private Transform _tail;

		#endregion

		#region Properties

		public Transform Root { get; private set; }

		#endregion

		#region Setup

		private void Awake()
		{
			Root = transform;
		}

		#endregion

		#region Public

		/// <summary>
		/// Wing tips raised by an angle (negative lowers them) and folded back along the body (0 open, 1 folded).
		/// </summary>
		public void SetWings(float upDegrees, float fold)
		{
			if (_wingLeft != null)
			{
				_wingLeft.localRotation = Quaternion.AngleAxis(-90f * fold, Vector3.up)
					* Quaternion.AngleAxis(-upDegrees, Vector3.forward);
			}

			if (_wingRight != null)
			{
				_wingRight.localRotation = Quaternion.AngleAxis(90f * fold, Vector3.up)
					* Quaternion.AngleAxis(upDegrees, Vector3.forward);
			}
		}

		/// <summary>Head tipped forward (positive) or back.</summary>
		public void SetHead(float pitchDegrees)
		{
			if (_head != null)
			{
				_head.localRotation = Quaternion.AngleAxis(pitchDegrees, Vector3.right);
			}
		}

		public void SetTail(float yawDegrees)
		{
			if (_tail != null)
			{
				_tail.localRotation = Quaternion.AngleAxis(yawDegrees, Vector3.up);
			}
		}

		#endregion

		#region Internal

		internal void Configure(SkinnedMeshRenderer skinnedRenderer, Transform wingLeft, Transform wingRight, Transform head,
			Transform tail)
		{
			_renderer = skinnedRenderer;
			_wingLeft = wingLeft;
			_wingRight = wingRight;
			_head = head;
			_tail = tail;
		}

		#endregion
	}
}
