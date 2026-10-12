using System.Runtime.CompilerServices;

// The experience scene builder makes its practice table with the table builder
[assembly: InternalsVisibleTo("DroneDDW.Experience.Editor")]

// The tests check the generated low-poly meshes
[assembly: InternalsVisibleTo("DroneDDW.Neighborhood.Tests")]
