using TMPro;
using UnityEngine;

namespace DroneDDW.Droney
{
	// Shows the text of Droney's current line just under her, turned toward the player.
	// Runs after her flight so the caption doesn't trail a frame behind her.
	[DefaultExecutionOrder(100)]
	internal class DroneyCaptions : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private DroneyVoice _droneyVoice;
		[SerializeField] private DroneyCaptionTable _captionTable;
		[SerializeField] private TMP_Text _text;
		[SerializeField] private Renderer _background;
		[SerializeField] private Transform _droney;
		[SerializeField] private Transform _head;

		[Min(0f)]
		[SerializeField] private float _heightBelowDroneyMeters = 0.14f;

		#endregion

		#region Fields

		private AudioClip _shownClip;

		#endregion

		#region Setup

		private void Awake()
		{
			SetVisible(false);
		}

		private void OnEnable()
		{
			_droneyVoice.OnLineStart += HandleLineStart;
			_droneyVoice.OnLineEnd += HandleLineEnd;
		}

		private void OnDisable()
		{
			_droneyVoice.OnLineStart -= HandleLineStart;
			_droneyVoice.OnLineEnd -= HandleLineEnd;
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			Vector3 position = _droney.position + Vector3.down * _heightBelowDroneyMeters;
			Vector3 awayFromHead = position - _head.position;

			// Text reads correctly when its forward points away from the viewer
			if (awayFromHead.sqrMagnitude > 0.0001f)
				transform.SetPositionAndRotation(position, Quaternion.LookRotation(awayFromHead));
		}

		#endregion

		#region Event handlers

		private void HandleLineStart(AudioClip clip)
		{
			if (!_captionTable.TryGetCaption(clip, out string caption))
			{
				_shownClip = null;
				SetVisible(false);
				return;
			}

			_shownClip = clip;
			_text.text = caption;
			SetVisible(true);
		}

		private void HandleLineEnd(AudioClip clip)
		{
			// A cut off line can end after the next one started, only its own caption is hidden
			if (clip != _shownClip)
				return;

			_shownClip = null;
			SetVisible(false);
		}

		#endregion

		#region Private

		private void SetVisible(bool isVisible)
		{
			_text.enabled = isVisible;
			_background.enabled = isVisible;
		}

		#endregion
	}
}
