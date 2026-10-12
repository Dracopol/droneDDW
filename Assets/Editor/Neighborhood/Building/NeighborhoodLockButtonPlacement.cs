namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>Where the table builder puts the lock-in button; the two are compared in the lock button test scenes.</summary>
    internal enum NeighborhoodLockButtonPlacement
    {
        /// <summary>In the table's right corner, between the slider's end and the table's edge, as big as fits there.</summary>
        TableCorner,

        /// <summary>On a stand of its own beside the table's right edge, as big as the Interaction Tests buttons.</summary>
        Stand,
    }
}
