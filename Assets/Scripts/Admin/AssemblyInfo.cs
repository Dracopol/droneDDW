using System.Runtime.CompilerServices;

// The editor tools configure components through internal methods instead of reflection, and the tests check internals
[assembly: InternalsVisibleTo("DroneDDW.Admin.Editor")]
[assembly: InternalsVisibleTo("DroneDDW.Admin.Tests")]

// The experience scene builder sets up the admin menu and Session Control, and its Play mode check reads them
[assembly: InternalsVisibleTo("DroneDDW.Experience.Editor")]
