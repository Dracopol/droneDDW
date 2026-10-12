using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// How the hands meet the table, shared by every scene whose table uses this asset (the neighborhood and the
	/// experience's practice table): the radius of the spheres along each hand bone, the push's touch margin and sphere
	/// spacing, a padding around the visible knob box the spheres push against, the knob's grab box (its BoxCollider,
	/// which only the finger grab reaches for), and how long the lock-in button has to be held. Read live, so changes
	/// apply at once; an asset, so changes made in Play mode are kept.
	/// </summary>
	[CreateAssetMenu(fileName = "Neighborhood Push Settings", menuName = "DroneDDW/Neighborhood/Push Settings")]
	public sealed class NeighborhoodPushSettings : ScriptableObject
	{
		#region Serialized fields

		[Header("Hand sphere radii (the touch margin is added on top)")]
		[SerializeField, Range(0f, MaxRadiusMeters), Tooltip("Wrist to palm centre.")]
		private float _wristRadiusMeters = 0.02f;

		[SerializeField, Range(0f, MaxRadiusMeters), Tooltip("The four segments fanning across the palm.")]
		private float _palmRadiusMeters = 0.015f;

		[SerializeField, Range(0f, MaxRadiusMeters), Tooltip("The thumb's first segment, from the wrist.")]
		private float _thumbBaseRadiusMeters = 0.013f;

		[SerializeField, Range(0f, MaxRadiusMeters), Tooltip("Each finger's first segment, and the thumb's middle one.")]
		private float _fingerBaseRadiusMeters = 0.01f;

		[SerializeField, Range(0f, MaxRadiusMeters), Tooltip("Each finger's middle segment.")]
		private float _fingerMiddleRadiusMeters = 0.009f;

		[SerializeField, Range(0f, MaxRadiusMeters), Tooltip("Each finger's and the thumb's last segment, up to the tip.")]
		private float _fingerTipRadiusMeters = 0.008f;

		[Header("Push")]
		[SerializeField, Range(0f, 0.02f), Tooltip("Added to every sphere's radius, so a hand resting against the knob "
			+ "keeps holding it.")]
		private float _touchMarginMeters = 0.005f;

		[SerializeField, Range(0.002f, 0.03f), Tooltip("Gap between the spheres along each hand bone.")]
		private float _sampleSpacingMeters = 0.01f;

		[SerializeField, Range(-0.02f, 0.03f), Tooltip("Grows the visible knob's box on every side (shrinks it when "
			+ "negative). The spheres push against this box.")]
		private float _pushBoxPaddingMeters;

		[Header("Grab box (the knob's BoxCollider)")]
		[SerializeField, Tooltip("In the knob's space. What the finger grab reaches for; pushes ignore it.")]
		private Vector3 _grabBoxCenter = new Vector3(0f, 0.03f, 0f);

		[SerializeField, Tooltip("Bigger than the knob, so it is easy to grab.")]
		private Vector3 _grabBoxSize = new Vector3(0.08f, 0.07f, 0.08f);

		[Header("Lock-in button")]
		[SerializeField, Range(0.2f, 5f), Tooltip("How long a hand has to hold the button down to lock the choice in; "
			+ "the countdown above the button shows the whole seconds left.")]
		private float _lockHoldSeconds = 3f;

		[SerializeField, Range(0.05f, 2f), Tooltip("How quickly the ring empties again when the hand lets go early.")]
		private float _lockDrainSeconds = 0.4f;

		#endregion

		#region Fields

		private const float MaxRadiusMeters = 0.04f;

		private static NeighborhoodPushSettings _defaults;

		#endregion

		#region Properties

		internal float WristRadiusMeters => _wristRadiusMeters;
		internal float PalmRadiusMeters => _palmRadiusMeters;
		internal float ThumbBaseRadiusMeters => _thumbBaseRadiusMeters;
		internal float FingerBaseRadiusMeters => _fingerBaseRadiusMeters;
		internal float FingerMiddleRadiusMeters => _fingerMiddleRadiusMeters;
		internal float FingerTipRadiusMeters => _fingerTipRadiusMeters;
		internal float TouchMarginMeters => _touchMarginMeters;
		internal float SampleSpacingMeters => _sampleSpacingMeters;
		internal float PushBoxPaddingMeters => _pushBoxPaddingMeters;
		internal Vector3 GrabBoxCenter => _grabBoxCenter;
		internal Vector3 GrabBoxSize => _grabBoxSize;
		internal float LockHoldSeconds => _lockHoldSeconds;
		internal float LockDrainSeconds => _lockDrainSeconds;

		#endregion

		#region Setup

		private void OnValidate()
		{
			// A BoxCollider can't take a negative size
			_grabBoxSize = Vector3.Max(_grabBoxSize, Vector3.zero);
		}

		#endregion

		#region Internal

		/// <summary>
		/// The given settings, or an instance with the defaults for components that have none (such as the slider in a
		/// scene built before this asset existed).
		/// </summary>
		internal static NeighborhoodPushSettings OrDefaults(NeighborhoodPushSettings settings)
		{
			if (settings != null)
			{
				return settings;
			}

			if (_defaults == null)
			{
				_defaults = CreateInstance<NeighborhoodPushSettings>();
				_defaults.hideFlags = HideFlags.HideAndDontSave;
			}

			return _defaults;
		}

		#endregion
	}
}
