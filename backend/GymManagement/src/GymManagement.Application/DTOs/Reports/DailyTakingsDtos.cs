namespace GymManagement.Application.DTOs.Reports;

/// <summary>One movement on the day, so the owner can check a figure they doubt.</summary>
public class TakingsPaymentDto
{
    public int Id { get; set; }

    /// <summary>When it happened, in the gym's own timezone.</summary>
    public DateTime TakenAt { get; set; }

    public string ClientName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;

    /// <summary>What was physically handed over, in <see cref="Currency"/>.</summary>
    public decimal AmountReceived { get; set; }

    /// <summary>The USD figure. Negative on a reversal.</summary>
    public decimal AmountUsd { get; set; }

    public decimal? ExchangeRate { get; set; }

    /// <summary>True when this row gave money back rather than took it.</summary>
    public bool IsReversal { get; set; }
}

/// <summary>
/// One day's money, arranged the way the owner checks it: what should be in the drawer,
/// then what came in but is not.
/// </summary>
public class DailyTakingsDto
{
    /// <summary>The gym's calendar date this covers.</summary>
    public DateTime Date { get; set; }

    // ---- in the drawer ----

    public decimal CashUsd { get; set; }

    /// <summary>LBP cash as the notes themselves, which is what gets counted.</summary>
    public decimal CashLbpReceived { get; set; }

    /// <summary>The same LBP converted at the rate each payment was taken at.</summary>
    public decimal CashLbpInUsd { get; set; }

    /// <summary>
    /// What the drawer should hold, in USD. This is the number the owner counts against.
    /// </summary>
    public decimal DrawerTotalUsd { get; set; }

    // ---- came in, but not in the drawer ----

    /// <summary>
    /// Whish transfers. Real income that never touched the till - mixing it into the drawer
    /// figure is what makes a takings report stop reconciling, and then stop being trusted.
    /// </summary>
    public decimal WhishUsd { get; set; }

    public decimal OtherUsd { get; set; }

    /// <summary>Everything, in USD: the drawer plus what arrived by phone.</summary>
    public decimal TotalUsd { get; set; }

    /// <summary>How many payments were taken. Reversals are counted separately.</summary>
    public int PaymentCount { get; set; }

    public int ReversalCount { get; set; }

    /// <summary>
    /// Money handed back today, as a negative figure. Already included in the totals above -
    /// shown on its own because a drawer that is short by a refund is not a drawer that is
    /// short.
    /// </summary>
    public decimal ReversalsUsd { get; set; }

    /// <summary>Every movement on the day, most recent first.</summary>
    public List<TakingsPaymentDto> Payments { get; set; } = new();
}
