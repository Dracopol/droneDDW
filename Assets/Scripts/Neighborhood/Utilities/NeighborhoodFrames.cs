using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Neighborhood.Utilities
{
    /// <summary>
    /// Frame and time waits for the async code, on UniTask's player loop (no allocations per frame). Scaled time.
    /// </summary>
    internal static class NeighborhoodFrames
    {
        #region Internal

        internal static UniTask NextFrameAsync(CancellationToken cancellationToken)
        {
            return UniTask.NextFrame(cancellationToken);
        }

        internal static UniTask WaitSecondsAsync(float seconds, CancellationToken cancellationToken)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), DelayType.DeltaTime, PlayerLoopTiming.Update,
                cancellationToken);
        }

        #endregion
    }
}
