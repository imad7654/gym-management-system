using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

public class Payment : AuditableEntity
{
    public int ClientId { get; set; }
    public int PackageId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;

    // Period covered by this payment
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }

    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Client Client { get; set; } = null!;
    public virtual Package Package { get; set; } = null!;
    public virtual ICollection<PaymentHistory> PaymentHistories { get; set; } = new List<PaymentHistory>();
}
