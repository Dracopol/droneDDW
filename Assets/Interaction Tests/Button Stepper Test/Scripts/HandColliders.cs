using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Buttons
{
	// Kinematic capsules that follow one tracked hand's bones, so the hand can push rigidbodies around
	internal class HandColliders : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private HandSkeleton _hand;

		#endregion

		#region Fields

		private const float MinBoneLengthMeters = 0.001f;

		// Parallel to the hand's bones
		private readonly List<Rigidbody> _bodies = new();
		private readonly List<CapsuleCollider> _colliders = new();

		private bool _areCollidersEnabled = true;

		#endregion

		#region Setup

		// The hand skeleton builds its bones in Awake
		private void Start()
		{
			Transform parent = transform;

			foreach (HandBone bone in _hand.Bones)
			{
				GameObject boneObject = new($"{bone.Start.name} Collider");
				boneObject.transform.SetParent(parent, false);

				Rigidbody body = boneObject.AddComponent<Rigidbody>();
				body.isKinematic = true;
				body.useGravity = false;
				body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

				CapsuleCollider capsule = boneObject.AddComponent<CapsuleCollider>();
				capsule.direction = 2;
				capsule.radius = bone.RadiusMeters;

				_bodies.Add(body);
				_colliders.Add(capsule);
			}

			SetCollidersEnabled(false);
		}

		#endregion

		#region Update

		private void FixedUpdate()
		{
			if (!_hand.IsTracked)
			{
				// A lost hand's skeleton freezes in place, it mustn't keep holding a button down
				SetCollidersEnabled(false);
				return;
			}

			// Teleport the first step after tracking returns, sweeping from the old pose would slam through buttons
			bool shouldTeleport = !_areCollidersEnabled;
			SetCollidersEnabled(true);

			for (int index = 0; index < _bodies.Count; index++)
			{
				MoveToBone(_hand.Bones[index], _bodies[index], _colliders[index], shouldTeleport);
			}
		}

		#endregion

		#region Private

		private void MoveToBone(HandBone bone, Rigidbody body, CapsuleCollider capsule, bool shouldTeleport)
		{
			Vector3 start = bone.Start.position;
			Vector3 end = bone.End.position;
			Vector3 direction = end - start;
			float lengthMeters = direction.magnitude;

			if (lengthMeters < MinBoneLengthMeters)
			{
				return;
			}

			Vector3 center = (start + end) * 0.5f;
			Quaternion rotation = Quaternion.LookRotation(direction / lengthMeters);
			capsule.height = lengthMeters + 2f * bone.RadiusMeters;

			if (shouldTeleport)
			{
				body.position = center;
				body.rotation = rotation;
				return;
			}

			body.MovePosition(center);
			body.MoveRotation(rotation);
		}

		private void SetCollidersEnabled(bool isEnabled)
		{
			if (_areCollidersEnabled == isEnabled)
			{
				return;
			}

			_areCollidersEnabled = isEnabled;

			foreach (CapsuleCollider capsule in _colliders)
			{
				capsule.enabled = isEnabled;
			}
		}

		#endregion
	}
}
