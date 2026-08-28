using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Reports;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IAuditService
{
    /// <summary>
    /// Adds one entry to the trail. Does not save - the caller's own SaveChanges commits it
    /// alongside the thing it describes, so the trail and the change either both happen or
    /// neither does.
    /// </summary>
    Task RecordAsync(
        string entityType,
        int? entityId,
        AuditAction action,
        string summary,
        string? details = null,
        int? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<AuditEntryDto>> GetEntriesAsync(
        AuditQueryParameters parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// The record of who did what.
///
/// Entries are queued onto the same unit of work as the change they describe and committed
/// by the same SaveChanges. Writing the trail separately would mean a deletion could
/// succeed while its trail entry failed, and a trail with holes in it is worse than none -
/// it looks complete.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipClock _clock;

    public AuditService(IUnitOfWork unitOfWork, IMembershipClock clock)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task RecordAsync(
        string entityType,
        int? entityId,
        AuditAction action,
        string summary,
        string? details = null,
        int? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Summary = summary,
            Details = details,
            ActorUserId = actorUserId,
            ActorName = await ResolveActorNameAsync(actorUserId, cancellationToken),
            OccurredAt = _clock.UtcNow
        }, cancellationToken);
    }

    public async Task<PaginatedResult<AuditEntryDto>> GetEntriesAsync(
        AuditQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.AuditLogs.Query();

        if (!string.IsNullOrWhiteSpace(parameters.EntityType))
        {
            query = query.Where(a => a.EntityType == parameters.EntityType);
        }

        if (parameters.EntityId.HasValue)
        {
            query = query.Where(a => a.EntityId == parameters.EntityId.Value);
        }

        if (parameters.Action.HasValue)
        {
            query = query.Where(a => a.Action == parameters.Action.Value);
        }

        // Dates arrive as gym calendar days and are turned into UTC instants here, for the
        // same reason the takings report does it: the owner means their day, not the
        // server's.
        if (parameters.From.HasValue)
        {
            var (startUtc, _) = _clock.DayBoundsUtc(parameters.From.Value);
            query = query.Where(a => a.OccurredAt >= startUtc);
        }

        if (parameters.To.HasValue)
        {
            var (_, endUtc) = _clock.DayBoundsUtc(parameters.To.Value);
            query = query.Where(a => a.OccurredAt < endUtc);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.ToLower();
            query = query.Where(a =>
                a.Summary.ToLower().Contains(search)
                || (a.ActorName != null && a.ActorName.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var entries = await query
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(a => new AuditEntryDto
            {
                Id = a.Id,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action.ToString(),
                Summary = a.Summary,
                Details = a.Details,
                ActorName = a.ActorName,
                OccurredAt = a.OccurredAt
            })
            .ToListAsync(cancellationToken);

        return new PaginatedResult<AuditEntryDto>(
            entries, totalCount, parameters.Page, parameters.PageSize);
    }

    /// <summary>
    /// Looks the actor's name up once, at write time, so the entry still reads properly
    /// after that user is gone.
    /// </summary>
    private async Task<string?> ResolveActorNameAsync(int? actorUserId, CancellationToken cancellationToken)
    {
        if (!actorUserId.HasValue) return null;

        var user = await _unitOfWork.Users.QueryIncludingDeleted()
            .Where(u => u.Id == actorUserId.Value)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        return user == null ? null : $"{user.FirstName} {user.LastName}".Trim();
    }
}
