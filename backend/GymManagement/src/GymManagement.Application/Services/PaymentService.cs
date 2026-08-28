using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IPaymentService
{
    Task<PaginatedResult<PaymentListDto>> GetPaymentsAsync(PaymentQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<PaymentDto?> GetPaymentByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<PaymentDto>> GetClientPaymentsAsync(int clientId, CancellationToken cancellationToken = default);
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, int? userId = null, CancellationToken cancellationToken = default);
    Task<PaymentDto> ReversePaymentAsync(int id, string? reason = null, int? userId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the desk payment flow (blueprint 6.5), the most important algorithm in the
/// system: it is what moves the membership dates and what every money report is built from.
///
/// Two rules shape everything here. The server works out the price and the period from the
/// package - the browser is never trusted with either. And money rows are append-only: a
/// wrong payment is corrected by a reversal row pointing back at the original, never by
/// editing it.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipClock _clock;

    public PaymentService(IUnitOfWork unitOfWork, IMembershipClock clock)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<PaginatedResult<PaymentListDto>> GetPaymentsAsync(PaymentQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Payments.Query()
            .Include(p => p.Client)
            .Include(p => p.Package)
            .AsQueryable();

        if (parameters.ClientId.HasValue)
        {
            query = query.Where(p => p.ClientId == parameters.ClientId.Value);
        }

        if (parameters.StartDate.HasValue)
        {
            query = query.Where(p => p.PaymentDate >= parameters.StartDate.Value);
        }

        if (parameters.EndDate.HasValue)
        {
            query = query.Where(p => p.PaymentDate <= parameters.EndDate.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(p => p.Status == parameters.Status.Value);
        }

        if (parameters.PaymentMethod.HasValue)
        {
            query = query.Where(p => p.PaymentMethod == parameters.PaymentMethod.Value);
        }

        // Sorting
        query = parameters.SortBy?.ToLower() switch
        {
            "amount" => parameters.SortDescending
                ? query.OrderByDescending(p => p.Amount)
                : query.OrderBy(p => p.Amount),
            "clientname" => parameters.SortDescending
                ? query.OrderByDescending(p => p.Client.FirstName)
                : query.OrderBy(p => p.Client.FirstName),
            _ => parameters.SortDescending
                ? query.OrderByDescending(p => p.PaymentDate)
                : query.OrderBy(p => p.PaymentDate)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var payments = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(p => new PaymentListDto
            {
                Id = p.Id,
                ClientName = p.Client.FirstName + " " + p.Client.LastName,
                PackageName = p.Package.Name,
                Amount = p.Amount,
                AmountReceived = p.AmountReceived,
                Currency = p.Currency.ToString(),
                PaymentDate = p.PaymentDate,
                PaymentMethod = p.PaymentMethod.ToString(),
                Status = p.Status.ToString(),
                IsReversal = p.ReversesPaymentId != null
            })
            .ToListAsync(cancellationToken);

        return new PaginatedResult<PaymentListDto>(payments, totalCount, parameters.Page, parameters.PageSize);
    }

    public async Task<PaymentDto?> GetPaymentByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.Query()
            .Include(p => p.Client)
            .Include(p => p.Package)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return payment == null ? null : MapToDto(payment);
    }

    public async Task<List<PaymentDto>> GetClientPaymentsAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var payments = await _unitOfWork.Payments.Query()
            .Include(p => p.Client)
            .Include(p => p.Package)
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        return payments.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Takes a payment at the desk. See blueprint 6.5 for the rules this implements.
    /// </summary>
    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, int? userId = null, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.Query()
            .FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken)
            ?? throw new NotFoundException("Client", request.ClientId);

        if (!client.IsActive)
        {
            throw new BusinessException("This member has been removed. Restore them before taking a payment.");
        }

        var package = await _unitOfWork.Packages.Query()
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, cancellationToken)
            ?? throw new NotFoundException("Package", request.PackageId);

        var amountUsd = ConvertToUsd(request);
        var today = _clock.Today;

        var payment = new Payment
        {
            ClientId = client.Id,
            PackageId = package.Id,
            // The price in USD, worked out here rather than accepted from the browser.
            Amount = amountUsd,
            AmountReceived = request.AmountReceived,
            Currency = request.Currency,
            ExchangeRate = request.Currency == Currency.Lbp ? request.ExchangeRate : null,
            PaymentDate = _clock.UtcNow,
            PaymentMethod = request.PaymentMethod,
            Status = TransactionStatus.Completed,
            TransactionReference = request.TransactionReference,
            Notes = request.Notes,
            CreatedBy = userId
        };

        string description;

        if (amountUsd < package.Price)
        {
            // Recorded, but the membership does not move. A half payment that silently
            // unlocks a full month is how a gym loses track of its income.
            client.PaymentStatus = PaymentStatus.Partial;

            var shortfall = package.Price - amountUsd;
            description =
                $"Partial payment of {amountUsd:0.00} USD against {package.Name} " +
                $"({package.Price:0.00} USD). Membership not extended; {shortfall:0.00} USD outstanding.";
        }
        else
        {
            var period = client.ExtendMembership(package, today);
            payment.PeriodStartDate = period.Start.ToDateTime(TimeOnly.MinValue);
            payment.PeriodEndDate = period.End.ToDateTime(TimeOnly.MinValue);

            description =
                $"Payment of {amountUsd:0.00} USD for {package.Name}. " +
                $"Membership runs {period.Start:yyyy-MM-dd} to {period.End:yyyy-MM-dd}.";
        }

        await _unitOfWork.Payments.AddAsync(payment, cancellationToken);

        // Set the Payment navigation property rather than PaymentId, so EF Core's
        // relationship fixup assigns the real foreign key once the new Payment row gets its
        // identity value during this same SaveChanges call.
        await _unitOfWork.PaymentHistories.AddAsync(new PaymentHistory
        {
            Payment = payment,
            ClientId = client.Id,
            Action = PaymentHistoryAction.Created,
            NewAmount = payment.Amount,
            NewStatus = payment.Status.ToString(),
            ChangeDescription = description,
            ChangedBy = userId,
            ChangedAt = _clock.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReloadDtoAsync(payment.Id, cancellationToken);
    }

    /// <summary>
    /// Reverses a payment by writing a second row that cancels it, and takes back the days
    /// it bought. The original row is left exactly as it was.
    ///
    /// The previous behaviour edited the original - setting its status to Refunded and
    /// leaving the membership untouched - which both broke the append-only rule and let a
    /// refunded member keep the time they had been refunded for.
    /// </summary>
    public async Task<PaymentDto> ReversePaymentAsync(int id, string? reason = null, int? userId = null, CancellationToken cancellationToken = default)
    {
        var original = await _unitOfWork.Payments.Query()
            .Include(p => p.Package)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Payment", id);

        if (original.IsReversal)
        {
            throw new BusinessException("This row is itself a reversal and cannot be reversed.");
        }

        var alreadyReversed = await _unitOfWork.Payments.Query()
            .AnyAsync(p => p.ReversesPaymentId == original.Id, cancellationToken);

        if (alreadyReversed)
        {
            throw new BusinessException("This payment has already been reversed.");
        }

        var client = await _unitOfWork.Clients.Query()
            .FirstOrDefaultAsync(c => c.Id == original.ClientId, cancellationToken)
            ?? throw new NotFoundException("Client", original.ClientId);

        var reversal = new Payment
        {
            ClientId = original.ClientId,
            PackageId = original.PackageId,
            // Negative, so summing the Amount column gives the true net take without the
            // reports having to know which rows are reversals.
            Amount = -original.Amount,
            AmountReceived = -original.AmountReceived,
            Currency = original.Currency,
            ExchangeRate = original.ExchangeRate,
            PaymentDate = _clock.UtcNow,
            PaymentMethod = original.PaymentMethod,
            // Completed, not Refunded: the money really did move, just the other way. Every
            // revenue query filters on Completed, so marking reversals as anything else
            // would leave them out of the sums and a reversed payment would still show up
            // as income. A reversal is recognised by ReversesPaymentId, not by its status.
            Status = TransactionStatus.Completed,
            ReversesPaymentId = original.Id,
            Notes = reason,
            CreatedBy = userId
        };

        // Only a payment that actually extended the membership has days to take back; a
        // partial payment never moved the dates.
        var extendedMembership = original.PeriodStartDate.HasValue && original.PeriodEndDate.HasValue;
        if (extendedMembership)
        {
            client.WindBackMembership(original.Package.DurationDays, _clock.Today);
        }

        await _unitOfWork.Payments.AddAsync(reversal, cancellationToken);

        await _unitOfWork.PaymentHistories.AddAsync(new PaymentHistory
        {
            Payment = reversal,
            ClientId = original.ClientId,
            Action = PaymentHistoryAction.Reversed,
            OldAmount = original.Amount,
            NewAmount = reversal.Amount,
            OldStatus = original.Status.ToString(),
            NewStatus = reversal.Status.ToString(),
            ChangeDescription = extendedMembership
                ? $"Reversal of payment #{original.Id}. {original.Package.DurationDays} days removed from the membership."
                    + (reason == null ? "" : $" Reason: {reason}")
                : $"Reversal of partial payment #{original.Id}, which had not extended the membership."
                    + (reason == null ? "" : $" Reason: {reason}"),
            ChangedBy = userId,
            ChangedAt = _clock.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReloadDtoAsync(reversal.Id, cancellationToken);
    }

    /// <summary>
    /// Works out the USD figure, which is the only one reports ever add up. The rate is
    /// captured on the payment and never recalculated afterwards.
    /// </summary>
    private static decimal ConvertToUsd(CreatePaymentRequest request)
    {
        if (request.Currency == Currency.Usd)
        {
            return decimal.Round(request.AmountReceived, 2, MidpointRounding.AwayFromZero);
        }

        if (!request.ExchangeRate.HasValue || request.ExchangeRate.Value <= 0)
        {
            throw new BusinessException("An exchange rate is required when the payment is in LBP.");
        }

        return decimal.Round(request.AmountReceived / request.ExchangeRate.Value, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<PaymentDto> ReloadDtoAsync(int paymentId, CancellationToken cancellationToken)
    {
        var saved = await _unitOfWork.Payments.Query()
            .Include(p => p.Client)
            .Include(p => p.Package)
            .FirstAsync(p => p.Id == paymentId, cancellationToken);

        return MapToDto(saved);
    }

    private static PaymentDto MapToDto(Payment payment)
    {
        // A reversal is not "short of the price"; only a forward payment can be partial.
        var isPartial = !payment.IsReversal && payment.Amount < payment.Package.Price;

        return new PaymentDto
        {
            Id = payment.Id,
            ClientId = payment.ClientId,
            ClientName = payment.Client.FirstName + " " + payment.Client.LastName,
            PackageId = payment.PackageId,
            PackageName = payment.Package.Name,
            Amount = payment.Amount,
            AmountReceived = payment.AmountReceived,
            Currency = payment.Currency.ToString(),
            ExchangeRate = payment.ExchangeRate,
            PaymentDate = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod.ToString(),
            Status = payment.Status.ToString(),
            PeriodStartDate = payment.PeriodStartDate,
            PeriodEndDate = payment.PeriodEndDate,
            ReversesPaymentId = payment.ReversesPaymentId,
            IsPartial = isPartial,
            AmountOutstanding = isPartial ? payment.Package.Price - payment.Amount : 0m,
            TransactionReference = payment.TransactionReference,
            Notes = payment.Notes,
            CreatedAt = payment.CreatedAt
        };
    }
}
