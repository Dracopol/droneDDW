using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Directs the park's life: follows the park's level, easing between levels while the park changes, so every
	/// system can ask how active it should be; knows where the listener is; and owns the shared one-shot sounds.
	/// Updates before the systems that read it.
	/// </summary>
	[DefaultExecutionOrder(-10)]
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Life")]
	public sealed class ParkLife : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkLevelController _controller;
		[SerializeField] private ParkLifeSettings _settings;

		[SerializeField, Tooltip("The XR camera. Falls back to Camera.main when left empty.")]
		private Transform _listener;

		[SerializeField] private ParkSoundPool _sounds;

		#endregion

		#region Fields

		private float _level = -1f;

		#endregion

		#region Properties

		/// <summary>The park's level, eased: 0 (full nature) to 4 (full efficiency), fractional while it changes.</summary>
		public float Level => Mathf.Max(0f, _level);
		public ParkLifeSettings Settings => _settings;
		public Vector3 ListenerPosition { get; private set; }
		public float Volume => _settings.Volume;
		public bool IsSpatialized => _settings.IsSpatialized;
		internal ParkSoundPool Sounds => _sounds;

		#endregion

		#region Setup

		private void Awake()
		{
			_sounds.Initialize(this, _settings.OneShotVoices);
			UpdateListener();
		}

		#endregion

		#region Update

		private void Update()
		{
			UpdateListener();
			int target = Mathf.Max(0, _controller.CurrentLevel);
			if (_level < 0f)
			{
				_level = target;
				return;
			}

			// Exponential easing reaches about 95 percent in the blend time, whatever the size of the step
			_level = Mathf.Lerp(_level, target, 1f - Mathf.Exp(-3f * Time.deltaTime / _settings.LevelBlendSeconds));
		}

		#endregion

		#region Public

		/// <summary>The value for the park's current (eased) level.</summary>
		public float Activity(ParkLevelValues values)
		{
			return values.Evaluate(Level);
		}

		#endregion

		#region Internal

		internal void Configure(ParkLevelController controller, ParkLifeSettings settings, Transform listener,
			ParkSoundPool sounds)
		{
			_controller = controller;
			_settings = settings;
			_listener = listener;
			_sounds = sounds;
		}

		#endregion

		#region Private

		private void UpdateListener()
		{
			Transform listener = _listener;
			if (listener == null && Camera.main != null)
			{
				listener = Camera.main.transform;
			}

			if (listener != null)
			{
				ListenerPosition = listener.position;
			}
		}

		#endregion
	}
}
