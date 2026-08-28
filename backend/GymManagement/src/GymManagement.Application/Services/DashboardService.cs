using GymManagement.Application.DTOs.Dashboard;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using GymManagement.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<RevenueChartDataDto> GetRevenueChartAsync(int months = 6, CancellationToken cancellationToken = default);
    Task<List<ExpiringMembershipDto>> GetExpiringMembershipsAsync(int days = 7, CancellationToken cancellationToken = default);
    Task<List<RecentPaymentDto>> GetRecentPaymentsAsync(int count = 5, CancellationToken cancellationToken = default);
    Task<List<RecentClientDto>> GetRecentClientsAsync(int count = 5, CancellationToken cancellationToken = default);
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

    /// <summary>
    /// Today in the gym's timezone. The owner reads this dashboard against the calendar on
    /// the wall, so "today's takings" has to mean their today, not the server's.
    /// </summary>
    private DateTime GymToday => _clock.Today.ToDateTime(TimeOnly.MinValue);

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var today = GymToday;
        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = firstDayOfMonth.AddMonths(-1);
        var lastMonthEnd = firstDayOfMonth.AddDays(-1);

        var stats = new DashboardStatsDto();

        // Client stats
        stats.TotalActiveClients = await _unitOfWork.Clients.Query()
            .CountAsync(c => MembershipStatuses.AllowedIn.Contains(c.MembershipStatus), cancellationToken);

        stats.TotalClients = await _unitOfWork.Clients.QueryIncludingDeleted()
            .CountAsync(cancellationToken);

        stats.NewClientsThisMonth = await _unitOfWork.Clients.Query()
            .CountAsync(c => c.CreatedAt >= firstDayOfMonth, cancellationToken);

        stats.ExpiringMembershipsCount = await _unitOfWork.Clients.Query()
            .CountAsync(c => c.MembershipEndDate >= today && c.MembershipEndDate <= today.AddDays(7) && MembershipStatuses.AllowedIn.Contains(c.MembershipStatus), cancellationToken);

        // Payment summary (active clients only)
        stats.PaymentSummary = new PaymentSummary
        {
            PaidCount = await _unitOfWork.Clients.Query()
                .CountAsync(c => c.PaymentStatus == PaymentStatus.Paid, cancellationToken),
            PendingCount = await _unitOfWork.Clients.Query()
                .CountAsync(c => c.PaymentStatus == PaymentStatus.Pending, cancellationToken),
            OverdueCount = await _unitOfWork.Clients.Query()
                .CountAsync(c => c.PaymentStatus == PaymentStatus.Overdue, cancellationToken)
        };

        // Revenue summary
        stats.RevenueSummary = new RevenueSummary
        {
            TodayRevenue = await _unitOfWork.Payments.Query()
                .Where(p => p.PaymentDate.Date == today && p.Status == TransactionStatus.Completed)
                .SumAsync(p => p.Amount, cancellationToken),

            ThisMonthRevenue = await _unitOfWork.Payments.Query()
                .Where(p => p.PaymentDate >= firstDayOfMonth && p.Status == TransactionStatus.Completed)
                .SumAsync(p => p.Amount, cancellationToken),

            LastMonthRevenue = await _unitOfWork.Payments.Query()
                .Where(p => p.PaymentDate >= lastMonthStart && p.PaymentDate <= lastMonthEnd && p.Status == TransactionStatus.Completed)
                .SumAsync(p => p.Amount, cancellationToken),

            TotalRevenue = await _unitOfWork.Payments.Query()
                .Where(p => p.Status == TransactionStatus.Completed)
                .SumAsync(p => p.Amount, cancellationToken)
        };

        return stats;
    }

    public async Task<RevenueChartDataDto> GetRevenueChartAsync(int months = 6, CancellationToken cancellationToken = default)
    {
        var result = new RevenueChartDataDto();
        var today = GymToday;
        var startDate = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));

        var payments = await _unitOfWork.Payments.Query()
            .Where(p => p.PaymentDate >= startDate && p.Status == TransactionStatus.Completed)
            .ToListAsync(cancellationToken);

        for (int i = 0; i < months; i++)
        {
            var monthStart = startDate.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var monthPayments = payments.Where(p => p.PaymentDate >= monthStart && p.PaymentDate <= monthEnd);

            result.Data.Add(new RevenueDataPoint
            {
                Label = monthStart.ToString("MMM yyyy"),
                Revenue = monthPayments.Sum(p => p.Amount),
                TransactionCount = monthPayments.Count()
            });
        }

        return result;
    }

    public async Task<List<ExpiringMembershipDto>> GetExpiringMembershipsAsync(int days = 7, CancellationToken cancellationToken = default)
    {
        var today = GymToday;
        var endDate = today.AddDays(days);

        // The day count is worked out after the rows come back, not in the projection:
        // subtracting two dates inside the Select cannot be translated to SQL and threw
        // at runtime ("No coercion operator ... DateTime and Nullable<TimeSpan>"), which
        // made this endpoint a guaranteed 500.
        var expiring = await _unitOfWork.Clients.Query()
            .Include(c => c.CurrentPackage)
            .Where(c => c.MembershipEndDate >= today && c.MembershipEndDate <= endDate && MembershipStatuses.AllowedIn.Contains(c.MembershipStatus))
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
                DaysUntilExpiration = (c.ExpirationDate.Date - today.Date).Days
            })
            .ToList();
    }

    public async Task<List<RecentPaymentDto>> GetRecentPaymentsAsync(int count = 5, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Payments.Query()
            .Include(p => p.Client)
            .OrderByDescending(p => p.PaymentDate)
            .Take(count)
            .Select(p => new RecentPaymentDto
            {
                Id = p.Id,
                ClientName = p.Client.FirstName + " " + p.Client.LastName,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                PaymentMethod = p.PaymentMethod.ToString()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RecentClientDto>> GetRecentClientsAsync(int count = 5, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Clients.Query()
            .Include(c => c.CurrentPackage)
            .OrderByDescending(c => c.CreatedAt)
            .Take(count)
            .Select(c => new RecentClientDto
            {
                Id = c.Id,
                FullName = c.FirstName + " " + c.LastName,
                PhoneNumber = c.PhoneNumber,
                PackageName = c.CurrentPackage != null ? c.CurrentPackage.Name : null,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
