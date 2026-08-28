namespace GymManagement.Application.Interfaces;

/// <summary>
/// Separates the two kinds of time this system deals with.
///
/// Timestamps - when a payment was taken, when someone scanned in - are instants, stored
/// in UTC. A membership end date is not an instant: it is a date on the calendar hanging
/// on the gym's wall, and it ends at the gym's midnight, not at UTC midnight.
///
/// Beirut is ahead of UTC, so for part of every day <c>DateTime.UtcNow.Date</c> is still
/// yesterday as far as the gym is concerned. Comparing an end date against it is what makes
/// a membership expire a day early and a member argue at the desk.
/// </summary>
public interface IMembershipClock
{
    /// <summary>Now, as an instant. Use for anything being recorded as "when this happened".</summary>
    DateTime UtcNow { get; }

    /// <summary>Today's date in the gym's own timezone. Use for every membership date comparison.</summary>
    DateOnly Today { get; }

    /// <summary>
    /// The two UTC instants that bracket one calendar day at the gym: start inclusive, end
    /// exclusive.
    ///
    /// Payment timestamps are stored in UTC, but "what did we take today" means the day on
    /// the gym's wall calendar. Beirut runs ahead of UTC, so filtering takings on the UTC
    /// date files the end of every evening under the wrong day - and the owner counting the
    /// drawer against it would find money missing that is really just sitting in tomorrow.
    /// </summary>
    (DateTime StartUtc, DateTime EndUtc) DayBoundsUtc(DateOnly date);
}
