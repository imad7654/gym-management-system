using GymManagement.Domain.Enums;

namespace GymManagement.Application.DTOs.Client;

public class ClientQueryParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    public string? Search { get; set; }
    public MembershipStatus? MembershipStatus { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public bool IncludeInactive { get; set; } = false;
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}
