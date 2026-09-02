namespace GymManagement.Application.DTOs.Reports;

/// <summary>
/// One month on the revenue chart.
///
/// The money is <b>cash in</b>: a payment counts in the month it was taken, whole, even
/// when it bought three months of membership. That is what the drawer and the bank did, and
/// it is what every other money screen in this system says - a chart that spread the same
/// payment across three months would be defensible accounting and would still leave the
/// owner with two screens disagreeing about March.
/// </summary>
public class RevenueMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>"Mar 2026". Built server-side so every screen names the month the same way.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Everything taken that month, reversals already netted off.</summary>
    public decimal TotalUsd { get; set; }

    /// <summary>Cash that went into the drawer, USD and converted LBP together.</summary>
    public decimal DrawerUsd { get; set; }

    /// <summary>Money that arrived by phone. Real income that never touched the till.</summary>
    public decimal WhishUsd { get; set; }

    public decimal OtherUsd { get; set; }

    public int PaymentCount { get; set; }
    public int ReversalCount { get; set; }

    /// <summary>Negative. Already inside <see cref="TotalUsd"/>.</summary>
    public decimal ReversalsUsd { get; set; }

    /// <summary>
    /// Members whose membership covered the last day of that month.
    ///
    /// The trend worth watching alongside the money: a falling member count under flat
    /// revenue is an early warning that the money chart alone hides for a month or two.
    ///
    /// Reconstructed from the membership dates, which is the only history there is. A freeze
    /// is a single current flag with no record of when it was applied, so a member frozen
    /// today counts as having trained in every past month they had dates for. Worth knowing
    /// before reading small movements as signal.
    /// </summary>
    public int ActiveMembers { get; set; }

    /// <summary>
    /// True for the month still being lived through.
    ///
    /// The chart has to say so. Two days of takings beside a full previous month reads as a
    /// collapse, and an owner who is told that once stops believing the chart.
    /// </summary>
    public bool InProgress { get; set; }
}

/// <summary>Revenue and membership month by month, newest month last so it plots left to right.</summary>
public class RevenueTrendDto
{
    public List<RevenueMonthDto> Months { get; set; } = new();

    /// <summary>Everything taken across the whole window.</summary>
    public decimal TotalUsd { get; set; }

    /// <summary>The average month in the window, for reading whether the latest is unusual.</summary>
    public decimal AverageMonthUsd { get; set; }

    public string? BestMonthLabel { get; set; }
    public decimal BestMonthUsd { get; set; }
}

/// <summary>
/// One month opened up: the same split the daily takings report shows, a level higher.
/// </summary>
public class RevenueMonthDetailDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;

    public decimal TotalUsd { get; set; }
    public decimal DrawerUsd { get; set; }
    public decimal CashUsd { get; set; }
    public decimal CashLbpInUsd { get; set; }
    public decimal CashLbpReceived { get; set; }
    public decimal WhishUsd { get; set; }
    public decimal OtherUsd { get; set; }

    public int PaymentCount { get; set; }
    public int ReversalCount { get; set; }
    public decimal ReversalsUsd { get; set; }

    /// <summary>Payments that actually moved a membership forward, as opposed to part payments.</summary>
    public int RenewalCount { get; set; }

    /// <summary>Every movement, newest first. Same shape as the daily report's rows.</summary>
    public List<TakingsPaymentDto> Payments { get; set; } = new();
}
