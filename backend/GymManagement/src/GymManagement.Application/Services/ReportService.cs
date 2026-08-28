using GymManagement.Application.DTOs.Reports;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IReportService
{
    /// <summary>Members who have part-paid for a package and still owe the difference.</summary>
    Task<WhoOwesMoneyDto> GetWhoOwesMoneyAsync(CancellationToken cancellationToken = default);
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
                    MembershipStatus = first.Client.MembershipStatus.ToString()
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
}
