using UnityEngine;

namespace DroneDDW.Voice
{
	// How loud each voice plays, in dB on top of its recording (Content/Data/Voice/Voice Levels.asset). Every voice's
	// AudioSource carries a VoiceGain that reads this live, so the levels can be tuned in Play mode (the Voice Levels
	// Test scene plays them side by side) and the edits stay. The character recordings came in 3 to 11 dB quieter than
	// the narrator's lines, the student's the quietest.
	[CreateAssetMenu(fileName = "Voice Levels", menuName = "DroneDDW/Voice Levels")]
	public sealed class VoiceLevels : ScriptableObject
	{
		#region Fields

		public const string AssetPath = "Assets/Content/Data/Voice/Voice Levels.asset";

		#endregion

		#region Serialized fields

		[Range(-24f, 24f)]
		[SerializeField] private float _narratorDb;

		[Range(-24f, 24f)]
		[SerializeField] private float _grandmaDb = 8f;

		[Range(-24f, 24f)]
		[SerializeField] private float _workerDb = 9f;

		[Range(-24f, 24f)]
		[SerializeField] private float _studentDb = 15f;

		#endregion

		#region Public

		public float GetDecibels(VoiceRole role)
		{
			switch (role)
			{
				case VoiceRole.Narrator:
					return _narratorDb;
				case VoiceRole.Grandma:
					return _grandmaDb;
				case VoiceRole.Worker:
					return _workerDb;
				case VoiceRole.Student:
					return _studentDb;
				default:
					throw new System.ArgumentOutOfRangeException(nameof(role), role, null);
			}
		}

		#endregion
	}
}
