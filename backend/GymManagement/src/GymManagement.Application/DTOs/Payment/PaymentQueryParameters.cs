using GymManagement.Domain.Enums;

namespace GymManagement.Application.DTOs.Payment;

public class PaymentQueryParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    public int? ClientId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public TransactionStatus? Status { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? SortBy { get; set; } = "PaymentDate";
    public bool SortDescending { get; set; } = true;
}
