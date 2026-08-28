using GymManagement.Domain.Enums;

namespace GymManagement.Application.DTOs.Reports;

/// <summary>One line of the trail, as the owner reads it.</summary>
public class AuditEntryDto
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Details { get; set; }

    /// <summary>Null when the system did it rather than a person.</summary>
    public string? ActorName { get; set; }

    public DateTime OccurredAt { get; set; }
}

public class AuditQueryParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 25;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? MaxPageSize : value;
    }

    /// <summary>Client, Payment, Package, ExchangeRate.</summary>
    public string? EntityType { get; set; }

    public int? EntityId { get; set; }
    public AuditAction? Action { get; set; }

    /// <summary>Gym calendar dates, inclusive at both ends.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Matches the summary line or who did it.</summary>
    public string? Search { get; set; }
}
