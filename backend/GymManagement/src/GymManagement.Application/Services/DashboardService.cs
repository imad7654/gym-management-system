using GymManagement.Application.DTOs.Dashboard;
using GymManagement.Domain.Common;
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

    /// <summary>The first screen of the day: the drawer, the call sheet, and who owes.</summary>
    Task<TodayDto> GetTodayAsync(CancellationToken cancellationToken = default);

    /// <summary>Members worth ringing: about to lapse, or recently lapsed.</summary>
    Task<List<NeedsChasingDto>> GetNeedsChasingAsync(CancellationToken cancellationToken = default);

    /// <summary>Records that somebody rang this member, or takes the mark back off.</summary>
    Task<bool> MarkChasedAsync(int clientId, bool called, CancellationToken cancellationToken = default);
}

public class DashboardService : IDashboardService
{
    /// <summary>
    /// The dashboard's expiring list is a call sheet, not a report, so it is capped. The
    /// UI compares this against the "Expiring Soon" count to say when it is showing a
    /// subset; change one and the other stops making sense.
    /// </summary>
    private const int MaxExpiringMemberships = 10;

    /// <summary>
    /// How long after lapsing somebody is still worth a phone call. Beyond a month the call
    /// stops being a nudge and starts being a cold sell, and the list stops being a list
    /// anybody works through.
    /// </summary>
    private const int LapsedWindowDays = 30;

    /// <summary>
    /// The call sheet is a morning's work, not a report. Capped so it stays something the
    /// owner finishes rather than something they scroll.
    /// </summary>
    private const int MaxNeedsChasing = 15;

    /// <summary>Enough owed rows to be worth acting on; the full report is a click away.</summary>
    private const int MaxOwesOnDashboard = 5;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipClock _clock;
    private readonly IReportService _reports;

    public DashboardService(
        IUnitOfWork unitOfWork, IMembershipClock clock, IReportService reports)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _reports = reports;
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

    /// <summary>
    /// The morning: what is in the drawer, who to ring, and who owes.
    ///
    /// Every figure here comes from the services that already own it. The takings on this
    /// page and the takings on the takings report have to be the same number, and the only
    /// way to guarantee that is for there to be one of it - a dashboard that counts money
    /// its own way is exactly how the old one came to disagree with the report about which
    /// day it was.
    /// </summary>
    public async Task<TodayDto> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        var takings = await _reports.GetDailyTakingsAsync(today, cancellationToken);
        var owes = await _reports.GetWhoOwesMoneyAsync(cancellationToken);
        var chasing = await GetNeedsChasingAsync(cancellationToken);

        var (todayStartUtc, todayEndUtc) = _clock.DayBoundsUtc(today);

        // Payments today that actually moved somebody's dates forward. A part payment that
        // fell short of the price is money in the drawer but is not a renewal, and calling
        // it one would tell the owner the chasing worked when nobody has been renewed.
        // Reversals are excluded by their period being null anyway, and by name here.
        var renewals = await _unitOfWork.Payments.Query()
            .CountAsync(p =>
                p.PaymentDate >= todayStartUtc
                && p.PaymentDate < todayEndUtc
                && p.Status == TransactionStatus.Completed
                && p.ReversesPaymentId == null
                && p.PeriodStartDate != null,
                cancellationToken);

        return new TodayDto
        {
            Date = takings.Date,

            CashUsd = takings.CashUsd,
            CashLbpInUsd = takings.CashLbpInUsd,
            CashLbpReceived = takings.CashLbpReceived,
            DrawerTotalUsd = takings.DrawerTotalUsd,
            WhishUsd = takings.WhishUsd,
            OtherUsd = takings.OtherUsd,
            TotalUsd = takings.TotalUsd,
            PaymentCount = takings.PaymentCount,
            ReversalCount = takings.ReversalCount,
            ReversalsUsd = takings.ReversalsUsd,

            RenewalsToday = renewals,

            NeedsChasing = chasing,
            CalledToday = chasing.Count(c => c.CalledToday),

            TotalOwed = owes.TotalOwed,
            OwesCount = owes.MemberCount,
            Owes = owes.Members
                .Take(MaxOwesOnDashboard)
                .Select(m => new OwesSummaryDto
                {
                    ClientId = m.ClientId,
                    ClientName = m.ClientName,
                    AmountOwed = m.AmountOwed,
                    DaysOutstanding = m.DaysOutstanding
                })
                .ToList()
        };
    }

    /// <summary>
    /// The call sheet: members whose membership is about to run out, and those it has
    /// already run out on.
    ///
    /// The lapsed are included on purpose, and sorted first. The expiring list on its own
    /// only ever showed people who had not left yet; the ones already gone are the ones a
    /// phone call actually wins back, and they were the only group no screen surfaced.
    ///
    /// Frozen members are left out. Somebody who told the gym they are travelling does not
    /// want a call asking why they have not been in.
    /// </summary>
    public async Task<List<NeedsChasingDto>> GetNeedsChasingAsync(
        CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var horizon = today.AddDays(Client.ExpiringWindowDays).ToDateTime(TimeOnly.MinValue);
        var lapsedSince = today.AddDays(-LapsedWindowDays).ToDateTime(TimeOnly.MinValue);
        var day = today.ToDateTime(TimeOnly.MinValue);

        var rows = await _unitOfWork.Clients.Query()
            .Include(c => c.CurrentPackage)
            .Where(c =>
                !c.IsSuspended
                && c.MembershipStartDate != null
                && c.MembershipEndDate != null
                && c.MembershipStartDate <= day
                && c.MembershipEndDate >= lapsedSince
                && c.MembershipEndDate <= horizon)
            .OrderBy(c => c.MembershipEndDate)
            .Take(MaxNeedsChasing)
            .ToListAsync(cancellationToken);

        var (todayStartUtc, todayEndUtc) = _clock.DayBoundsUtc(today);

        return rows
            .Select(c => new NeedsChasingDto
            {
                ClientId = c.Id,
                ClientName = c.FullName,
                PhoneNumber = c.PhoneNumber,
                PhoneDigits = PhoneNumberKey.Normalize(c.PhoneNumber),
                PackageName = c.CurrentPackage?.Name,
                MembershipEndDate = c.MembershipEndDate,
                DaysRemaining = c.DaysRemaining(today),
                MembershipStatus = c.MembershipStatusOn(today).ToString(),
                LastChasedAt = c.LastChasedAt,

                // "Today" on the gym's wall, not the server's. Beirut runs ahead of UTC, so
                // for part of every evening a UTC comparison would call this morning's
                // phone call yesterday's and offer the member up to be rung again.
                CalledToday = c.LastChasedAt.HasValue
                    && c.LastChasedAt.Value >= todayStartUtc
                    && c.LastChasedAt.Value < todayEndUtc
            })
            .ToList();
    }

    /// <summary>
    /// Records that somebody rang this member, or takes the mark back off.
    ///
    /// Not audited. The trail exists for money and for accounts; a note that reception
    /// pressed "called" would bury those in noise, and nothing about it can be got wrong in
    /// a way anybody would need to investigate.
    /// </summary>
    public async Task<bool> MarkChasedAsync(
        int clientId, bool called, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.Query()
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

        if (client == null) return false;

        client.LastChasedAt = called ? _clock.UtcNow : null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
