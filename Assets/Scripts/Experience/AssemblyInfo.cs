using System.Runtime.CompilerServices;

// The scene builder configures components through internal methods, its Play mode check reads and drives them, and
// the tests check the step logic
[assembly: InternalsVisibleTo("DroneDDW.Experience.Editor")]
[assembly: InternalsVisibleTo("DroneDDW.Experience.Tests")]
