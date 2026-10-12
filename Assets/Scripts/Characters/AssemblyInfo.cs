using System.Runtime.CompilerServices;

// The editor tools configure components through internal methods instead of reflection, and the tests check internals
[assembly: InternalsVisibleTo("DroneDDW.Characters.Editor")]
[assembly: InternalsVisibleTo("DroneDDW.Characters.Tests")]

// The experience scene builder makes its portrait cards the way the test scene does
[assembly: InternalsVisibleTo("DroneDDW.Experience.Editor")]
