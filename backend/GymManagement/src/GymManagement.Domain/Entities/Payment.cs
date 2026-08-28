using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

/// <summary>
/// One money movement. Rows are append-only: a mistake is corrected by adding a reversal
/// that points back at the original, never by editing or deleting what was already written.
/// If a payment can be quietly changed afterwards, the owner can never use this system to
/// check the till against the drawer, which is most of why they wanted it.
/// </summary>
public class Payment : AuditableEntity
{
    public int ClientId { get; set; }
    public int PackageId { get; set; }

    /// <summary>
    /// The price in USD. This is the only figure any report ever adds up, whatever
    /// currency was actually handed over.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>What the member physically handed over, in <see cref="Currency"/>.</summary>
    public decimal AmountReceived { get; set; }

    public Currency Currency { get; set; } = Currency.Usd;

    /// <summary>
    /// LBP per USD at the moment of payment, or null when paid in USD. Captured once and
    /// never recalculated - a payment taken at last month's rate must still read the same
    /// next year, or the books stop making sense.
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    public DateTime PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;

    /// <summary>
    /// Period this payment bought. Null on a payment that did not cover the package price
    /// and on reversal rows, because neither moves the membership.
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    public DateTime? PeriodEndDate { get; set; }

    /// <summary>
    /// Set on a reversal row to the payment it cancels out. Null on ordinary payments.
    /// </summary>
    public int? ReversesPaymentId { get; set; }

    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }

    /// <summary>A reversal carries the negative of what it cancels.</summary>
    public bool IsReversal => ReversesPaymentId.HasValue;

    // Navigation properties
    public virtual Client Client { get; set; } = null!;
    public virtual Package Package { get; set; } = null!;
    public virtual Payment? ReversesPayment { get; set; }
    public virtual ICollection<PaymentHistory> PaymentHistories { get; set; } = new List<PaymentHistory>();
}
