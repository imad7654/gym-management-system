namespace GymManagement.Domain.Enums;

/// <summary>
/// Groupings of <see cref="MembershipStatus"/> that the door scanner and in-memory checks
/// ask about.
///
/// These exist because "active" in the everyday sense - a paid-up member who may walk in -
/// is not the same as the single <see cref="MembershipStatus.Active"/> value. A member in
/// their last week is <see cref="MembershipStatus.Expiring"/> and is still perfectly
/// entitled to train. Comparing against Active alone quietly loses them from every count
/// and every list.
///
/// For database queries use <c>ClientQueries.AllowedIn(today)</c> instead. Since the status
/// stopped being stored there is no column to compare against, and the dates have to be
/// asked directly.
/// </summary>
public static class MembershipStatuses
{
    /// <summary>
    /// Statuses that allow entry to the gym. Expiring members are warned, not refused.
    /// </summary>
    public static readonly MembershipStatus[] AllowedIn =
    {
        MembershipStatus.Active,
        MembershipStatus.Expiring
    };

    /// <summary>Whether a status lets the member through the door.</summary>
    public static bool AllowsEntry(MembershipStatus status) =>
        status is MembershipStatus.Active or MembershipStatus.Expiring;
}
