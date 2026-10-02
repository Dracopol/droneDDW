using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Core;
using DroneDDW.Interaction;
using UnityEngine;

namespace DroneDDW.Tutorial
{
	// Arms the stamp for the given papers and completes once one of them is stamped
	internal class StampCondition : StepCondition
	{
		#region Serialized fields

		[SerializeField] private Stamp _stamp;
		[SerializeField] private Paper[] _papers;

		[Tooltip("On: each paper is its own outcome, in list order. Off: stamping any of them is the one outcome.")]
		[SerializeField] private bool _hasOutcomePerPaper;

		#endregion

		#region Properties

		internal override int OutcomeCount => _hasOutcomePerPaper ? _papers.Length : 1;

		#endregion

		#region Internal

		internal override async UniTask<int> WaitAsync(CancellationToken cancellationToken)
		{
			using CancellationTokenSource linkedSource =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);

			_stamp.Arm(_papers);

			try
			{
				Paper stampedPaper = await EventAwaitUtil.NextAsync<Paper>(
					handler => _stamp.OnStamp += handler,
					handler => _stamp.OnStamp -= handler,
					linkedSource.Token);

				return _hasOutcomePerPaper ? Array.IndexOf(_papers, stampedPaper) : 0;
			}
			finally
			{
				_stamp.Disarm();
			}
		}

		#endregion
	}
}
