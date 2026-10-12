using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Experience
{
	// A quad just in front of the headset camera that fades the view to black and back, so switching between
	// passthrough and the Neighborhood never shows. Its material writes full alpha, so at black it also covers
	// passthrough. It draws under the admin menu, which stays usable while the view is black. Unscaled time: a fade
	// finishes even while the session is paused.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/View Fade")]
	internal sealed class ViewFade : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Renderer _renderer;
		[SerializeField] private Color _color = Color.black;

		#endregion

		#region Fields

		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

		private MaterialPropertyBlock _propertyBlock;
		private float _opacity;

		#endregion

		#region Properties

		// 0 shows the world, 1 covers the view
		internal float Opacity => _opacity;

		#endregion

		#region Setup

		private void Awake()
		{
			_propertyBlock = new MaterialPropertyBlock();
			SetOpacity(0f);
		}

		#endregion

		#region Internal

		internal void Configure(Renderer fadeRenderer)
		{
			_renderer = fadeRenderer;
		}

		internal async UniTask FadeToAsync(float targetOpacity, float durationSeconds, CancellationToken cancellationToken)
		{
			float startOpacity = _opacity;
			float elapsedSeconds = 0f;
			while (elapsedSeconds < durationSeconds)
			{
				elapsedSeconds += Time.unscaledDeltaTime;
				SetOpacity(Mathf.Lerp(startOpacity, targetOpacity, elapsedSeconds / durationSeconds));
				await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
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
