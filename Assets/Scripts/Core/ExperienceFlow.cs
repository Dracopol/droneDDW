using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDDW.Core
{
	// Runs the whole experience: the tutorial in passthrough, then the questionnaire in VR
	internal class ExperienceFlow : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private TutorialRunner _tutorialRunner;

		[Tooltip("Editor only: waits for this key so there's time to put the headset on. Builds start right away.")]
		[SerializeField] private Key _editorStartKey = Key.Space;

		#endregion

		#region Setup

		private async void Start()
		{
			try
			{
				await WaitForEditorStartKeyAsync(destroyCancellationToken);
				await _tutorialRunner.RunAsync(destroyCancellationToken);
				Debug.Log("[DDW Experience] Tutorial complete", this);
			}
			catch (OperationCanceledException)
			{
				// Play mode stopped or the scene unloaded
			}
		}

		#endregion

		#region Private

		private async UniTask WaitForEditorStartKeyAsync(CancellationToken cancellationToken)
		{
			if (!Application.isEditor)
				return;

			Debug.Log($"[DDW Experience] Press {_editorStartKey} in the Game view to start", this);
			await UniTask.WaitUntil(IsEditorStartKeyPressed, cancellationToken: cancellationToken);
		}

		private bool IsEditorStartKeyPressed()
		{
			return Keyboard.current != null && Keyboard.current[_editorStartKey].wasPressedThisFrame;
		}

		#endregion
	}
}
