using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Core
{
	// A plane just in front of the camera that fades the view to a color and back
	internal class ScreenFade : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Renderer _renderer;
		[SerializeField] private Color _color = Color.black;

		[Min(0f)]
		[SerializeField] private float _durationSeconds = 1f;

		#endregion

		#region Fields

		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

		private MaterialPropertyBlock _propertyBlock;
		private float _opacity;

		#endregion

		#region Setup

		private void Awake()
		{
			_propertyBlock = new MaterialPropertyBlock();
			SetOpacity(0f);
		}

		#endregion

		#region Internal

		// 0 shows the world, 1 covers the view completely
		internal async UniTask FadeToAsync(float targetOpacity, CancellationToken cancellationToken)
		{
			using CancellationTokenSource linkedSource =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);

			float startOpacity = _opacity;
			float elapsedSeconds = 0f;

			while (elapsedSeconds < _durationSeconds)
			{
				elapsedSeconds += Time.unscaledDeltaTime;
				SetOpacity(Mathf.Lerp(startOpacity, targetOpacity, elapsedSeconds / _durationSeconds));
				await UniTask.Yield(PlayerLoopTiming.Update, linkedSource.Token);
			}

			SetOpacity(targetOpacity);
		}

		#endregion

		#region Private

		private void SetOpacity(float opacity)
		{
			_opacity = opacity;

			Color color = _color;
			color.a = opacity;
			_propertyBlock.SetColor(BaseColorId, color);
			_renderer.SetPropertyBlock(_propertyBlock);
			_renderer.enabled = opacity > 0f;
		}

		#endregion
	}
}
