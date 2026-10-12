using System.Runtime.CompilerServices;

// The editor tools configure components through internal methods instead of reflection.
[assembly: InternalsVisibleTo("DroneDDW.Neighborhood.Editor")]

// The experience scene builder wires the practice table's parts
[assembly: InternalsVisibleTo("DroneDDW.Experience.Editor")]

// The tests check the push button's cap
[assembly: InternalsVisibleTo("DroneDDW.Neighborhood.Tests")]
