using System.Runtime.CompilerServices;

// The editor tools configure components through internal methods instead of reflection, and the tests check internals
[assembly: InternalsVisibleTo("DroneDDW.NarratorOrb.Editor")]
[assembly: InternalsVisibleTo("DroneDDW.NarratorOrb.Tests")]

// The experience scene builder places the orb and sets up its voice
[assembly: InternalsVisibleTo("DroneDDW.Experience.Editor")]
