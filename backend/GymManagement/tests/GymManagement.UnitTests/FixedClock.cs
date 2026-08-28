using GymManagement.Application.Interfaces;

namespace GymManagement.UnitTests;

/// <summary>
/// A gym clock frozen on one date, so tests about membership dates and daily takings do not
/// quietly change behaviour at midnight or at the turn of a month.
///
/// Shared rather than redefined per test class: five private copies meant a single change to
/// <see cref="IMembershipClock"/> broke five files, and nothing about the fake differs
/// between them.
/// </summary>
public sealed class FixedClock : IMembershipClock
{
    private readonly TimeSpan _offset;

    /// <param name="today">The gym's date.</param>
    /// <param name="utcOffsetHours">
    /// How far the gym runs ahead of UTC. Defaults to zero so existing tests keep comparing
    /// plain dates; pass Beirut's +3 to exercise the day-boundary conversion.
    /// </param>
    public FixedClock(DateOnly today, int utcOffsetHours = 0)
    {
        Today = today;
        _offset = TimeSpan.FromHours(utcOffsetHours);
    }

    public DateOnly Today { get; }

    public DateTime UtcNow => Today.ToDateTime(TimeOnly.MinValue) - _offset;

    public (DateTime StartUtc, DateTime EndUtc) DayBoundsUtc(DateOnly date) =>
        (date.ToDateTime(TimeOnly.MinValue) - _offset,
         date.AddDays(1).ToDateTime(TimeOnly.MinValue) - _offset);
}
