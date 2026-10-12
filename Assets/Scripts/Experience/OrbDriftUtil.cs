using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.NarratorOrb;
using UnityEngine;

namespace DroneDDW.Experience
{
	// Floats the orb to another spot, easing in and out, on scaled time so a paused session holds it still
	internal static class OrbDriftUtil
	{
		#region Internal

		internal static async UniTask DriftAsync(NarratorOrbController orb, Vector3 target, float durationSeconds,
			CancellationToken cancellationToken)
		{
			Vector3 start = orb.transform.position;
			float elapsedSeconds = 0f;
			while (elapsedSeconds < durationSeconds)
			{
				elapsedSeconds += Time.deltaTime;
				orb.MoveTo(Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsedSeconds / durationSeconds)));
				await UniTask.NextFrame(cancellationToken);
			}

			orb.MoveTo(target);
		}

		#endregion
	}
}
