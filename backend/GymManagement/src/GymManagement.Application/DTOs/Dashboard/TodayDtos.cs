namespace GymManagement.Application.DTOs.Dashboard;

/// <summary>
/// One member on the call sheet, with everything needed to ring them without leaving the
/// page.
/// </summary>
public class NeedsChasingDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Digits only, for the tel: and WhatsApp links. Built with the same rule the desk
    /// search uses, so a number written "03 123 456" still dials.
    /// </summary>
    public string? PhoneDigits { get; set; }

    public string? PackageName { get; set; }
    public DateTime? MembershipEndDate { get; set; }

    /// <summary>
    /// Negative once the membership has run out. The sign is what sorts the list: the
    /// people who have already gone are the ones worth ringing first.
    /// </summary>
    public int? DaysRemaining { get; set; }

    public string MembershipStatus { get; set; } = string.Empty;

    /// <summary>True when somebody already rang them today, so nobody rings twice.</summary>
    public bool CalledToday { get; set; }

    /// <summary>When they were last called, whenever that was. Null if never.</summary>
    public DateTime? LastChasedAt { get; set; }
}

/// <summary>
/// The first screen of the day: what is in the drawer, who to ring, and who owes.
///
/// Composed from the reports that already exist rather than counted again here. The daily
/// takings figure on this page and the one on the takings report have to be the same
/// number, and the way to guarantee that is for there to be only one of it.
/// </summary>
public class TodayDto
{
    /// <summary>The gym's calendar date this describes.</summary>
    public DateTime Date { get; set; }

    // --- the drawer
    public decimal CashUsd { get; set; }
    public decimal CashLbpInUsd { get; set; }
    public decimal CashLbpReceived { get; set; }

    /// <summary>What should physically be in the drawer. Whish is deliberately not in it.</summary>
    public decimal DrawerTotalUsd { get; set; }

    /// <summary>Money that arrived by phone. Real income, never in the drawer.</summary>
    public decimal WhishUsd { get; set; }

    public decimal OtherUsd { get; set; }
    public decimal TotalUsd { get; set; }
    public int PaymentCount { get; set; }
    public int ReversalCount { get; set; }
    public decimal ReversalsUsd { get; set; }

    /// <summary>
    /// Payments today that actually moved a membership forward - the result of yesterday's
    /// chasing, and the number that says whether the calling is working.
    /// </summary>
    public int RenewalsToday { get; set; }

    // --- the call sheet
    public List<NeedsChasingDto> NeedsChasing { get; set; } = new();

    /// <summary>How many of those have already been rung today.</summary>
    public int CalledToday { get; set; }

    // --- who owes
    public decimal TotalOwed { get; set; }
    public int OwesCount { get; set; }
    public List<OwesSummaryDto> Owes { get; set; } = new();
}

/// <summary>A member who part-paid, trimmed to what the morning needs.</summary>
public class OwesSummaryDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public decimal AmountOwed { get; set; }
    public int DaysOutstanding { get; set; }
}

/// <summary>Marking somebody as rung, or undoing a mis-click.</summary>
public class MarkChasedRequest
{
    public bool Called { get; set; } = true;
}
