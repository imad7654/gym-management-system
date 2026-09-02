using GymManagement.Application.DTOs.Reports;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IReportService
{
    /// <summary>Members who have part-paid for a package and still owe the difference.</summary>
    Task<WhoOwesMoneyDto> GetWhoOwesMoneyAsync(CancellationToken cancellationToken = default);

    /// <summary>One day's money, split so the owner can count the drawer against it.</summary>
    Task<DailyTakingsDto> GetDailyTakingsAsync(DateOnly? date = null, CancellationToken cancellationToken = default);

    /// <summary>Revenue and membership month by month, counted as cash in.</summary>
    Task<RevenueTrendDto> GetRevenueTrendAsync(int months = 12, CancellationToken cancellationToken = default);

    /// <summary>One month opened up: the same split the daily report shows, a level higher.</summary>
    Task<RevenueMonthDetailDto> GetRevenueMonthAsync(int year, int month, CancellationToken cancellationToken = default);
}

/// <summary>
/// The owner's money reports.
///
/// These are read from the payment rows themselves rather than from any running total kept
/// on the member. A number the system maintains separately is a number that can drift, and
/// the whole reason the owner wanted this system is to be able to check it.
/// </summary>
public class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipClock _clock;

    public ReportService(IUnitOfWork unitOfWork, IMembershipClock clock)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<WhoOwesMoneyDto> GetWhoOwesMoneyAsync(CancellationToken cancellationToken = default)
    {
        // The same definition the desk credits a new payment against, so the report can
        // never bill someone for money the payment screen has already taken.
        var outstanding = await _unitOfWork.Payments.Query()
            .OutstandingCredit()
            .Include(p => p.Client)
            .Include(p => p.Package)
            .ToListAsync(cancellationToken);

        var today = _clock.Today;

        var members = outstanding
            // A payment whose member has been removed comes back with no Client, because
            // the soft-delete filter drops them. Chasing a deleted member for money is not
            // something to put in front of the owner.
            .Where(p => p.Client != null && p.Package != null)
            .GroupBy(p => new { p.ClientId, p.PackageId })
            .Select(group =>
            {
                var first = group.OrderBy(p => p.PaymentDate).First();
                var paid = group.Sum(p => p.Amount);
                var price = first.Package.Price;

                return new OwedAmountDto
                {
                    ClientId = group.Key.ClientId,
                    ClientName = first.Client.FullName,
                    PhoneNumber = first.Client.PhoneNumber,
                    PackageName = first.Package.Name,
                    PackagePrice = price,
                    AmountPaid = paid,
                    AmountOwed = price - paid,
                    OwingSince = first.PaymentDate,
                    DaysOutstanding = today.DayNumber - DateOnly.FromDateTime(first.PaymentDate).DayNumber,
                    MembershipStatus = first.Client.MembershipStatusOn(today).ToString()
                };
            })
            // Only real debts. A group can net to zero or below when a part payment was
            // reversed - the money is square, and the member has no business on this list.
            .Where(row => row.AmountOwed > 0 && row.AmountPaid > 0)
            .OrderByDescending(row => row.DaysOutstanding)
            .ThenByDescending(row => row.AmountOwed)
            .ToList();

        return new WhoOwesMoneyDto
        {
            TotalOwed = members.Sum(row => row.AmountOwed),
            MemberCount = members.Count,
            Members = members
        };
    }

    public async Task<DailyTakingsDto> GetDailyTakingsAsync(
        DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var day = date ?? _clock.Today;
        var (startUtc, endUtc) = _clock.DayBoundsUtc(day);

        var movements = await _unitOfWork.Payments.Query()
            .Include(p => p.Client)
            .Include(p => p.Package)
            .Where(p => p.PaymentDate >= startUtc
                     && p.PaymentDate < endUtc
                     && p.Status == TransactionStatus.Completed)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        // Reversals are in every figure below, as negatives. Money handed back today really
        // did leave the drawer, so a report that left them out would send the owner hunting
        // for a shortfall they created themselves.
        decimal SumUsd(Func<Payment, bool> predicate) =>
            movements.Where(predicate).Sum(p => p.Amount);

        var cashUsd = SumUsd(p => p.PaymentMethod == PaymentMethod.Cash && p.Currency == Currency.Usd);

        var lbpRows = movements
            .Where(p => p.PaymentMethod == PaymentMethod.Cash && p.Currency == Currency.Lbp)
            .ToList();

        // Converted at the rate each payment was taken at, not at today's rate. The notes in
        // the drawer are worth what they were worth when they were handed over, and
        // reconverting old cash at a new rate would make the count drift every morning.
        var cashLbpInUsd = lbpRows.Sum(p => p.Amount);
        var cashLbpReceived = lbpRows.Sum(p => p.AmountReceived);

        var whishUsd = SumUsd(p => p.PaymentMethod == PaymentMethod.Whish);
        var otherUsd = SumUsd(p => p.PaymentMethod == PaymentMethod.Other);

        return new DailyTakingsDto
        {
            Date = day.ToDateTime(TimeOnly.MinValue),

            CashUsd = cashUsd,
            CashLbpReceived = cashLbpReceived,
            CashLbpInUsd = cashLbpInUsd,
            DrawerTotalUsd = cashUsd + cashLbpInUsd,

            WhishUsd = whishUsd,
            OtherUsd = otherUsd,
            TotalUsd = cashUsd + cashLbpInUsd + whishUsd + otherUsd,

            PaymentCount = movements.Count(p => !p.IsReversal),
            ReversalCount = movements.Count(p => p.IsReversal),
            ReversalsUsd = movements.Where(p => p.IsReversal).Sum(p => p.Amount),

            Payments = movements.Select(p => new TakingsPaymentDto
            {
                Id = p.Id,
                TakenAt = p.PaymentDate,
                ClientName = p.Client?.FullName ?? "(removed member)",
                PackageName = p.Package?.Name ?? "(deleted package)",
                PaymentMethod = p.PaymentMethod.ToString(),
                Currency = p.Currency.ToString(),
                AmountReceived = p.AmountReceived,
                AmountUsd = p.Amount,
                ExchangeRate = p.ExchangeRate,
                IsReversal = p.IsReversal
            }).ToList()
        };
    }

    /// <summary>
    /// Revenue and membership month by month.
    ///
    /// Money is counted <b>when it was taken</b>, whole, in the month it arrived. A member
    /// paying for three months in January makes January large and February quiet, and that
    /// is deliberate: it is what the drawer did, and it is what the daily takings report
    /// and the dashboard already say. Spreading each payment across the months it bought
    /// would be reasonable accounting and would leave two screens disagreeing about March,
    /// which is the failure this system spends most of its effort avoiding.
    ///
    /// All the arithmetic is done in memory over one query rather than as twelve grouped
    /// queries, because the month boundaries have to come from the gym's clock - Beirut
    /// runs ahead of UTC, so a payment taken late on the 31st belongs to the month that
    /// ended, and SQL grouping on the raw UTC timestamp would file it under the next one.
    /// </summary>
    public async Task<RevenueTrendDto> GetRevenueTrendAsync(
        int months = 12, CancellationToken cancellationToken = default)
    {
        var monthCount = Math.Clamp(months, 1, 36);
        var today = _clock.Today;

        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(monthCount - 1));

        // Each month's window, as UTC instants, asked of the gym's clock.
        var windows = Enumerable.Range(0, monthCount)
            .Select(offset =>
            {
                var start = firstMonth.AddMonths(offset);
                return new
                {
                    Start = start,
                    StartUtc = _clock.DayBoundsUtc(start).StartUtc,
                    EndUtc = _clock.DayBoundsUtc(start.AddMonths(1)).StartUtc,

                    // Where the membership count is taken. For a finished month that is its
                    // last day; for the month in progress it is today, because counting a
                    // date that has not happened yet asks how many memberships will still
                    // be running on the 30th - and most will not have been renewed, so the
                    // newest bar would show a collapse every single month.
                    CountOn = Min(start.AddMonths(1).AddDays(-1), today),

                    // True for the month still being lived through, so the chart can say so
                    // rather than letting two days of takings read as a bad month.
                    InProgress = start.Year == today.Year && start.Month == today.Month
                };
            })
            .ToList();

        var windowStartUtc = windows[0].StartUtc;
        var windowEndUtc = windows[^1].EndUtc;

        var movements = await _unitOfWork.Payments.Query()
            .Where(p => p.PaymentDate >= windowStartUtc
                     && p.PaymentDate < windowEndUtc
                     && p.Status == TransactionStatus.Completed)
            .ToListAsync(cancellationToken);

        // Membership dates for everyone, including members removed since: they were really
        // training in those months, and dropping them would rewrite the past every time the
        // owner tidies the member list.
        var memberships = await _unitOfWork.Clients.QueryIncludingDeleted()
            .Where(c => c.MembershipStartDate != null && c.MembershipEndDate != null)
            .Select(c => new { c.MembershipStartDate, c.MembershipEndDate })
            .ToListAsync(cancellationToken);

        var result = new RevenueTrendDto();

        foreach (var window in windows)
        {
            var inMonth = movements
                .Where(p => p.PaymentDate >= window.StartUtc && p.PaymentDate < window.EndUtc)
                .ToList();

            var drawerUsd = inMonth
                .Where(p => p.PaymentMethod == PaymentMethod.Cash)
                .Sum(p => p.Amount);

            var whishUsd = inMonth
                .Where(p => p.PaymentMethod == PaymentMethod.Whish)
                .Sum(p => p.Amount);

            var otherUsd = inMonth
                .Where(p => p.PaymentMethod == PaymentMethod.Other)
                .Sum(p => p.Amount);

            var countOn = window.CountOn.ToDateTime(TimeOnly.MinValue);

            result.Months.Add(new RevenueMonthDto
            {
                Year = window.Start.Year,
                Month = window.Start.Month,
                Label = MonthLabel(window.Start),

                TotalUsd = drawerUsd + whishUsd + otherUsd,
                DrawerUsd = drawerUsd,
                WhishUsd = whishUsd,
                OtherUsd = otherUsd,

                PaymentCount = inMonth.Count(p => !p.IsReversal),
                ReversalCount = inMonth.Count(p => p.IsReversal),
                ReversalsUsd = inMonth.Where(p => p.IsReversal).Sum(p => p.Amount),

                ActiveMembers = memberships.Count(m =>
                    m.MembershipStartDate <= countOn && m.MembershipEndDate >= countOn),

                InProgress = window.InProgress
            });
        }

        result.TotalUsd = result.Months.Sum(m => m.TotalUsd);
        result.AverageMonthUsd = result.Months.Count == 0
            ? 0m
            : Math.Round(result.TotalUsd / result.Months.Count, 2);

        var best = result.Months.OrderByDescending(m => m.TotalUsd).FirstOrDefault();
        result.BestMonthLabel = best?.Label;
        result.BestMonthUsd = best?.TotalUsd ?? 0m;

        return result;
    }

    /// <summary>
    /// One month opened up. The same split as the daily takings report, a level higher, so
    /// the drill-down reads familiar the first time somebody clicks a bar.
    /// </summary>
    public async Task<RevenueMonthDetailDto> GetRevenueMonthAsync(
        int year, int month, CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            throw new BusinessException($"{month} is not a month.");
        }

        var start = new DateOnly(year, month, 1);
        var startUtc = _clock.DayBoundsUtc(start).StartUtc;
        var endUtc = _clock.DayBoundsUtc(start.AddMonths(1)).StartUtc;

        var movements = await _unitOfWork.Payments.Query()
            .Include(p => p.Client)
            .Include(p => p.Package)
            .Where(p => p.PaymentDate >= startUtc
                     && p.PaymentDate < endUtc
                     && p.Status == TransactionStatus.Completed)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        decimal SumUsd(Func<Payment, bool> predicate) =>
            movements.Where(predicate).Sum(p => p.Amount);

        var cashUsd = SumUsd(p => p.PaymentMethod == PaymentMethod.Cash && p.Currency == Currency.Usd);

        var lbpRows = movements
            .Where(p => p.PaymentMethod == PaymentMethod.Cash && p.Currency == Currency.Lbp)
            .ToList();

        // Each payment at the rate it was taken at, never at today's rate - the same rule
        // the daily report follows, for the same reason.
        var cashLbpInUsd = lbpRows.Sum(p => p.Amount);
        var whishUsd = SumUsd(p => p.PaymentMethod == PaymentMethod.Whish);
        var otherUsd = SumUsd(p => p.PaymentMethod == PaymentMethod.Other);

        return new RevenueMonthDetailDto
        {
            Year = year,
            Month = month,
            Label = MonthLabel(start),

            CashUsd = cashUsd,
            CashLbpInUsd = cashLbpInUsd,
            CashLbpReceived = lbpRows.Sum(p => p.AmountReceived),
            DrawerUsd = cashUsd + cashLbpInUsd,
            WhishUsd = whishUsd,
            OtherUsd = otherUsd,
            TotalUsd = cashUsd + cashLbpInUsd + whishUsd + otherUsd,

            PaymentCount = movements.Count(p => !p.IsReversal),
            ReversalCount = movements.Count(p => p.IsReversal),
            ReversalsUsd = movements.Where(p => p.IsReversal).Sum(p => p.Amount),

            // A payment that bought a period moved somebody's dates. A part payment is real
            // money that did not, and counting it as a renewal would overstate how many
            // memberships the month actually sold.
            RenewalCount = movements.Count(p => !p.IsReversal && p.PeriodStartDate != null),

            Payments = movements.Select(p => new TakingsPaymentDto
            {
                Id = p.Id,
                TakenAt = p.PaymentDate,
                ClientName = p.Client?.FullName ?? "(removed member)",
                PackageName = p.Package?.Name ?? "(deleted package)",
                PaymentMethod = p.PaymentMethod.ToString(),
                Currency = p.Currency.ToString(),
                AmountReceived = p.AmountReceived,
                AmountUsd = p.Amount,
                ExchangeRate = p.ExchangeRate,
                IsReversal = p.IsReversal
            }).ToList()
        };
    }

    /// <summary>The earlier of two dates. DateOnly has no Min of its own.</summary>
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    /// <summary>"Mar 2026". One place, so every screen names a month identically.</summary>
    private static string MonthLabel(DateOnly month) =>
        month.ToDateTime(TimeOnly.MinValue).ToString("MMM yyyy",
            System.Globalization.CultureInfo.InvariantCulture);
}
