using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Core
{
	// Fades from the passthrough tutorial into the virtual room, swapping everything while the view is black
	internal class EnvironmentTransition : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ScreenFade _screenFade;
		[SerializeField] private OVRPassthroughLayer _passthroughLayer;
		[SerializeField] private Camera _camera;
		[SerializeField] private Revealable _virtualEnvironment;

		[Tooltip("Hidden while the view is black, e.g. the tutorial papers")]
		[SerializeField] private Revealable[] _passthroughOnlyObjects;

		[Tooltip("How long the view stays black while the room swaps")]
		[Min(0f)]
		[SerializeField] private float _blackHoldSeconds = 0.3f;

		#endregion

		#region Internal

		internal async UniTask ToVirtualAsync(CancellationToken cancellationToken)
		{
			using CancellationTokenSource linkedSource =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);

			await _screenFade.FadeToAsync(1f, linkedSource.Token);

			_passthroughLayer.hidden = true;
			_camera.clearFlags = CameraClearFlags.Skybox;
			_virtualEnvironment.Show();

			foreach (Revealable revealable in _passthroughOnlyObjects)
				revealable.Hide();

			await UniTask.Delay(TimeSpan.FromSeconds(_blackHoldSeconds), cancellationToken: linkedSource.Token);
			await _screenFade.FadeToAsync(0f, linkedSource.Token);
		}

		#endregion
	}
}
