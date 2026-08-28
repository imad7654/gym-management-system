namespace GymManagement.Application.DTOs.Payment;

public class PaymentDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;

    /// <summary>The USD figure. The only one any report adds up.</summary>
    public decimal Amount { get; set; }

    /// <summary>What was physically handed over, in <see cref="Currency"/>.</summary>
    public decimal AmountReceived { get; set; }

    public string Currency { get; set; } = string.Empty;
    public decimal? ExchangeRate { get; set; }

    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>Null when the payment did not cover the price, and on reversal rows.</summary>
    public DateTime? PeriodStartDate { get; set; }
    public DateTime? PeriodEndDate { get; set; }

    /// <summary>Set on a reversal row to the payment it cancels.</summary>
    public int? ReversesPaymentId { get; set; }

    /// <summary>True when this payment was short of the package price.</summary>
    public bool IsPartial { get; set; }

    /// <summary>How much of the package price is still owed. Zero on a full payment.</summary>
    public decimal AmountOutstanding { get; set; }

    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentListDto
{
    public int Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal AmountReceived { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsReversal { get; set; }
}
