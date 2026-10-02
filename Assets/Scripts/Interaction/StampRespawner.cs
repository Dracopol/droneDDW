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

		[Tooltip("Also respawns once it has lain this far from where it started, measured flat, e.g. across the table")]
		[Min(0f)]
		[SerializeField] private float _maxDistanceMeters = 0.7f;

		[Tooltip("How long it has to lie too far away first, so a stamp that's just sliding isn't snatched back")]
		[Min(0f)]
		[SerializeField] private float _tooFarSeconds = 1.5f;

		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _respawnClip;

		#endregion

		#region Fields

		private Pose _startPose;
		private float _tooFarElapsedSeconds;

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
			if (_grabMonitor.IsHeld)
			{
				_tooFarElapsedSeconds = 0f;
				return;
			}

			if (_rigidbody.position.y < _startPose.position.y - _dropDistanceMeters)
			{
				Respawn();
				return;
			}

			_tooFarElapsedSeconds = IsTooFar() ? _tooFarElapsedSeconds + Time.fixedDeltaTime : 0f;

			if (_tooFarElapsedSeconds >= _tooFarSeconds)
				Respawn();
		}

		#endregion

		#region Private

		private bool IsTooFar()
		{
			Vector3 offset = _rigidbody.position - _startPose.position;
			offset.y = 0f;
			return offset.magnitude > _maxDistanceMeters;
		}

		private void Respawn()
		{
			_tooFarElapsedSeconds = 0f;
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
