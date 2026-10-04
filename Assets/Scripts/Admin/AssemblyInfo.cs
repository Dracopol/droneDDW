using System.Runtime.CompilerServices;

// The editor tools configure components through internal methods instead of reflection, and the tests check internals
[assembly: InternalsVisibleTo("DroneDDW.Admin.Editor")]
[assembly: InternalsVisibleTo("DroneDDW.Admin.Tests")]
