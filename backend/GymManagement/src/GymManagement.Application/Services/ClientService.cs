using GymManagement.Domain.Common;
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
    Task<List<ClientListDto>> GetExpiringClientsAsync(int days = Client.ExpiringWindowDays, CancellationToken cancellationToken = default);

    /// <summary>Everything the member page shows, in one call.</summary>
    Task<MemberSummaryDto?> GetMemberSummaryAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Money put toward packages this member has not finished paying for.</summary>
    Task<List<OutstandingPackageDto>> GetOutstandingAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>Freezes or unfreezes a membership. Returns false if the member does not exist.</summary>
    Task<bool> SetSuspendedAsync(int id, bool suspended, string? reason = null, int? userId = null, CancellationToken cancellationToken = default);
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

            // Phone numbers are written four ways in the gym's own records - "03 123 456",
            // "03123456", "+961 3 123 456". Comparing the raw text meant typing a number
            // with spaces found nobody. PhoneNumberKey is the rule for this everywhere else,
            // but it is C# and cannot run in the database, so the separators the gym
            // actually uses are stripped from both sides in SQL instead.
            //
            // This is a narrower rule than PhoneNumberKey - it does not strip the 961
            // country code - so a search is a search, not the duplicate-detection match.
            var digits = new string(parameters.Search.Where(char.IsDigit).ToArray());
            var searchByPhone = digits.Length >= 3;

            query = query.Where(c =>
                c.FirstName.ToLower().Contains(searchLower) ||
                c.LastName.ToLower().Contains(searchLower) ||
                (c.FirstName + " " + c.LastName).ToLower().Contains(searchLower) ||
                (c.Email != null && c.Email.ToLower().Contains(searchLower)) ||
                (searchByPhone && c.PhoneNumber
                    .Replace(" ", "").Replace("-", "").Replace("(", "")
                    .Replace(")", "").Replace("+", "").Replace(".", "")
                    .Contains(digits)));
        }

        var today = _clock.Today;

        // Filter by status. There is no status column any more, so this asks the dates the
        // same question Client.StatusFrom asks in memory.
        if (parameters.MembershipStatus.HasValue)
        {
            query = query.WithStatus(parameters.MembershipStatus.Value, today);
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
                ? query.OrderByDescending(ClientQueries.StatusRank(today))
                : query.OrderBy(ClientQueries.StatusRank(today)),
            _ => parameters.SortDescending
                ? query.OrderByDescending(c => c.CreatedAt)
                : query.OrderBy(c => c.CreatedAt)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        // The status is worked out after the rows come back rather than in the projection.
        // Client.StatusFrom is ordinary C# and cannot be translated to SQL, and rewriting
        // it as a CASE here would be a second copy of the rule to keep in step.
        var clients = (await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(c => new ClientListRow(
                c.Id,
                c.FirstName + " " + c.LastName,
                c.PhoneNumber,
                c.Email,
                c.CurrentPackage != null ? c.CurrentPackage.Name : null,
                c.MembershipStartDate,
                c.MembershipEndDate,
                c.IsSuspended,
                c.PaymentStatus,
                c.IsActive))
            .ToListAsync(cancellationToken))
            .Select(row => row.ToDto(today))
            .ToList();

        return new PaginatedResult<ClientListDto>(clients, totalCount, parameters.Page, parameters.PageSize);
    }

    public async Task<ClientDto?> GetClientByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.QueryIncludingDeleted()
            .Include(c => c.CurrentPackage)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (client == null) return null;

        return MapToDto(client, _clock.Today);
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

        return MapToDto(client!, _clock.Today);
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

        return MapToDto(client!, _clock.Today);
    }

    private static string ShowDate(DateTime? date) =>
        date?.ToString("yyyy-MM-dd") ?? "none";

    public async Task<MemberSummaryDto?> GetMemberSummaryAsync(
        int id, CancellationToken cancellationToken = default)
    {
        // Includes removed members on purpose. Opening a deleted member is how you restore
        // them, so refusing to load the page would leave the undelete unreachable again.
        var client = await _unitOfWork.Clients.QueryIncludingDeleted()
            .Include(c => c.CurrentPackage)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (client == null) return null;

        var today = _clock.Today;

        var payments = await _unitOfWork.Payments.Query()
            .Include(p => p.Package)
            .Where(p => p.ClientId == id)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        var outstanding = await GetOutstandingAsync(id, cancellationToken);

        return new MemberSummaryDto
        {
            Id = client.Id,
            FullName = client.FullName,
            PhoneNumber = client.PhoneNumber,
            PhoneDigits = PhoneNumberKey.Normalize(client.PhoneNumber),
            Email = client.Email,

            MembershipStatus = client.MembershipStatusOn(today).ToString(),
            IsSuspended = client.IsSuspended,
            DaysRemaining = client.DaysRemaining(today),
            MembershipStartDate = client.MembershipStartDate,
            MembershipEndDate = client.MembershipEndDate,
            CurrentPackageId = client.CurrentPackageId,
            CurrentPackageName = client.CurrentPackage?.Name,

            DateOfBirth = client.DateOfBirth,
            Gender = client.Gender?.ToString(),
            Address = client.Address,
            EmergencyContact = client.EmergencyContact,
            EmergencyPhone = client.EmergencyPhone,
            Notes = client.Notes,

            IsActive = client.IsActive,
            CreatedAt = client.CreatedAt,

            Outstanding = outstanding,
            TotalOwed = outstanding.Sum(row => row.AmountOwed),

            Payments = payments.Select(p => new MemberPaymentDto
            {
                Id = p.Id,
                PaidAt = p.PaymentDate,
                PackageName = p.Package?.Name,
                AmountUsd = p.Amount,
                AmountReceived = p.AmountReceived,
                Currency = p.Currency.ToString(),
                ExchangeRate = p.ExchangeRate,
                PaymentMethod = p.PaymentMethod.ToString(),
                IsReversal = p.IsReversal,
                PeriodStartDate = p.PeriodStartDate,
                PeriodEndDate = p.PeriodEndDate,
                Notes = p.Notes
            }).ToList()
        };
    }

    /// <summary>
    /// What this member has put toward packages they have not finished paying for.
    ///
    /// Shares <c>PaymentQueries.OutstandingCredit()</c> with the payment desk and the
    /// who-owes-money report, so the member page cannot quote reception a different figure
    /// from the one the report chases or the one the next payment is credited against.
    /// </summary>
    public async Task<List<OutstandingPackageDto>> GetOutstandingAsync(
        int clientId, CancellationToken cancellationToken = default)
    {
        var credit = await _unitOfWork.Payments.Query()
            .Where(p => p.ClientId == clientId)
            .OutstandingCredit()
            .Include(p => p.Package)
            .ToListAsync(cancellationToken);

        return credit
            .Where(p => p.Package != null)
            .GroupBy(p => p.PackageId)
            .Select(group =>
            {
                var first = group.OrderBy(p => p.PaymentDate).First();
                var paid = group.Sum(p => p.Amount);

                return new OutstandingPackageDto
                {
                    PackageId = group.Key,
                    PackageName = first.Package.Name,
                    PackagePrice = first.Package.Price,
                    AmountPaid = paid,
                    AmountOwed = first.Package.Price - paid,
                    OwingSince = first.PaymentDate
                };
            })
            // A group can net to zero or below when a part payment was reversed. The money
            // is square, so it is not a debt and does not belong on the page.
            .Where(row => row.AmountOwed > 0 && row.AmountPaid > 0)
            .OrderBy(row => row.OwingSince)
            .ToList();
    }

    public async Task<bool> SetSuspendedAsync(
        int id, bool suspended, string? reason = null, int? userId = null,
        CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.Query()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (client == null) return false;

        if (client.IsSuspended == suspended) return true;

        if (suspended) client.Suspend(); else client.Resume();

        await _audit.RecordAsync(
            "Client", client.Id,
            suspended ? AuditAction.Updated : AuditAction.Updated,
            suspended
                ? $"Froze {client.FullName}'s membership"
                : $"Unfroze {client.FullName}'s membership",
            suspended
                // The dates are untouched, so say so - a frozen member is not losing days,
                // and the question at the desk is always whether they are.
                ? $"Membership dates unchanged; end date still {ShowDate(client.MembershipEndDate)}."
                  + (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}")
                : $"Now reads as {client.MembershipStatusOn(_clock.Today)}.",
            userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

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

        client.Restore();

        await _audit.RecordAsync(
            "Client", client.Id, AuditAction.Restored,
            $"Restored member {client.FullName}",
            $"Membership reads as {client.MembershipStatusOn(_clock.Today)}.",
            userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<ClientListDto>> GetExpiringClientsAsync(
        int days = Client.ExpiringWindowDays, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        return (await _unitOfWork.Clients.Query()
            .Include(c => c.CurrentPackage)
            .ExpiringWithin(days, today)
            .OrderBy(c => c.MembershipEndDate)
            .Select(c => new ClientListRow(
                c.Id,
                c.FirstName + " " + c.LastName,
                c.PhoneNumber,
                c.Email,
                c.CurrentPackage != null ? c.CurrentPackage.Name : null,
                c.MembershipStartDate,
                c.MembershipEndDate,
                c.IsSuspended,
                c.PaymentStatus,
                c.IsActive))
            .ToListAsync(cancellationToken))
            .Select(row => row.ToDto(today))
            .ToList();
    }

    private static ClientDto MapToDto(Client client, DateOnly today)
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
            MembershipStatus = client.MembershipStatusOn(today).ToString(),
            PaymentStatus = client.PaymentStatus.ToString(),
            IsActive = client.IsActive,
            CreatedAt = client.CreatedAt,
            UpdatedAt = client.UpdatedAt
        };
    }
}

/// <summary>
/// The raw columns a member list row needs, straight from the database.
///
/// The status is deliberately absent: it is not stored, and the rule that derives it is
/// ordinary C# that SQL cannot run. So the query fetches the three fields the rule reads -
/// the two dates and the freeze flag - and <see cref="ToDto"/> applies the rule once the
/// rows are back in memory. That keeps exactly one definition of the status.
/// </summary>
internal readonly record struct ClientListRow(
    int Id,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? CurrentPackageName,
    DateTime? MembershipStartDate,
    DateTime? MembershipEndDate,
    bool IsSuspended,
    PaymentStatus PaymentStatus,
    bool IsActive)
{
    public ClientListDto ToDto(DateOnly today) => new()
    {
        Id = Id,
        FullName = FullName,
        PhoneNumber = PhoneNumber,
        Email = Email,
        CurrentPackageName = CurrentPackageName,
        MembershipEndDate = MembershipEndDate,
        MembershipStatus = Client
            .StatusFrom(IsSuspended, MembershipStartDate, MembershipEndDate, today)
            .ToString(),
        PaymentStatus = PaymentStatus.ToString(),
        IsActive = IsActive
    };
}
