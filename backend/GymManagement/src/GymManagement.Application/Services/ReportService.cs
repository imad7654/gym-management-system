using GymManagement.Application.DTOs.Reports;
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
}
