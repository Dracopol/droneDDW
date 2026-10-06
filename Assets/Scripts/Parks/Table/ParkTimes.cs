namespace DroneDDW.Parks.Table
{
    public static class ParkTimes
    {
        public const int Count = 3;

        private static readonly int[] Years = { 2026, 2050, 2100 };

        public static int Clamp(int step)
        {
            return step < 0 ? 0 : (step >= Count ? Count - 1 : step);
        }

        public static int GetYear(int step)
        {
            return Years[Clamp(step)];
        }
    }

    public readonly struct ParkTimeChange
    {
        public ParkTimeChange(int previous, int next, bool isImmediate)
        {
            Previous = previous;
            Next = next;
            IsImmediate = isImmediate;
        }

        public int Previous { get; }
        public int Next { get; }
        public bool IsImmediate { get; }
    }
}