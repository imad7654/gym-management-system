using GymManagement.Application.DTOs.Dashboard;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using GymManagement.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<List<ExpiringMembershipDto>> GetExpiringMembershipsAsync(int days = Client.ExpiringWindowDays, CancellationToken cancellationToken = default);
}

public class DashboardService : IDashboardService
{
    /// <summary>
    /// The dashboard's expiring list is a call sheet, not a report, so it is capped. The
    /// UI compares this against the "Expiring Soon" count to say when it is showing a
    /// subset; change one and the other stops making sense.
    /// </summary>
    private const int MaxExpiringMemberships = 10;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipClock _clock;

    public DashboardService(IUnitOfWork unitOfWork, IMembershipClock clock)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        // Payment timestamps are UTC instants, but the owner reads this against the
        // calendar on the wall. Beirut runs ahead of UTC, so comparing a UTC date against
        // the gym's date files the end of every evening under the wrong day - and this
        // screen would then disagree with the daily takings report, which has always
        // asked the clock properly. Two screens showing two numbers for the same day is
        // what stops either being trusted.
        var (todayStartUtc, todayEndUtc) = _clock.DayBoundsUtc(today);

        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var monthStartUtc = _clock.DayBoundsUtc(firstOfMonth).StartUtc;
        var monthEndUtc = _clock.DayBoundsUtc(firstOfMonth.AddMonths(1)).StartUtc;
        var lastMonthStartUtc = _clock.DayBoundsUtc(firstOfMonth.AddMonths(-1)).StartUtc;

        var stats = new DashboardStatsDto();

        // Members entitled to train right now, asked of the dates rather than of a stored
        // status. Expiring members are included: they are in their last week and still
        // perfectly entitled to come in.
        stats.TotalActiveClients = await _unitOfWork.Clients.Query()
            .AllowedIn(today)
            .CountAsync(cancellationToken);

        stats.TotalClients = await _unitOfWork.Clients.QueryIncludingDeleted()
            .CountAsync(cancellationToken);

        stats.NewClientsThisMonth = await _unitOfWork.Clients.Query()
            .CountAsync(c => c.CreatedAt >= monthStartUtc, cancellationToken);

        stats.ExpiringMembershipsCount = await _unitOfWork.Clients.Query()
            .ExpiringWithin(Client.ExpiringWindowDays, today)
            .CountAsync(cancellationToken);

        stats.PaymentSummary = new PaymentSummary
        {
            PaidCount = await _unitOfWork.Clients.Query()
                .CountAsync(c => c.PaymentStatus == PaymentStatus.Paid, cancellationToken),
            PendingCount = await _unitOfWork.Clients.Query()
                .CountAsync(c => c.PaymentStatus == PaymentStatus.Pending, cancellationToken),
            OwesMoneyCount = await _unitOfWork.Clients.Query()
                .CountAsync(c => c.PaymentStatus == PaymentStatus.Partial, cancellationToken)
        };

        // Reversals are Completed rows carrying a negative amount, so summing Completed
        // nets them off correctly. That is deliberate - see PaymentService.
        stats.RevenueSummary = new RevenueSummary
        {
            TodayRevenue = await SumCompletedBetween(todayStartUtc, todayEndUtc, cancellationToken),
            ThisMonthRevenue = await SumCompletedBetween(monthStartUtc, monthEndUtc, cancellationToken),
            LastMonthRevenue = await SumCompletedBetween(lastMonthStartUtc, monthStartUtc, cancellationToken),

            TotalRevenue = await _unitOfWork.Payments.Query()
                .Where(p => p.Status == TransactionStatus.Completed)
                .SumAsync(p => p.Amount, cancellationToken)
        };

        return stats;
    }

    /// <summary>Completed money between two instants, start inclusive and end exclusive.</summary>
    private Task<decimal> SumCompletedBetween(
        DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken) =>
        _unitOfWork.Payments.Query()
            .Where(p => p.PaymentDate >= startUtc
                     && p.PaymentDate < endUtc
                     && p.Status == TransactionStatus.Completed)
            .SumAsync(p => p.Amount, cancellationToken);

    public async Task<List<ExpiringMembershipDto>> GetExpiringMembershipsAsync(
        int days = Client.ExpiringWindowDays, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        // The day count is worked out after the rows come back, not in the projection:
        // subtracting two dates inside the Select cannot be translated to SQL and threw
        // at runtime ("No coercion operator ... DateTime and Nullable<TimeSpan>"), which
        // made this endpoint a guaranteed 500.
        var expiring = await _unitOfWork.Clients.Query()
            .Include(c => c.CurrentPackage)
            .ExpiringWithin(days, today)
            .OrderBy(c => c.MembershipEndDate)
            .Take(MaxExpiringMemberships)
            .Select(c => new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                c.PhoneNumber,
                PackageName = c.CurrentPackage != null ? c.CurrentPackage.Name : "N/A",
                ExpirationDate = c.MembershipEndDate!.Value
            })
            .ToListAsync(cancellationToken);

        return expiring
            .Select(c => new ExpiringMembershipDto
            {
                ClientId = c.Id,
                ClientName = c.FirstName + " " + c.LastName,
                PhoneNumber = c.PhoneNumber,
                PackageName = c.PackageName,
                ExpirationDate = c.ExpirationDate,
                DaysUntilExpiration =
                    DateOnly.FromDateTime(c.ExpirationDate).DayNumber - today.DayNumber
            })
            .ToList();
    }
}
