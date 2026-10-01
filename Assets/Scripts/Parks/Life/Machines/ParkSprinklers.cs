using System;
using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// The irrigation runs in bursts: a group of pipes near the seat sprays droplets along its pipes for a few seconds,
	/// hissing, while its controller's light blinks; then rests until its next turn. Only groups close enough to be
	/// seen spray, and only a couple at a time. One particle system per group, fed by hand, so it costs nothing at rest.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Sprinklers")]
	public sealed class ParkSprinklers : MonoBehaviour
	{
		#region Entities

		private sealed class Group
		{
			public ParticleSystem Spray;
			public ParkLoop Hiss;
			public float NextBurst;
			public float BurstEnd;
			public bool IsActive;
			public float Pending;
		}

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLife _life;

		[SerializeField, Tooltip("Controller lights blink while their group sprays.")]
		private ParkMachineLights _lights;

		[SerializeField] private Material _dropletMaterial;

		[SerializeField, Tooltip("Each pipe's two ends, at nozzle height.")]
		private Vector3[] _pipeStarts = Array.Empty<Vector3>();

		[SerializeField] private Vector3[] _pipeEnds = Array.Empty<Vector3>();
		[SerializeField] private ParkLevelElement[] _pipeElements = Array.Empty<ParkLevelElement>();

		[SerializeField, Tooltip("The group each pipe belongs to.")]
		private int[] _pipeGroups = Array.Empty<int>();

		[SerializeField] private Vector3[] _groupCenters = Array.Empty<Vector3>();

		[SerializeField, Tooltip("The irrigation controller (machine lights index) of each group, or -1.")]
		private int[] _groupControllers = Array.Empty<int>();

		#endregion

		#region Fields

		private const float Gravity = 9.81f;
		private const float SideSpeedMetersPerSecond = 0.6f;
		private const float DropletSize = 0.045f;
		private const int MaxDroplets = 800;
		private const int SoundPriority = 120;

		private static readonly Color DropletColor = new Color(0.78f, 0.9f, 1f, 0.75f);

		private Group[] _groups = Array.Empty<Group>();
		private ParticleSystem.EmitParams _emit;

		#endregion

		#region Setup

		private void Start()
		{
			ParkSprinklerSettings settings = _life.Settings.Sprinklers;
			_groups = new Group[_groupCenters.Length];
			for (int g = 0; g < _groups.Length; g++)
			{
				_groups[g] = new Group
				{
					Spray = CreateSpray(g),
					Hiss = new ParkLoop(_life, transform, $"Sprinkler Hiss {g + 1}", settings.Sound, SoundPriority, 0f),
					NextBurst = Time.time + UnityEngine.Random.Range(2f, settings.IntervalSeconds.y),
				};
				_groups[g].Hiss.Transform.position = _groupCenters[g];
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkSprinklerSettings settings = _life.Settings.Sprinklers;
			float now = Time.time, deltaTime = Time.deltaTime;
			int active = 0;
			foreach (Group group in _groups)
			{
				active += group.IsActive ? 1 : 0;
			}

			for (int g = 0; g < _groups.Length; g++)
			{
				Group group = _groups[g];
				float presence = GroupPresence(g);
				if (group.IsActive && (now >= group.BurstEnd || presence < 0.5f))
				{
					SetActive(g, false);
					group.NextBurst = now + UnityEngine.Random.Range(settings.IntervalSeconds.x, settings.IntervalSeconds.y);
					active--;
				}
				else if (!group.IsActive && now >= group.NextBurst)
				{
					float range = settings.RangeMeters;
					bool isNear = (_groupCenters[g] - _life.ListenerPosition).sqrMagnitude < range * range;
					if (isNear && presence >= 0.5f && active < settings.MaxGroups)
					{
						SetActive(g, true);
						group.BurstEnd = now + UnityEngine.Random.Range(settings.DurationSeconds.x, settings.DurationSeconds.y);
						active++;
					}
					else
					{
						group.NextBurst = now + UnityEngine.Random.Range(2f, 6f);
					}
				}

				if (group.IsActive)
				{
					Emit(g, group, settings, deltaTime);
				}

				group.Hiss.Update(group.IsActive ? presence : 0f, 1f, deltaTime, 0.4f);
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkMachineLights lights, Material dropletMaterial, Vector3[] pipeStarts,
			Vector3[] pipeEnds, ParkLevelElement[] pipeElements, int[] pipeGroups, Vector3[] groupCenters,
			int[] groupControllers)
		{
			_life = life;
			_lights = lights;
			_dropletMaterial = dropletMaterial;
			_pipeStarts = pipeStarts;
			_pipeEnds = pipeEnds;
			_pipeElements = pipeElements;
			_pipeGroups = pipeGroups;
			_groupCenters = groupCenters;
			_groupControllers = groupControllers;
		}

		#endregion

		#region Private

		private ParticleSystem CreateSpray(int group)
		{
			GameObject sprayObject = new GameObject($"Spray {group + 1}");
			sprayObject.transform.SetParent(transform, false);
			ParticleSystem spray = sprayObject.AddComponent<ParticleSystem>();
			spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			ParticleSystem.MainModule main = spray.main;
			main.playOnAwake = false;
			main.loop = true;
			main.startSpeed = 0f;
			main.startSize = DropletSize;
			main.startColor = DropletColor;
			main.gravityModifier = 1f;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.maxParticles = MaxDroplets;
			ParticleSystem.EmissionModule emission = spray.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = spray.shape;
			shape.enabled = false;
			ParticleSystemRenderer sprayRenderer = sprayObject.GetComponent<ParticleSystemRenderer>();
			sprayRenderer.sharedMaterial = _dropletMaterial;
			sprayRenderer.renderMode = ParticleSystemRenderMode.Billboard;
			sprayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			spray.Play();
			return spray;
		}

		/// <summary>Launches droplets from random points along the group's shown pipes, up to the spray height.</summary>
		private void Emit(int g, Group group, ParkSprinklerSettings settings, float deltaTime)
		{
			float upSpeed = Mathf.Sqrt(2f * Gravity * settings.SprayHeightMeters);
			float lifetime = 2f * upSpeed / Gravity;
			for (int p = 0; p < _pipeGroups.Length; p++)
			{
				if (_pipeGroups[p] != g || PipePresence(p) < 0.5f)
				{
					continue;
				}

				group.Pending += settings.DropletsPerSecondPerPipe * deltaTime;
				Vector3 along = _pipeEnds[p] - _pipeStarts[p];
				Vector3 side = Vector3.Cross(Vector3.up, along).normalized;
				while (group.Pending >= 1f)
				{
					group.Pending -= 1f;
					float spread = UnityEngine.Random.Range(-1f, 1f);
					_emit.position = _pipeStarts[p] + along * UnityEngine.Random.value;
					_emit.velocity = Vector3.up * upSpeed * UnityEngine.Random.Range(0.8f, 1.05f)
						+ side * spread * SideSpeedMetersPerSecond;
					_emit.startLifetime = lifetime * 1.1f;
					_emit.startSize = DropletSize * UnityEngine.Random.Range(0.7f, 1.3f);
					group.Spray.Emit(_emit, 1);
				}
			}
		}

		private void SetActive(int g, bool isActive)
		{
			_groups[g].IsActive = isActive;
			if (_lights != null)
			{
				_lights.SetControllerActive(_groupControllers[g], isActive);
			}
		}

		private float GroupPresence(int g)
		{
			float best = 0f;
			for (int p = 0; p < _pipeGroups.Length; p++)
			{
				if (_pipeGroups[p] == g)
				{
					best = Mathf.Max(best, PipePresence(p));
				}
			}

			return best;
		}

		private float PipePresence(int p)
		{
			ParkLevelElement element = _pipeElements[p];
			if (element == null)
			{
				return 1f;
			}

			return element.gameObject.activeInHierarchy ? Mathf.Clamp01(element.Presence) : 0f;
		}

		#endregion
	}
}
