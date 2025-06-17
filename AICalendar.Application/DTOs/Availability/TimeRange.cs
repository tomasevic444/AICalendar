
namespace AICalendar.Application.DTOs.Availability
{
    public struct TimeRange : IComparable<TimeRange>
    {
        public DateTime StartTimeUtc { get; }
        public DateTime EndTimeUtc { get; }

        public TimeRange(DateTime startTimeUtc, DateTime endTimeUtc)
        {
            if (startTimeUtc > endTimeUtc)
            {
                // Or swap them, or handle as an invalid state earlier
                throw new ArgumentException("StartTimeUtc cannot be after EndTimeUtc.");
            }
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
        }

        public TimeSpan Duration => EndTimeUtc - StartTimeUtc;

        // For sorting and merging
        public int CompareTo(TimeRange other)
        {
            int startComparison = StartTimeUtc.CompareTo(other.StartTimeUtc);
            if (startComparison != 0)
            {
                return startComparison;
            }
            return EndTimeUtc.CompareTo(other.EndTimeUtc);
        }

        public override string ToString() => $"[{StartTimeUtc:O} - {EndTimeUtc:O}]";
    }
}
