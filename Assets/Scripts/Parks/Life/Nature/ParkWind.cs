using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// The park's wind: a steady breeze plus gust fronts that roll across the park now and then. Every frame it hands the
	/// vegetation shader (DroneDDW/Parks/Vegetation Wind) the wind as globals, and it works out the same strength on the
	/// CPU so the leaf rustle swells with exactly the gusts the trees show.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Wind")]
	public sealed class ParkWind : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkLife _life;

		#endregion

		#region Fields

		private const float StartMeters = -75f;
		private const float EndMeters = 75f;
		private const float FlutterMetersPerUnit = 0.02f;

		private static readonly int DirectionId = Shader.PropertyToID("_ParkWindDirection");
		private static readonly int GustId = Shader.PropertyToID("_ParkWindGust");
		private static readonly int ShapeId = Shader.PropertyToID("_ParkWindShape");

		private Vector2 _direction = Vector2.up;
		private float _frontMeters = EndMeters;
		private float _nextGustTime;

		#endregion

		#region Setup

		private void Start()
		{
			_nextGustTime = Time.time + Random.Range(1f, _life.Settings.Wind.GustIntervalSeconds.y);
		}

		private void OnDisable()
		{
			Shader.SetGlobalVector(DirectionId, Vector4.zero);
			Shader.SetGlobalVector(GustId, Vector4.zero);
			Shader.SetGlobalVector(ShapeId, Vector4.zero);
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkWindSettings settings = _life.Settings.Wind;
			float radians = settings.DirectionDegrees * Mathf.Deg2Rad;
			_direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));

			// A front crosses the park, then the next one waits its turn
			if (_frontMeters < EndMeters)
			{
				_frontMeters += settings.GustSpeedMetersPerSecond * Time.deltaTime;
			}
			else if (Time.time >= _nextGustTime)
			{
				_frontMeters = StartMeters;
				_nextGustTime = Time.time + Random.Range(settings.GustIntervalSeconds.x, settings.GustIntervalSeconds.y)
					+ (EndMeters - StartMeters) / settings.GustSpeedMetersPerSecond;
			}

			Shader.SetGlobalVector(DirectionId, new Vector4(_direction.x, _direction.y, settings.Breeze, settings.SwayHertz));
			Shader.SetGlobalVector(GustId, new Vector4(_direction.x, _direction.y, _frontMeters, settings.GustStrength));
			Shader.SetGlobalVector(ShapeId, new Vector4(settings.BendMeters, settings.Flutter * FlutterMetersPerUnit,
				settings.GustWidthMeters, 0f));
		}

		#endregion

		#region Public

		/// <summary>
		/// How hard the wind blows at a point on the ground right now, as the vegetation shader sees it: the breeze (with
		/// the shader's slow sway left out) plus any gust front passing there.
		/// </summary>
		public float StrengthAt(Vector3 position)
		{
			ParkWindSettings settings = _life.Settings.Wind;
			float along = position.x * _direction.x + position.z * _direction.y - _frontMeters;
			float width = Mathf.Max(1f, settings.GustWidthMeters);
			return settings.Breeze + settings.GustStrength * Mathf.Exp(-along * along / (width * width));
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life)
		{
			_life = life;
		}

		#endregion
	}
}
