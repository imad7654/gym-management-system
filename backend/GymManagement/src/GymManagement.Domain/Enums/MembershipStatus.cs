namespace GymManagement.Domain.Enums;

/// <summary>
/// A member is always in exactly one of these. Every value except <see cref="Suspended"/>
/// is derived from the membership end date by the nightly job - never typed in by hand.
/// Stored as a string, so the order here does not matter to the database.
/// </summary>
public enum MembershipStatus
{
    /// <summary>Member exists but has never paid. Cannot enter.</summary>
    Pending,

    /// <summary>End date is beyond the expiring-soon window. Can enter.</summary>
    Active,

    /// <summary>End date is within the warning window. Can still enter, with a reminder shown.</summary>
    Expiring,

    /// <summary>End date has passed. Cannot enter.</summary>
    Expired,

    /// <summary>
    /// Paused by the owner for travel or injury. The only status a person sets directly,
    /// which is why the nightly recalculation must leave it alone.
    /// </summary>
    Suspended
}
