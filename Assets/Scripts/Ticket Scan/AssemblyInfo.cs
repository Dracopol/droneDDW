using System.Runtime.CompilerServices;

// The scene builder configures components through internal methods, and the tests drive the decoder and code reader
[assembly: InternalsVisibleTo("DroneDDW.TicketScan.Editor")]
[assembly: InternalsVisibleTo("DroneDDW.TicketScan.Tests")]

// The experience scene builder adds the camera scanner for the admin menu's check-in screen
[assembly: InternalsVisibleTo("DroneDDW.Experience.Editor")]
