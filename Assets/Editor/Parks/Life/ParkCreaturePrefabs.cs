using DroneDDW.Parks.Life;

namespace DroneDDW.Parks.Editor.Life
{
	/// <summary>The creature prefabs the life builder hands to the wildlife systems.</summary>
	internal sealed class ParkCreaturePrefabs
	{
		#region Properties

		/// <summary>Robin, blackbird, blue tit and sparrow.</summary>
		internal ParkCreature[] Songbirds { get; set; }

		internal ParkCreature Pigeon { get; set; }
		internal ParkCreature Crow { get; set; }
		internal ParkCreature Woodpecker { get; set; }
		internal ParkCreature Buzzard { get; set; }

		/// <summary>Mallard drake and hen.</summary>
		internal ParkCreature[] Ducks { get; set; }

		internal ParkCreature Frog { get; set; }
		internal ParkCreature Fish { get; set; }
		internal ParkCreature Bee { get; set; }
		internal ParkCreature[] Butterflies { get; set; }
		internal ParkCreature Dragonfly { get; set; }

		#endregion
	}
}
