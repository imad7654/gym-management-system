namespace GymManagement.Application.DTOs.Client;

/// <summary>
/// Everything the member page shows, in one call.
///
/// Deliberately one request rather than four. The page is used at the desk with a member
/// standing there, often on a phone on gym wifi, and four round trips is the difference
/// between the page being there and the page being a spinner.
/// </summary>
public class MemberSummaryDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }

    /// <summary>
    /// The phone number reduced to digits, for <c>tel:</c> and WhatsApp links. Built here
    /// rather than in the browser so both use the same rule the rest of the system matches
    /// members by.
    /// </summary>
    public string? PhoneDigits { get; set; }

    public string MembershipStatus { get; set; } = string.Empty;
    public bool IsSuspended { get; set; }

    /// <summary>
    /// Days left, inclusive of today, or null if they have never paid. Negative once the
    /// membership has lapsed, so the page can say "expired 12 days ago" rather than just
    /// "expired".
    /// </summary>
    public int? DaysRemaining { get; set; }

    public DateTime? MembershipStartDate { get; set; }
    public DateTime? MembershipEndDate { get; set; }
    public int? CurrentPackageId { get; set; }
    public string? CurrentPackageName { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Packages this member has part-paid for and still owes money on.</summary>
    public List<OutstandingPackageDto> Outstanding { get; set; } = new();

    /// <summary>Total still owed across all of them, or zero.</summary>
    public decimal TotalOwed { get; set; }

    public List<MemberPaymentDto> Payments { get; set; } = new();
}

/// <summary>
/// Money a member has put toward one package that has not bought them anything yet.
///
/// Built from <c>PaymentQueries.OutstandingCredit()</c> - the same definition the payment
/// desk credits against and the who-owes-money report bills from. If this used its own
/// arithmetic, the member page could tell reception a different figure from the report.
/// </summary>
public class OutstandingPackageDto
{
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal PackagePrice { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountOwed { get; set; }
    public DateTime OwingSince { get; set; }
}

/// <summary>One row of a member's money history.</summary>
public class MemberPaymentDto
{
    public int Id { get; set; }
    public DateTime PaidAt { get; set; }
    public string? PackageName { get; set; }

    /// <summary>Always USD, whatever was handed over.</summary>
    public decimal AmountUsd { get; set; }

    public decimal AmountReceived { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? ExchangeRate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>A correction, not a payment. Shown as such rather than as negative income.</summary>
    public bool IsReversal { get; set; }

    /// <summary>
    /// True when a reversal already cancels this payment, so the page can stop offering to
    /// refund money that has already been handed back. The server refuses a second
    /// reversal, but only after reception has clicked it and read an error.
    /// </summary>
    public bool IsReversed { get; set; }

    /// <summary>Set only on the payment that completed a purchase - the one that moved the dates.</summary>
    public DateTime? PeriodStartDate { get; set; }
    public DateTime? PeriodEndDate { get; set; }

    public string? Notes { get; set; }
}
