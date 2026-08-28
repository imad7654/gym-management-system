namespace GymManagement.Domain.Enums;

/// <summary>
/// Groupings of <see cref="MembershipStatus"/> that queries and the door scanner ask about.
///
/// These exist because "active" in the everyday sense - a paid-up member who may walk in -
/// is not the same as the single <see cref="MembershipStatus.Active"/> value. A member in
/// their last week is <see cref="MembershipStatus.Expiring"/> and is still perfectly
/// entitled to train. Comparing against Active alone quietly loses them from every count
/// and every list.
/// </summary>
public static class MembershipStatuses
{
    /// <summary>
    /// Statuses that allow entry to the gym. Expiring members are warned, not refused.
    /// Written as an array so EF Core translates <c>Contains</c> into a SQL IN clause.
    /// </summary>
    public static readonly MembershipStatus[] AllowedIn =
    {
        MembershipStatus.Active,
        MembershipStatus.Expiring
    };
}
