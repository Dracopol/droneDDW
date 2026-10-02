using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Interaction
{
	// Marks papers it's pressed onto, but only armed papers and only a deliberate, face-down stamp while held
	internal class Stamp : MonoBehaviour
	{
		#region Events

		internal event Action<Paper> OnStamp;

		#endregion

		#region Serialized fields

		[SerializeField] private GrabMonitor _grabMonitor;

		[Tooltip("The stamp's face, its down direction has to point at the paper")]
		[SerializeField] private Transform _face;

		[Tooltip("How far the face may tilt away from pointing straight down")]
		[Range(0f, 90f)]
		[SerializeField] private float _maxTiltDegrees = 35f;

		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _stampClip;

		[Tooltip("Plays when the stamp touches an armed paper but doesn't count, so the player knows to try again")]
		[SerializeField] private AudioClip _rejectedClip;

		#endregion

		#region Fields

		private readonly List<Paper> _armedPapers = new();

		#endregion

		#region Internal

		// Only these papers can be stamped until the next Arm or Disarm
		internal void Arm(IReadOnlyList<Paper> papers)
		{
			_armedPapers.Clear();
			_armedPapers.AddRange(papers);
		}

		internal void Disarm()
		{
			_armedPapers.Clear();
		}

		// Called by the trigger zone on the stamp's face when it touches something
		internal void HandleFaceContact(Collider other, Vector3 facePosition)
		{
			Paper paper = _armedPapers.Find(candidate => candidate.Collider == other);

			if (paper == null)
				return;

			if (!_grabMonitor.IsHeld)
			{
				Reject(paper, "the stamp isn't held");
				return;
			}

			float tiltDegrees = Vector3.Angle(-_face.up, Vector3.down);

			if (tiltDegrees > _maxTiltDegrees)
			{
				Reject(paper, $"tilted {tiltDegrees:0} degrees");
				return;
			}

			paper.ShowMark(facePosition, _face.eulerAngles.y);
			_audioSource.PlayOneShot(_stampClip);

			Debug.Log($"[DDW Stamp] Stamped {paper.name}", this);
			OnStamp?.Invoke(paper);
		}

		#endregion

		#region Private

		private void Reject(Paper paper, string reason)
		{
			_audioSource.PlayOneShot(_rejectedClip);
			Debug.Log($"[DDW Stamp] Ignored {paper.name}: {reason}", this);
		}

		#endregion
	}
}
