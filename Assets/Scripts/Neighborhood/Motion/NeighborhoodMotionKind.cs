namespace DroneDDW.Neighborhood.Motion
{
    /// <summary>How a <see cref="NeighborhoodMover"/> moves. Values match "_kind" in the layout.</summary>
    public enum NeighborhoodMotionKind
    {
        None = 0,

        /// <summary>Round a closed loop of points (corridor drones, lane robots).</summary>
        Path = 1,

        /// <summary>Round an ellipse (ducks on the pond, drones circling).</summary>
        Orbit = 2,

        /// <summary>Stays put, bobbing and swaying a little (drones at a balcony or over the hub).</summary>
        Hover = 3,

        /// <summary>Small hops and turns now and then (the robin on the table).</summary>
        Hop = 4,
    }
}
