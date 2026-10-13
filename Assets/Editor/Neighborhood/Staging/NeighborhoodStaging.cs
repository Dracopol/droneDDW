using System.Collections.Generic;
using DroneDDW.Neighborhood.Characters;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Staging
{
    /// <summary>
    /// Where the scene builder puts the three people. The street export has them all around the seat, up to 18 m
    /// away and one of them behind; staged, they talk in front of the seat, where a seated participant sees them while
    /// using the slider. A few street objects move too (the Balanced pond, nearer to Grandma). Made once with the
    /// defaults as Data/Neighborhood Staging.asset under the Neighborhood content folder and never overwritten: edit it,
    /// then build the scene again. Switch it off to build the street as exported.
    /// </summary>
    internal sealed class NeighborhoodStaging : ScriptableObject
    {
        #region Serialized fields

        [SerializeField, Tooltip("Off: the people stay where the street export put them.")]
        private bool _isEnabled = true;

        [SerializeField] private List<NeighborhoodStagingSpot> _spots = new List<NeighborhoodStagingSpot>();

        [SerializeField, Tooltip("Street objects moved as a group, without a person (the Balanced pond nearer to Grandma).")]
        private List<NeighborhoodStagingShift> _shifts = new List<NeighborhoodStagingShift>();

        #endregion

        #region Properties

        public bool IsEnabled => _isEnabled;
        public IReadOnlyList<NeighborhoodStagingSpot> Spots => _spots;
        public IReadOnlyList<NeighborhoodStagingShift> Shifts => _shifts;

        #endregion

        #region Internal

        internal void SetDefaults()
        {
            _isEnabled = true;
            _spots = CreateDefaultSpots();
            _shifts = CreateDefaultShifts();
        }

        /// <summary>
        /// The Balanced pond is smaller than the Nature one, so its shore is twice as far from Grandma (3.1 m against
        /// 1.7 m); moved 1.4 m east, with its reeds and ducks, it comes as near as in Nature.
        /// </summary>
        internal static List<NeighborhoodStagingShift> CreateDefaultShifts()
        {
            return new List<NeighborhoodStagingShift>
            {
                new NeighborhoodStagingShift("the Balanced pond nearer to Grandma", new Vector3(1.4f, 0f, 0f),
                    new[]
                    {
                        "Pond_B_01", "Pond_B_02", "Pond_B_03", "Pond_B_04", "Pond_B_05", "Pond_B_06", "Pond_B_07",
                        "Ducks_B_01", "Ducks_B_02",
                    }),
            };
        }

        /// <summary>
        /// Grandma on the pond's east shore, half turned to the ducks; the worker with his lamp beside the north path
        /// (near enough for her "that lamp's still out"); the student and her dog in front of the vegetable garden.
        /// </summary>
        internal static List<NeighborhoodStagingSpot> CreateDefaultSpots()
        {
            return new List<NeighborhoodStagingSpot>
            {
                new NeighborhoodStagingSpot(NeighborhoodCharacterId.Grandma, new Vector3(-3f, 0f, 7.4f), 57f,
                    new[] { "GrandmaBench_N_01", "GrandmaBench_B_01", "GrandmaBench_E_01" },
                    new string[0]),
                new NeighborhoodStagingSpot(NeighborhoodCharacterId.Worker, new Vector3(2f, 0f, 10.2f), 0f,
                    new[]
                    {
                        "CastProps_N_01", "WorkerLamp_N_01", "WorkerLamp_N_02", "WorkerLamp_B_01", "Lamps_B_01",
                        "WorkerLamp_E_01", "WorkerLamp_E_02", "Lamps_E_01", "Robots_E_02", "Robots_E_03", "Drones_E_02",
                    },
                    new string[0]),
                new NeighborhoodStagingSpot(NeighborhoodCharacterId.Student, new Vector3(3.6f, 0f, 7.6f), 0f,
                    new[] { "Dog_N_01", "Dog_B_01", "Dog_E_01" },
                    new string[0]),
            };
        }

        #endregion
    }
}
