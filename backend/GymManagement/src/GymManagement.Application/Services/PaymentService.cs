using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Payment;
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
    Task<bool> RefundPaymentAsync(int id, int? userId = null, CancellationToken cancellationToken = default);
}

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
                PaymentDate = p.PaymentDate,
                PaymentMethod = p.PaymentMethod.ToString(),
                Status = p.Status.ToString()
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

        if (payment == null) return null;

        return MapToDto(payment);
    }

    public async Task<List<PaymentDto>> GetClientPaymentsAsync(int clientId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Payments.Query()
            .Include(p => p.Client)
            .Include(p => p.Package)
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                ClientId = p.ClientId,
                ClientName = p.Client.FirstName + " " + p.Client.LastName,
                PackageId = p.PackageId,
                PackageName = p.Package.Name,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                PaymentMethod = p.PaymentMethod.ToString(),
                Status = p.Status.ToString(),
                PeriodStartDate = p.PeriodStartDate,
                PeriodEndDate = p.PeriodEndDate,
                TransactionReference = p.TransactionReference,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, int? userId = null, CancellationToken cancellationToken = default)
    {
        var payment = new Payment
        {
            ClientId = request.ClientId,
            PackageId = request.PackageId,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate,
            PaymentMethod = request.PaymentMethod,
            Status = TransactionStatus.Completed,
            PeriodStartDate = request.PeriodStartDate,
            PeriodEndDate = request.PeriodEndDate,
            TransactionReference = request.TransactionReference,
            Notes = request.Notes,
            CreatedBy = userId
        };

        await _unitOfWork.Payments.AddAsync(payment, cancellationToken);

        // Update client membership
        var client = await _unitOfWork.Clients.GetByIdAsync(request.ClientId, cancellationToken);
        if (client != null)
        {
            client.CurrentPackageId = request.PackageId;
            client.MembershipStartDate = request.PeriodStartDate;
            client.MembershipEndDate = request.PeriodEndDate;
            client.PaymentStatus = PaymentStatus.Paid;
            client.UpdateMembershipStatus();
        }

        // Create payment history
        var history = new PaymentHistory
        {
            PaymentId = payment.Id,
            ClientId = request.ClientId,
            Action = PaymentHistoryAction.Created,
            NewAmount = payment.Amount,
            NewStatus = payment.Status.ToString(),
            ChangeDescription = "Payment created",
            ChangedBy = userId,
            ChangedAt = DateTime.UtcNow
        };

        await _unitOfWork.PaymentHistories.AddAsync(history, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Update history with payment ID
        history.PaymentId = payment.Id;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with navigation properties
        payment = await _unitOfWork.Payments.Query()
            .Include(p => p.Client)
            .Include(p => p.Package)
            .FirstOrDefaultAsync(p => p.Id == payment.Id, cancellationToken);

        return MapToDto(payment!);
    }

    public async Task<bool> RefundPaymentAsync(int id, int? userId = null, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(id, cancellationToken);
        if (payment == null) return false;

        var oldStatus = payment.Status.ToString();
        payment.Status = TransactionStatus.Refunded;

        var history = new PaymentHistory
        {
            PaymentId = payment.Id,
            ClientId = payment.ClientId,
            Action = PaymentHistoryAction.Refunded,
            OldAmount = payment.Amount,
            NewAmount = payment.Amount,
            OldStatus = oldStatus,
            NewStatus = payment.Status.ToString(),
            ChangeDescription = "Payment refunded",
            ChangedBy = userId,
            ChangedAt = DateTime.UtcNow
        };

        await _unitOfWork.PaymentHistories.AddAsync(history, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static PaymentDto MapToDto(Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            ClientId = payment.ClientId,
            ClientName = payment.Client.FirstName + " " + payment.Client.LastName,
            PackageId = payment.PackageId,
            PackageName = payment.Package.Name,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod.ToString(),
            Status = payment.Status.ToString(),
            PeriodStartDate = payment.PeriodStartDate,
            PeriodEndDate = payment.PeriodEndDate,
            TransactionReference = payment.TransactionReference,
            Notes = payment.Notes,
            CreatedAt = payment.CreatedAt
        };
    }
}
