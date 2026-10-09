namespace DroneDDW.Neighborhood.Versions
{
    /// <summary>
    /// How an element enters and leaves the street. Values match the "_style" codes in the layout file.
    /// </summary>
    public enum NeighborhoodTransitionStyle
    {
        /// <summary>Switches on or off at once.</summary>
        None = 0,

        /// <summary>Grows out of its pivot or shrinks back into it (plants, props, drones). Only the scale
        /// changes, so a moving object can keep moving while it grows.</summary>
        Scale = 1,

        /// <summary>Rises out of the ground or sinks into it (paving, water, buildings, the skyline).</summary>
        Lift = 2,
    }
}
