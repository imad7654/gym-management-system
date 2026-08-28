namespace GymManagement.Application.DTOs.Reports;

/// <summary>One member's unfinished payment, as something the owner can act on.</summary>
public class OwedAmountDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;
    public decimal PackagePrice { get; set; }

    /// <summary>What they have handed over toward this package so far.</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>What is left before the membership starts. Always more than zero.</summary>
    public decimal AmountOwed { get; set; }

    /// <summary>When they first put money down against this package.</summary>
    public DateTime OwingSince { get; set; }

    /// <summary>
    /// How long they have been part-paid. The owner works the list from the top, and the
    /// oldest debt is the one least likely to be collected.
    /// </summary>
    public int DaysOutstanding { get; set; }

    /// <summary>
    /// Where their membership stands. A part payment never extends a membership, so most
    /// of these are Expired or Pending - which is exactly why the money is worth chasing.
    /// </summary>
    public string MembershipStatus { get; set; } = string.Empty;
}

public class WhoOwesMoneyDto
{
    public decimal TotalOwed { get; set; }
    public int MemberCount { get; set; }

    /// <summary>Longest outstanding first.</summary>
    public List<OwedAmountDto> Members { get; set; } = new();
}
