using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// All tuning for the park's life: machines, wildlife, wind and their sounds. An asset, so changes made in Play
	/// mode are kept and survive rebuilding the park scene. The park builder creates it once and never overwrites it.
	/// </summary>
	[CreateAssetMenu(fileName = "Park Life Settings", menuName = "DroneDDW/Parks/Park Life Settings")]
	public sealed class ParkLifeSettings : ScriptableObject
	{
		#region Serialized fields

		[SerializeField, Min(0.1f), Tooltip("Seconds life takes to follow a level change (the park's wave takes about 4).")]
		private float _levelBlendSeconds = 3.5f;

		[SerializeField, Tooltip("3D sounds through Meta XR Audio's head-related filtering. Off uses plain stereo panning.")]
		private bool _isSpatialized = true;

		[SerializeField, Range(0f, 1f)] private float _volume = 1f;

		[SerializeField, Range(1, 32), Tooltip("Sources shared by all short sounds (calls, beeps, plops).")]
		private int _oneShotVoices = 12;

		[SerializeField] private ParkDroneSettings _drones = new ParkDroneSettings();
		[SerializeField] private ParkRobotSettings _robots = new ParkRobotSettings();
		[SerializeField] private ParkMachineSettings _machines = new ParkMachineSettings();
		[SerializeField] private ParkSprinklerSettings _sprinklers = new ParkSprinklerSettings();
		[SerializeField] private ParkBirdSettings _birds = new ParkBirdSettings();
		[SerializeField] private ParkPondSettings _pond = new ParkPondSettings();
		[SerializeField] private ParkInsectSettings _insects = new ParkInsectSettings();
		[SerializeField] private ParkWindSettings _wind = new ParkWindSettings();

		#endregion

		#region Properties

		public float LevelBlendSeconds => _levelBlendSeconds;
		public bool IsSpatialized => _isSpatialized;
		public float Volume => _volume;
		public int OneShotVoices => _oneShotVoices;
		public ParkDroneSettings Drones => _drones;
		public ParkRobotSettings Robots => _robots;
		public ParkMachineSettings Machines => _machines;
		public ParkSprinklerSettings Sprinklers => _sprinklers;
		public ParkBirdSettings Birds => _birds;
		public ParkPondSettings Pond => _pond;
		public ParkInsectSettings Insects => _insects;
		public ParkWindSettings Wind => _wind;

		#endregion
	}
}
