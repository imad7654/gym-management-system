namespace GymManagement.Application.DTOs.Dashboard;

public class DashboardStatsDto
{
    public int TotalActiveClients { get; set; }
    public int TotalClients { get; set; }
    public int NewClientsThisMonth { get; set; }
    public int ExpiringMembershipsCount { get; set; }

    public PaymentSummary PaymentSummary { get; set; } = new();
    public RevenueSummary RevenueSummary { get; set; } = new();
}

public class PaymentSummary
{
    public int PaidCount { get; set; }
    public int PendingCount { get; set; }
    /// <summary>
    /// Members who handed over part of a package price and still owe the rest.
    ///
    /// This replaced an Overdue count that nothing in the system could ever set - no code
    /// path assigned it and no screen exposed the field, so it read zero forever while
    /// Partial members were counted in none of the three figures. The counts now add up to
    /// the member list.
    /// </summary>
    public int OwesMoneyCount { get; set; }
}

public class RevenueSummary
{
    public decimal TodayRevenue { get; set; }
    public decimal ThisMonthRevenue { get; set; }
    public decimal LastMonthRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class ExpiringMembershipDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public int DaysUntilExpiration { get; set; }
}
