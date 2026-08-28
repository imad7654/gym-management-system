using GymManagement.Application.DTOs.Client;
using GymManagement.Application.DTOs.Common;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IClientService
{
    Task<PaginatedResult<ClientListDto>> GetClientsAsync(ClientQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<ClientDto?> GetClientByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ClientDto> CreateClientAsync(CreateClientRequest request, int? userId = null, CancellationToken cancellationToken = default);
    Task<ClientDto?> UpdateClientAsync(int id, UpdateClientRequest request, int? userId = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteClientAsync(int id, int? userId = null, CancellationToken cancellationToken = default);
    Task<bool> RestoreClientAsync(int id, int? userId = null, CancellationToken cancellationToken = default);
    Task<List<ClientListDto>> GetExpiringClientsAsync(int days = 7, CancellationToken cancellationToken = default);
}

public class ClientService : IClientService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipClock _clock;
    private readonly IAuditService _audit;

    public ClientService(IUnitOfWork unitOfWork, IMembershipClock clock, IAuditService audit)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
    }

    public async Task<PaginatedResult<ClientListDto>> GetClientsAsync(ClientQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = parameters.IncludeInactive
            ? _unitOfWork.Clients.QueryIncludingDeleted()
            : _unitOfWork.Clients.Query();

        query = query.Include(c => c.CurrentPackage);

        // Search
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var searchLower = parameters.Search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(searchLower) ||
                c.LastName.ToLower().Contains(searchLower) ||
                c.PhoneNumber.Contains(searchLower) ||
                (c.Email != null && c.Email.ToLower().Contains(searchLower)));
        }

        // Filter by status
        if (parameters.MembershipStatus.HasValue)
        {
            query = query.Where(c => c.MembershipStatus == parameters.MembershipStatus.Value);
        }

        if (parameters.PaymentStatus.HasValue)
        {
            query = query.Where(c => c.PaymentStatus == parameters.PaymentStatus.Value);
        }

        // Sorting
        query = parameters.SortBy?.ToLower() switch
        {
            "name" => parameters.SortDescending
                ? query.OrderByDescending(c => c.FirstName).ThenByDescending(c => c.LastName)
                : query.OrderBy(c => c.FirstName).ThenBy(c => c.LastName),
            "membershipenddate" => parameters.SortDescending
                ? query.OrderByDescending(c => c.MembershipEndDate)
                : query.OrderBy(c => c.MembershipEndDate),
            "membershipstatus" => parameters.SortDescending
                ? query.OrderByDescending(c => c.MembershipStatus)
                : query.OrderBy(c => c.MembershipStatus),
            _ => parameters.SortDescending
                ? query.OrderByDescending(c => c.CreatedAt)
                : query.OrderBy(c => c.CreatedAt)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var clients = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(c => new ClientListDto
            {
                Id = c.Id,
                FullName = c.FirstName + " " + c.LastName,
                PhoneNumber = c.PhoneNumber,
                Email = c.Email,
                CurrentPackageName = c.CurrentPackage != null ? c.CurrentPackage.Name : null,
                MembershipEndDate = c.MembershipEndDate,
                MembershipStatus = c.MembershipStatus.ToString(),
                PaymentStatus = c.PaymentStatus.ToString(),
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);

        return new PaginatedResult<ClientListDto>(clients, totalCount, parameters.Page, parameters.PageSize);
    }

    public async Task<ClientDto?> GetClientByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.QueryIncludingDeleted()
            .Include(c => c.CurrentPackage)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (client == null) return null;

        return MapToDto(client);
    }

    public async Task<ClientDto> CreateClientAsync(CreateClientRequest request, int? userId = null, CancellationToken cancellationToken = default)
    {
        var client = new Client
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = request.Address,
            EmergencyContact = request.EmergencyContact,
            EmergencyPhone = request.EmergencyPhone,
            Notes = request.Notes,
            CurrentPackageId = request.PackageId,
            CreatedBy = userId
        };

        if (request.PackageId.HasValue && request.MembershipStartDate.HasValue)
        {
            var package = await _unitOfWork.Packages.GetByIdAsync(request.PackageId.Value, cancellationToken);
            if (package != null)
            {
                client.MembershipStartDate = request.MembershipStartDate.Value;
                // Both dates are inclusive, so the last day is start + duration - 1, the
                // same rule Client.ExtendMembership applies to payments. Using
                // start + duration here gave a member registered with a 30-day package
                // 31 days, and disagreed with every renewal they went on to pay for.
                client.MembershipEndDate = request.MembershipStartDate.Value.AddDays(package.DurationDays - 1);
                client.UpdateMembershipStatus(_clock.Today);
            }
        }

        await _unitOfWork.Clients.AddAsync(client, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Written after the save rather than with it, because the row has no id until then
        // and a trail entry that cannot say which member it refers to is not worth keeping.
        // The trailing save is the one place the trail is not in the same transaction as its
        // change; a create that failed to be logged still leaves a visible new member, which
        // is the mild version of this going wrong.
        await _audit.RecordAsync(
            "Client", client.Id, AuditAction.Created,
            $"Added member {client.FullName}",
            client.MembershipEndDate.HasValue
                ? $"Membership runs to {client.MembershipEndDate:yyyy-MM-dd}."
                : "No membership dates set yet.",
            userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with package
        client = await _unitOfWork.Clients.Query()
            .Include(c => c.CurrentPackage)
            .FirstOrDefaultAsync(c => c.Id == client.Id, cancellationToken);

        return MapToDto(client!);
    }

    public async Task<ClientDto?> UpdateClientAsync(int id, UpdateClientRequest request, int? userId = null, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.Query()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (client == null) return null;

        // Captured before anything is overwritten. The membership fields are the ones worth
        // spelling out: a changed end date is a member let in or turned away, and "who moved
        // this date" is the question the trail exists to answer.
        var beforeEnd = client.MembershipEndDate;
        var beforeStart = client.MembershipStartDate;
        var beforePackageId = client.CurrentPackageId;
        var beforePaymentStatus = client.PaymentStatus;

        client.FirstName = request.FirstName;
        client.LastName = request.LastName;
        client.Email = request.Email;
        client.PhoneNumber = request.PhoneNumber;
        client.DateOfBirth = request.DateOfBirth;
        client.Gender = request.Gender;
        client.Address = request.Address;
        client.EmergencyContact = request.EmergencyContact;
        client.EmergencyPhone = request.EmergencyPhone;
        client.Notes = request.Notes;
        client.CurrentPackageId = request.PackageId;
        client.MembershipStartDate = request.MembershipStartDate;
        client.MembershipEndDate = request.MembershipEndDate;
        client.UpdatedBy = userId;

        if (request.PaymentStatus.HasValue)
        {
            client.PaymentStatus = request.PaymentStatus.Value;
        }

        client.UpdateMembershipStatus(_clock.Today);

        var changes = new List<string>();
        if (beforeStart != client.MembershipStartDate)
            changes.Add($"Start date {ShowDate(beforeStart)} to {ShowDate(client.MembershipStartDate)}");
        if (beforeEnd != client.MembershipEndDate)
            changes.Add($"End date {ShowDate(beforeEnd)} to {ShowDate(client.MembershipEndDate)}");
        if (beforePackageId != client.CurrentPackageId)
            changes.Add($"Package #{beforePackageId?.ToString() ?? "none"} to #{client.CurrentPackageId?.ToString() ?? "none"}");
        if (beforePaymentStatus != client.PaymentStatus)
            changes.Add($"Payment status {beforePaymentStatus} to {client.PaymentStatus}");

        await _audit.RecordAsync(
            "Client", client.Id, AuditAction.Updated,
            changes.Count > 0
                ? $"Changed {client.FullName}'s membership"
                : $"Edited {client.FullName}'s details",
            changes.Count > 0 ? string.Join(". ", changes) + "." : null,
            userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with package
        client = await _unitOfWork.Clients.Query()
            .Include(c => c.CurrentPackage)
            .FirstOrDefaultAsync(c => c.Id == client.Id, cancellationToken);

        return MapToDto(client!);
    }

    private static string ShowDate(DateTime? date) =>
        date?.ToString("yyyy-MM-dd") ?? "none";

    public async Task<bool> DeleteClientAsync(int id, int? userId = null, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.GetByIdAsync(id, cancellationToken);
        if (client == null) return false;

        client.SoftDelete();

        await _audit.RecordAsync(
            "Client", client.Id, AuditAction.Deleted,
            $"Removed member {client.FullName}",
            "Soft delete - the record and their payment history are kept and can be restored.",
            userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreClientAsync(int id, int? userId = null, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.QueryIncludingDeleted()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (client == null) return false;

        client.Restore(_clock.Today);

        await _audit.RecordAsync(
            "Client", client.Id, AuditAction.Restored,
            $"Restored member {client.FullName}",
            $"Membership status recalculated to {client.MembershipStatus}.",
            userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<ClientListDto>> GetExpiringClientsAsync(int days = 7, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today.ToDateTime(TimeOnly.MinValue);
        var endDate = today.AddDays(days);

        return await _unitOfWork.Clients.Query()
            .Include(c => c.CurrentPackage)
            .Where(c => c.MembershipEndDate >= today && c.MembershipEndDate <= endDate
                && MembershipStatuses.AllowedIn.Contains(c.MembershipStatus))
            .OrderBy(c => c.MembershipEndDate)
            .Select(c => new ClientListDto
            {
                Id = c.Id,
                FullName = c.FirstName + " " + c.LastName,
                PhoneNumber = c.PhoneNumber,
                Email = c.Email,
                CurrentPackageName = c.CurrentPackage != null ? c.CurrentPackage.Name : null,
                MembershipEndDate = c.MembershipEndDate,
                MembershipStatus = c.MembershipStatus.ToString(),
                PaymentStatus = c.PaymentStatus.ToString(),
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    private static ClientDto MapToDto(Client client)
    {
        return new ClientDto
        {
            Id = client.Id,
            FirstName = client.FirstName,
            LastName = client.LastName,
            FullName = client.FullName,
            Email = client.Email,
            PhoneNumber = client.PhoneNumber,
            DateOfBirth = client.DateOfBirth,
            Gender = client.Gender?.ToString(),
            Address = client.Address,
            EmergencyContact = client.EmergencyContact,
            EmergencyPhone = client.EmergencyPhone,
            ProfileImageUrl = client.ProfileImageUrl,
            Notes = client.Notes,
            CurrentPackageId = client.CurrentPackageId,
            CurrentPackageName = client.CurrentPackage?.Name,
            MembershipStartDate = client.MembershipStartDate,
            MembershipEndDate = client.MembershipEndDate,
            MembershipStatus = client.MembershipStatus.ToString(),
            PaymentStatus = client.PaymentStatus.ToString(),
            IsActive = client.IsActive,
            CreatedAt = client.CreatedAt,
            UpdatedAt = client.UpdatedAt
        };
    }
}
