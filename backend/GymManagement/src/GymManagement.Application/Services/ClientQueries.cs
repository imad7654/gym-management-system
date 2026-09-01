using System.Linq.Expressions;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;

namespace GymManagement.Application.Services;

/// <summary>
/// Membership status as a database question.
///
/// <see cref="Client.StatusFrom"/> is the rule; this is the same rule written as SQL, so
/// the database can filter and sort on a status that is no longer stored anywhere. The two
/// have to agree - a member the list calls Expired and the door calls Active is the exact
/// confusion that deriving the status was meant to end. The tests drive both against the
/// same cases for that reason.
///
/// Every method takes the gym's today rather than reading a clock, so a report for a past
/// date asks the same questions about that date.
/// </summary>
public static class ClientQueries
{
    /// <summary>
    /// Members entitled to train: inside their dates and not frozen.
    ///
    /// Deliberately not "status == Active". A member in their last week is Expiring and is
    /// still perfectly entitled to come in; comparing against Active alone quietly loses
    /// them from every count and every list.
    /// </summary>
    public static IQueryable<Client> AllowedIn(this IQueryable<Client> clients, DateOnly today)
    {
        var day = today.ToDateTime(TimeOnly.MinValue);

        return clients.Where(c =>
            !c.IsSuspended
            && c.MembershipStartDate != null
            && c.MembershipEndDate != null
            && c.MembershipStartDate <= day
            && c.MembershipEndDate >= day);
    }

    /// <summary>
    /// Members whose membership runs out within <paramref name="days"/> and who can still
    /// train today - the call sheet for renewals.
    /// </summary>
    public static IQueryable<Client> ExpiringWithin(
        this IQueryable<Client> clients, int days, DateOnly today)
    {
        var horizon = today.AddDays(days).ToDateTime(TimeOnly.MinValue);

        return clients.AllowedIn(today).Where(c => c.MembershipEndDate <= horizon);
    }

    /// <summary>
    /// Members with exactly one status. Mirrors <see cref="Client.StatusFrom"/> branch for
    /// branch.
    /// </summary>
    public static IQueryable<Client> WithStatus(
        this IQueryable<Client> clients, MembershipStatus status, DateOnly today)
    {
        var day = today.ToDateTime(TimeOnly.MinValue);
        var warnFrom = today.AddDays(Client.ExpiringWindowDays).ToDateTime(TimeOnly.MinValue);

        return status switch
        {
            MembershipStatus.Suspended => clients.Where(c => c.IsSuspended),

            // Never paid, or dated to start later. Both read as Pending because neither is
            // a member who may walk in yet.
            MembershipStatus.Pending => clients.Where(c =>
                !c.IsSuspended
                && (c.MembershipStartDate == null
                    || c.MembershipEndDate == null
                    || c.MembershipStartDate > day)),

            MembershipStatus.Expired => clients.Where(c =>
                !c.IsSuspended
                && c.MembershipStartDate != null
                && c.MembershipEndDate != null
                && c.MembershipEndDate < day),

            MembershipStatus.Expiring =>
                clients.AllowedIn(today).Where(c => c.MembershipEndDate <= warnFrom),

            MembershipStatus.Active =>
                clients.AllowedIn(today).Where(c => c.MembershipEndDate > warnFrom),

            _ => clients
        };
    }

    /// <summary>
    /// A sortable number per status, in the order of the <see cref="MembershipStatus"/>
    /// enum, so "sort by status" means something a person would predict.
    ///
    /// It replaces ordering on the old stored column, which was saved as text and so sorted
    /// alphabetically - Active, Expired, Expiring, Pending - an order nobody asked for.
    /// </summary>
    public static Expression<Func<Client, int>> StatusRank(DateOnly today)
    {
        var day = today.ToDateTime(TimeOnly.MinValue);
        var warnFrom = today.AddDays(Client.ExpiringWindowDays).ToDateTime(TimeOnly.MinValue);

        return c =>
            c.IsSuspended ? (int)MembershipStatus.Suspended
            : c.MembershipStartDate == null || c.MembershipEndDate == null ? (int)MembershipStatus.Pending
            : c.MembershipStartDate > day ? (int)MembershipStatus.Pending
            : c.MembershipEndDate < day ? (int)MembershipStatus.Expired
            : c.MembershipEndDate <= warnFrom ? (int)MembershipStatus.Expiring
            : (int)MembershipStatus.Active;
    }
}
