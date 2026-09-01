namespace GymManagement.Domain.Enums;

/// <summary>
/// A member is always in exactly one of these on any given day.
///
/// Nothing stores this. Every value except <see cref="Suspended"/> is worked out from the
/// membership end date whenever it is asked for, by <c>Client.StatusFrom</c> in memory and
/// by <c>ClientQueries</c> in SQL. It was a stored column until 31 August 2026, kept in
/// step by a nightly job that was never actually written - so expired members went on
/// reading <see cref="Active"/> forever. There is no longer a copy that can go stale.
///
/// The order of the values is the order "sort by status" uses, so it is not arbitrary.
/// </summary>
public enum MembershipStatus
{
    /// <summary>Member exists but has never paid, or their start date is still ahead. Cannot enter.</summary>
    Pending,

    /// <summary>End date is beyond the expiring-soon window. Can enter.</summary>
    Active,

    /// <summary>End date is within the warning window. Can still enter, with a reminder shown.</summary>
    Expiring,

    /// <summary>End date has passed. Cannot enter.</summary>
    Expired,

    /// <summary>
    /// Paused by the owner for travel or injury. The only value a person sets directly,
    /// which is why it is the only one with a stored column behind it, and why it wins
    /// over whatever the dates say.
    /// </summary>
    Suspended
}
