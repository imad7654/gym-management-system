using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

public class PaymentHistory : BaseEntity
{
    public int PaymentId { get; set; }
    public int ClientId { get; set; }
    public PaymentHistoryAction Action { get; set; }
    public decimal? OldAmount { get; set; }
    public decimal? NewAmount { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? ChangeDescription { get; set; }
    public int? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Payment Payment { get; set; } = null!;
    public virtual Client Client { get; set; } = null!;
}
