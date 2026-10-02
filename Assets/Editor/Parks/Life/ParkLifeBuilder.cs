using System.Collections.Generic;
using System.Linq;
using DroneDDW.Parks.Levels;
using DroneDDW.Parks.Life;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Life
{
	/// <summary>
	/// Builds the park's life (Park_Life) from the objects the level park builder placed: the director and its shared
	/// sounds, the machines (drones, delivery robots, mowers, lights, machine sounds, sprinklers) and the wildlife and
	/// wind. Routes and spots come from the park's layout: patrol routes over the drone yard and the park, robot routes
	/// on the axis paths, mowers on the lawns around them, sprinkler groups from the irrigation pipes.
	/// </summary>
	internal static class ParkLifeBuilder
	{
		#region Fields

		private const string Prefix = "Park_LV_";
		private const string Drone = Prefix + "Drone";
		private const string DronePad = Prefix + "DronePad";
		private const string DeliveryRobot = Prefix + "DeliveryRobot";
		private const string RobotMower = Prefix + "RobotMower";
		private const string LawnPrefix = Prefix + "Lawn_";
		private const float ParkedDroneHeight = 0.33f;
		private const float HoverHeightThreshold = 2f;
		private const float MowerLawnReach = 12f;
		private const float SprinklerCellMeters = 12f;
		private const float ControllerReach = 25f;
		private const string TreePrefix = Prefix + "Tree_";
		private const string PineTree = TreePrefix + "Pine";
		private const string MeadowPrefix = Prefix + "Meadow_";
		private const string NearMeadowPrefix = Prefix + "Meadow_Near_";
		private const string FlowerPrefix = Prefix + "FlowerClump_";
		private const int RustleCrowns = 24;
		private const int FrogBanks = 5;
		private const float FlowerReachMeters = 25f;
		private const float FountainHalfWidthMeters = 5f;
		private const float CrownHeightShare = 0.66f;
		private const float PineCrownHeightShare = 0.42f;
		private const float CrownRadiusShare = 0.75f;

		// Trees whose crowns rustle (the young trees are small and far off)
		private static readonly string[] RustlingTrees =
		{
			TreePrefix + "Oak", TreePrefix + "Linden", TreePrefix + "CopperBeech", PineTree, TreePrefix + "Birch",
			TreePrefix + "Uniform",
		};

		// Where the woodpecker clings on each broadleaf, measured on the kit's meshes: below the crown (which comes down
		// to about 2.2 m, 1.9 m on the linden), at the radius there of the tapering hexagonal trunk
		private static readonly (string Prefab, float HeightMeters, float RadiusMeters)[] Trunks =
		{
			(TreePrefix + "Oak", 1.6f, 0.29f),
			(TreePrefix + "CopperBeech", 1.6f, 0.29f),
			(TreePrefix + "Linden", 1.2f, 0.25f),
		};

		private static readonly Vector3 BuzzardCenter = new Vector3(0f, 0f, 10f);

		// Patrol routes, handed to hovering drones in the order they appear through the levels: the level 2 drone
		// circles the drone yard; level 3 adds one around the park; level 4 adds the hub circle, a pass in front of the
		// seat and a run straight overhead along the axis.
		private static readonly Vector3[][] PatrolRoutes =
		{
			new[]
			{
				new Vector3(20f, 10f, 14f), new Vector3(42f, 12f, 14f), new Vector3(42f, 12f, 42f),
				new Vector3(20f, 10f, 42f),
			},
			new[]
			{
				new Vector3(-32f, 14f, -32f), new Vector3(32f, 14f, -32f), new Vector3(32f, 14f, 32f),
				new Vector3(-32f, 14f, 32f),
			},
			Circle(new Vector3(0f, 6f, 26f), 10f, 12),
			new[] { new Vector3(-40f, 8f, 12f), new Vector3(40f, 8f, 12f) },
			new[] { new Vector3(0f, 16f, 44f), new Vector3(0f, 16f, -44f) },
		};

		// Where pad drones fly to and hover: the lockers, the plaza in front of the seat, the entrance, the pavilion,
		// the hub roof and the rest area
		private static readonly Vector3[] DeliveryPoints =
		{
			new Vector3(-7f, 3.5f, 20f), new Vector3(7f, 3.5f, 20f), new Vector3(0f, 3.5f, 9f), new Vector3(0f, 4f, -44f),
			new Vector3(16f, 6f, 33f), new Vector3(0f, 10f, 26f), new Vector3(-28f, 3.5f, 11.5f),
		};

		// Robot routes on the axis paths, keeping to the right: south axis, west axis, north axis to the west locker
		private static readonly Vector3[][] RobotRoutes =
		{
			new[] { new Vector3(1.2f, 0.1f, -44f), new Vector3(1.2f, 0.1f, -10f) },
			new[] { new Vector3(-42f, 0.1f, 1.2f), new Vector3(-9f, 0.1f, 1.2f) },
			new[] { new Vector3(-1.2f, 0.1f, 9f), new Vector3(-1.2f, 0.1f, 17.5f), new Vector3(-5.5f, 0.1f, 18.8f) },
		};

		#endregion

		#region Internal

		/// <summary>Props that move: wrapped by the level park builder so transitions and motion don't fight.</summary>
		internal static bool IsMover(string prefab)
		{
			return prefab == Drone || prefab == DeliveryRobot || prefab == RobotMower;
		}

		internal static void Build(IReadOnlyList<ParkLifePiece> pieces, ParkLevelController controller, Transform head)
		{
			ParkLifeSettings settings = ParkLifeAssets.LoadOrCreateSettings();
			Material lightsMaterial = ParkLifeAssets.CreateLightsMaterial();
			Material dropletMaterial = ParkLifeAssets.CreateDropletMaterial();

			GameObject root = new GameObject("Park_Life");
			ParkSoundPool sounds = AddPart<ParkSoundPool>(root, "Sounds");
			ParkLife life = root.AddComponent<ParkLife>();
			life.Configure(controller, settings, head, sounds);

			List<ParkLifePiece> pads = Of(pieces, DronePad);
			List<ParkLifePiece> beacons = Of(pieces, Prefix + "BeaconPole");
			List<ParkLifePiece> controllers = Of(pieces, Prefix + "IrrigationController");
			ParkMachineLights lights = BuildLights(root, life, pieces, lightsMaterial, pads, beacons, controllers);
			int drones = BuildDrones(root, life, lights, pieces, pads);
			int robots = BuildRobots(root, life, pieces);
			int mowers = BuildMowers(root, life, pieces);
			BuildMachineSounds(root, life, lights, pieces, beacons);
			int groups = BuildSprinklers(root, life, lights, pieces, controllers, dropletMaterial);
			string nature = BuildNature(root, life, pieces);
			Debug.Log($"[Parks] Park life: {drones} drones, {robots} delivery robots, {mowers} mowers, "
				+ $"{groups} sprinkler groups; nature on {nature}.");
		}

		#endregion

		#region Private

		private static ParkMachineLights BuildLights(GameObject root, ParkLife life, IReadOnlyList<ParkLifePiece> pieces,
			Material lightsMaterial, List<ParkLifePiece> pads, List<ParkLifePiece> beacons, List<ParkLifePiece> controllers)
		{
			ParkMachineLights lights = AddPart<ParkMachineLights>(root, "Machine Lights");
			List<ParkLifePiece> screens = Of(pieces, Prefix + "WayfindingTotem", Prefix + "InfoKiosk", Prefix + "DeliveryLocker",
				DeliveryRobot);
			lights.Configure(life,
				Lit(beacons, lightsMaterial),
				Lit(Of(pieces, Prefix + "SensorPole"), lightsMaterial),
				Lit(Of(pieces, Prefix + "ChargingStation"), lightsMaterial),
				Lit(pads, lightsMaterial),
				Lit(controllers, lightsMaterial),
				Lit(screens, lightsMaterial),
				Lit(Of(pieces, Prefix + "Lamp"), lightsMaterial),
				Lit(Of(pieces, Prefix + "Bin_Smart"), lightsMaterial),
				Lit(Of(pieces, Prefix + "ScooterDock"), lightsMaterial));
			return lights;
		}

		private static int BuildDrones(GameObject root, ParkLife life, ParkMachineLights lights,
			IReadOnlyList<ParkLifePiece> pieces, List<ParkLifePiece> pads)
		{
			List<ParkLifePiece> drones = Of(pieces, Drone);
			ParkDroneFleet fleet = AddPart<ParkDroneFleet>(root, "Drones");

			// Hovering drones get patrol routes, those in the most levels first (they appear earliest)
			List<ParkLifePiece> hovering = drones.Where(d => d.Position.y >= HoverHeightThreshold)
				.OrderByDescending(d => CountLevels(d.Entry.LevelMask)).ToList();
			int[] routeIndices = drones.Select(d => d.Position.y >= HoverHeightThreshold
				? hovering.IndexOf(d) % PatrolRoutes.Length
				: -1).ToArray();
			ParkHabitatSpot[] padSpots = pads.Select(p => new ParkHabitatSpot(p.Element,
				new Vector3(p.Position.x, ParkedDroneHeight, p.Position.z), 3f)).ToArray();
			fleet.Configure(life, lights,
				drones.Select(d => d.Element).ToArray(),
				drones.Select(d => d.Body).ToArray(),
				routeIndices,
				PatrolRoutes.Select(points => new ParkRoute(points, true)).ToArray(),
				padSpots,
				DeliveryPoints);
			return drones.Count;
		}

		private static int BuildRobots(GameObject root, ParkLife life, IReadOnlyList<ParkLifePiece> pieces)
		{
			List<ParkLifePiece> robots = Of(pieces, DeliveryRobot);
			ParkRobotFleet fleet = AddPart<ParkRobotFleet>(root, "Delivery Robots");
			fleet.Configure(life,
				robots.Select(r => r.Element).ToArray(),
				robots.Select(r => r.Body).ToArray(),
				robots.Select(r => new ParkRoute(NearestRoute(r.Position), false)).ToArray());
			return robots.Count;
		}

		private static int BuildMowers(GameObject root, ParkLife life, IReadOnlyList<ParkLifePiece> pieces)
		{
			List<ParkLifePiece> mowers = Of(pieces, RobotMower);
			List<ParkLifePiece> lawns = pieces.Where(p => p.Prefab.StartsWith(LawnPrefix)).ToList();
			ParkMowerFleet fleet = AddPart<ParkMowerFleet>(root, "Mowers");
			fleet.Configure(life,
				mowers.Select(m => m.Element).ToArray(),
				mowers.Select(m => m.Body).ToArray(),
				mowers.Select(m => LawnAround(m.Position, lawns)).ToArray());
			return mowers.Count;
		}

		private static void BuildMachineSounds(GameObject root, ParkLife life, ParkMachineLights lights,
			IReadOnlyList<ParkLifePiece> pieces, List<ParkLifePiece> beacons)
		{
			ParkMachineSounds sounds = AddPart<ParkMachineSounds>(root, "Machine Sounds");
			sounds.Configure(life, lights,
				Spots(Of(pieces, Prefix + "ChargingStation"), 1f),
				Spots(Of(pieces, Prefix + "ScooterDock"), 0.6f),
				Spots(Of(pieces, Prefix + "ServiceHub"), 4f),
				Spots(Of(pieces, Prefix + "SensorPole"), 0f),
				Spots(Of(pieces, Prefix + "WayfindingTotem"), 0f),
				Spots(Of(pieces, Prefix + "Bin_Smart"), 0f),
				Spots(beacons, 7.2f));
		}

		private static int BuildSprinklers(GameObject root, ParkLife life, ParkMachineLights lights,
			IReadOnlyList<ParkLifePiece> pieces, List<ParkLifePiece> controllers, Material dropletMaterial)
		{
			List<ParkLifePiece> pipes = Of(pieces, Prefix + "IrrigationPipe");
			List<Vector3> starts = new List<Vector3>(), ends = new List<Vector3>();
			foreach (ParkLifePiece pipe in pipes)
			{
				// The nozzles run along the pipe mesh's length, on its top
				MeshFilter filter = pipe.Body.GetComponentInChildren<MeshFilter>(true);
				Bounds bounds = filter.sharedMesh.bounds;
				Transform pipeTransform = filter.transform;
				starts.Add(pipeTransform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)));
				ends.Add(pipeTransform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z)));
			}

			// One group per 12 m cell of the park
			Dictionary<Vector2Int, int> cells = new Dictionary<Vector2Int, int>();
			int[] pipeGroups = new int[pipes.Count];
			List<Vector3> sums = new List<Vector3>();
			List<int> counts = new List<int>();
			for (int i = 0; i < pipes.Count; i++)
			{
				Vector3 middle = (starts[i] + ends[i]) * 0.5f;
				Vector2Int cell = new Vector2Int(Mathf.FloorToInt(middle.x / SprinklerCellMeters),
					Mathf.FloorToInt(middle.z / SprinklerCellMeters));
				if (!cells.TryGetValue(cell, out int group))
				{
					group = sums.Count;
					cells.Add(cell, group);
					sums.Add(Vector3.zero);
					counts.Add(0);
				}

				pipeGroups[i] = group;
				sums[group] += middle;
				counts[group]++;
			}

			Vector3[] centers = sums.Select((sum, g) => sum / counts[g]).ToArray();
			int[] groupControllers = centers.Select(center => NearestIndex(controllers, center, ControllerReach)).ToArray();
			ParkSprinklers sprinklers = AddPart<ParkSprinklers>(root, "Sprinklers");
			sprinklers.Configure(life, lights, dropletMaterial, starts.ToArray(), ends.ToArray(),
				pipes.Select(p => p.Element).ToArray(), pipeGroups, centers, groupControllers);
			return centers.Length;
		}

		/// <summary>
		/// The wildlife and the weather: water that ripples, wind and leaf rustle, birds in the trees and the sky, life on
		/// the ponds and insects, with the creature models made fresh for this build. Returns what it found, for the log.
		/// </summary>
		private static string BuildNature(GameObject root, ParkLife life, IReadOnlyList<ParkLifePiece> pieces)
		{
			ParkCreaturePrefabs creatures = ParkLifeModels.Create();
			ParkWaterSurface water = AddPart<ParkWaterSurface>(root, "Water");
			ParkWind wind = AddPart<ParkWind>(root, "Wind");
			wind.Configure(life);

			List<ParkLifePiece> trees = pieces.Where(p => p.Prefab.StartsWith(TreePrefix)).ToList();
			ParkHabitatSpot[] crowns = trees.Select(Crown).ToArray();
			ParkHabitatSpot[] rustleCrowns = trees.Where(t => RustlingTrees.Contains(t.Prefab))
				.OrderBy(t => Flat(t.Position).sqrMagnitude)
				.Take(RustleCrowns)
				.Select(Crown)
				.ToArray();
			ParkRustle rustle = AddPart<ParkRustle>(root, "Rustle");
			rustle.Configure(life, wind, rustleCrowns);

			ParkHabitatSpot[] trunks = trees.Where(t => Trunks.Any(trunk => trunk.Prefab == t.Prefab)).Select(Trunk).ToArray();
			ParkHabitatSpot[] nearMeadows = pieces.Where(p => p.Prefab.StartsWith(NearMeadowPrefix))
				.Select(meadow => Ground(meadow, 0.9f))
				.ToArray();
			ParkHabitatSpot[] meadows = pieces.Where(p => p.Prefab.StartsWith(MeadowPrefix))
				.Select(meadow => Ground(meadow, 0.8f))
				.ToArray();
			ParkLifePiece plazaPiece = pieces.FirstOrDefault(p => p.Prefab == Prefix + "Plaza_Ring");
			ParkHabitatSpot plaza = plazaPiece != null ? Ground(plazaPiece, 0.95f) : default;
			ParkHabitatSpot[] lamps = Of(pieces, Prefix + "Lamp").Select(Top).ToArray();
			ParkHabitatSpot[] reeds = Of(pieces, Prefix + "Reeds")
				.Select(reed => new ParkHabitatSpot(reed.Element, reed.WorldBounds.center, 0.4f))
				.ToArray();
			ParkHabitatSpot[] flowers = pieces
				.Where(p => p.Prefab.StartsWith(FlowerPrefix) && Flat(p.Position).magnitude < FlowerReachMeters)
				.Select(Top)
				.ToArray();

			// Flocks of sparrows and blackbirds: two of the songbird colourings
			ParkCreature[] flocks = { creatures.Songbirds[3], creatures.Songbirds[1] };
			ParkBirds birds = AddPart<ParkBirds>(root, "Birds");
			birds.Configure(life, crowns, nearMeadows, trunks, plaza, lamps, creatures.Songbirds, creatures.Pigeon,
				creatures.Crow, creatures.Woodpecker);
			ParkSkyBirds sky = AddPart<ParkSkyBirds>(root, "Sky Birds");
			sky.Configure(life, flocks, creatures.Buzzard, BuzzardCenter);
			ParkInsects insects = AddPart<ParkInsects>(root, "Insects");
			insects.Configure(life, flowers, nearMeadows, meadows, reeds, creatures.Bee, creatures.Butterflies,
				creatures.Dragonfly);
			bool hasPonds = BuildPond(root, life, water, pieces, reeds, creatures);
			return $"{crowns.Length} tree crowns, {trunks.Length} trunks, {meadows.Length} meadows "
				+ $"({nearMeadows.Length} near), {flowers.Length} flower clumps, {reeds.Length} reeds, {lamps.Length} lamps, "
				+ (hasPonds ? "the ponds" : "no ponds");
		}

		/// <summary>
		/// Pond life on the natural pond (ducks, frogs, fish, lapping) and the formal pond (ducks, its fountain), and the
		/// basin's trickle. Ducks on the formal pond keep to its north half, clear of the fountain's row of jets.
		/// </summary>
		private static bool BuildPond(GameObject root, ParkLife life, ParkWaterSurface water,
			IReadOnlyList<ParkLifePiece> pieces, ParkHabitatSpot[] reeds, ParkCreaturePrefabs creatures)
		{
			ParkLifePiece natural = pieces.FirstOrDefault(p => p.Prefab == Prefix + "Pond_Water_Natural");
			ParkLifePiece formal = pieces.FirstOrDefault(p => p.Prefab == Prefix + "Pond_Formal_Water");
			ParkLifePiece basin = pieces.FirstOrDefault(p => p.Prefab == Prefix + "Basin_Water");
			if (natural == null || formal == null || basin == null)
			{
				Debug.LogWarning("[Parks] The layout has no natural pond, formal pond or basin water, "
					+ "so the ponds stay lifeless.");
				return false;
			}

			Bounds naturalBounds = natural.WorldBounds, formalBounds = formal.WorldBounds, basinBounds = basin.WorldBounds;
			Vector3 center = new Vector3(naturalBounds.center.x, naturalBounds.max.y, naturalBounds.center.z);
			Vector3 size = naturalBounds.extents;
			Vector3 formalCenter = new Vector3(formalBounds.center.x, formalBounds.min.y,
				formalBounds.center.z + formalBounds.extents.z * 0.5f);
			ParkHabitatSpot[] ponds =
			{
				new ParkHabitatSpot(natural.Element, center, size.x),
				new ParkHabitatSpot(formal.Element, formalCenter, formalBounds.extents.x),
			};
			Vector2[] radii =
			{
				new Vector2(size.x - 1.5f, size.z - 1.5f),
				new Vector2(formalBounds.extents.x - 1.2f, formalBounds.extents.z * 0.5f - 0.8f),
			};

			// Frogs sit on the grass bank facing the seat, so only at the level that has it (the next one has stones
			// and a deck there); water laps on three sides
			ParkLifePiece bank = pieces.FirstOrDefault(p => p.Prefab == Prefix + "Pond_Edge_South") ?? natural;
			float bankTop = bank.WorldBounds.max.y;
			ParkHabitatSpot[] frogBanks = Enumerable.Range(0, FrogBanks).Select(i =>
			{
				float angle = Mathf.Deg2Rad * Mathf.Lerp(215f, 325f, i / (float)(FrogBanks - 1));
				Vector3 point = new Vector3(center.x + Mathf.Cos(angle) * (size.x + 0.3f), bankTop,
					center.z + Mathf.Sin(angle) * (size.z + 0.3f));
				return new ParkHabitatSpot(bank.Element, point, 0.2f);
			}).ToArray();
			ParkHabitatSpot[] lapping =
			{
				new ParkHabitatSpot(natural.Element, new Vector3(center.x, 0.1f, center.z - size.z), 1f),
				new ParkHabitatSpot(natural.Element, new Vector3(center.x - size.x, 0.1f, center.z), 1f),
				new ParkHabitatSpot(natural.Element, new Vector3(center.x + size.x, 0.1f, center.z), 1f),
			};
			ParkHabitatSpot fountain = new ParkHabitatSpot(formal.Element,
				new Vector3(formalBounds.center.x, formalBounds.min.y + 0.8f, formalBounds.center.z), 1f);
			ParkHabitatSpot trickle = new ParkHabitatSpot(basin.Element,
				new Vector3(basinBounds.center.x, basinBounds.max.y + 0.2f, basinBounds.center.z), 1f);

			ParkPond pond = AddPart<ParkPond>(root, "Pond");
			pond.Configure(life, water, ponds, radii, reeds, frogBanks, lapping, fountain, FountainHalfWidthMeters, trickle,
				creatures.Ducks, creatures.Frog, creatures.Fish);
			return true;
		}

		/// <summary>
		/// A tree's crown: up its height where the leaves are thickest (low on a pine, which is widest near the ground),
		/// as wide as most of its spread.
		/// </summary>
		private static ParkHabitatSpot Crown(ParkLifePiece tree)
		{
			Bounds bounds = tree.WorldBounds;
			float share = tree.Prefab == PineTree ? PineCrownHeightShare : CrownHeightShare;
			Vector3 center = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * share, bounds.center.z);
			float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * CrownRadiusShare;
			return new ParkHabitatSpot(tree.Element, center, radius);
		}

		/// <summary>Where a woodpecker clings on a broadleaf's trunk, scaled with the tree.</summary>
		private static ParkHabitatSpot Trunk(ParkLifePiece tree)
		{
			(string Prefab, float HeightMeters, float RadiusMeters) trunk = Trunks.First(t => t.Prefab == tree.Prefab);
			Vector3 scale = tree.Entry.Scale;
			return new ParkHabitatSpot(tree.Element, tree.Position + Vector3.up * (trunk.HeightMeters * scale.y),
				trunk.RadiusMeters * scale.x);
		}

		/// <summary>The top of a flat piece (a meadow, the plaza), with a radius inside its edge.</summary>
		private static ParkHabitatSpot Ground(ParkLifePiece piece, float radiusShare)
		{
			Bounds bounds = piece.WorldBounds;
			return new ParkHabitatSpot(piece.Element, new Vector3(bounds.center.x, bounds.max.y + 0.01f, bounds.center.z),
				Mathf.Min(bounds.extents.x, bounds.extents.z) * radiusShare);
		}

		/// <summary>The top middle of a piece (a lamp head, a flower clump).</summary>
		private static ParkHabitatSpot Top(ParkLifePiece piece)
		{
			Bounds bounds = piece.WorldBounds;
			return new ParkHabitatSpot(piece.Element, new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), 0.4f);
		}

		private static T AddPart<T>(GameObject root, string name) where T : Component
		{
			GameObject part = new GameObject(name);
			part.transform.SetParent(root.transform, false);
			return part.AddComponent<T>();
		}

		private static List<ParkLifePiece> Of(IReadOnlyList<ParkLifePiece> pieces, params string[] prefabs)
		{
			return pieces.Where(p => prefabs.Contains(p.Prefab)).ToList();
		}

		/// <summary>Gives the pieces the lights material and returns their renderers.</summary>
		private static Renderer[] Lit(List<ParkLifePiece> pieces, Material lightsMaterial)
		{
			Renderer[] renderers = pieces.Select(p => p.Renderer).Where(r => r != null).ToArray();
			foreach (Renderer target in renderers)
			{
				target.sharedMaterial = lightsMaterial;
			}

			return renderers;
		}

		private static ParkHabitatSpot[] Spots(List<ParkLifePiece> pieces, float heightMeters)
		{
			return pieces.Select(p => new ParkHabitatSpot(p.Element, p.Position + Vector3.up * heightMeters, 1f)).ToArray();
		}

		private static Vector3[] NearestRoute(Vector3 position)
		{
			return RobotRoutes.OrderBy(route => route.Min(point => (point - position).sqrMagnitude)).First();
		}

		/// <summary>The lawn tiles around a mower, joined into one rectangle on the ground plane.</summary>
		private static Rect LawnAround(Vector3 position, List<ParkLifePiece> lawns)
		{
			Bounds? area = null;
			foreach (ParkLifePiece lawn in lawns)
			{
				if (Flat(lawn.Position - position).magnitude > MowerLawnReach)
				{
					continue;
				}

				Bounds bounds = lawn.WorldBounds;
				if (area.HasValue)
				{
					Bounds joined = area.Value;
					joined.Encapsulate(bounds);
					area = joined;
				}
				else
				{
					area = bounds;
				}
			}

			Bounds result = area ?? new Bounds(position, new Vector3(10f, 0f, 10f));
			return Rect.MinMaxRect(result.min.x, result.min.z, result.max.x, result.max.z);
		}

		private static int NearestIndex(List<ParkLifePiece> pieces, Vector3 position, float reach)
		{
			int best = -1;
			float bestDistance = reach;
			for (int i = 0; i < pieces.Count; i++)
			{
				float distance = Flat(pieces[i].Position - position).magnitude;
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = i;
				}
			}

			return best;
		}

		private static Vector3 Flat(Vector3 vector)
		{
			return new Vector3(vector.x, 0f, vector.z);
		}

		private static int CountLevels(int mask)
		{
			int count = 0;
			for (int level = 0; level < ParkLevels.Count; level++)
			{
				count += ParkLevels.Contains(mask, level) ? 1 : 0;
			}

			return count;
		}

		private static Vector3[] Circle(Vector3 center, float radius, int points)
		{
			Vector3[] circle = new Vector3[points];
			for (int i = 0; i < points; i++)
			{
				float angle = 2f * Mathf.PI * i / points;
				circle[i] = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
			}

			return circle;
		}

		#endregion
	}
}
