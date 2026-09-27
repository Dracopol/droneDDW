using UnityEngine;

namespace DroneDDW.Interaction
{
	// Puts a dropped object back where it started, passthrough has no floor so it would fall forever
	internal class StampRespawner : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Rigidbody _rigidbody;
		[SerializeField] private GrabMonitor _grabMonitor;

		[Tooltip("Respawns once it's this far below where it started and nobody holds it")]
		[Min(0f)]
		[SerializeField] private float _dropDistanceMeters = 0.5f;

		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _respawnClip;

		#endregion

		#region Fields

		private Pose _startPose;

		#endregion

		#region Setup

		private void Awake()
		{
			_startPose = new Pose(_rigidbody.position, _rigidbody.rotation);
		}

		#endregion

		#region Update

		private void FixedUpdate()
		{
			if (_grabMonitor.IsHeld || _rigidbody.position.y > _startPose.position.y - _dropDistanceMeters)
				return;

			Respawn();
		}

		#endregion

		#region Private

		private void Respawn()
		{
			_rigidbody.linearVelocity = Vector3.zero;
			_rigidbody.angularVelocity = Vector3.zero;
			_rigidbody.position = _startPose.position;
			_rigidbody.rotation = _startPose.rotation;
			_rigidbody.transform.SetPositionAndRotation(_startPose.position, _startPose.rotation);

			if (_audioSource != null && _respawnClip != null)
				_audioSource.PlayOneShot(_respawnClip);

			Debug.Log($"[DDW Stamp] {_rigidbody.name} respawned", this);
		}

		#endregion
	}
}
