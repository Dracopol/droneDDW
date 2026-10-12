using System.Runtime.CompilerServices;

// The experience scene builder makes its portrait cards the way the test scene does
[assembly: InternalsVisibleTo("DroneDDW.Experience.Editor")]

// The tests check the portrait measurements against the files
[assembly: InternalsVisibleTo("DroneDDW.Characters.Tests")]
