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
    public int OverdueCount { get; set; }
}

public class RevenueSummary
{
    public decimal TodayRevenue { get; set; }
    public decimal ThisMonthRevenue { get; set; }
    public decimal LastMonthRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class RevenueChartDataDto
{
    public List<RevenueDataPoint> Data { get; set; } = new();
}

public class RevenueDataPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
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

public class RecentPaymentDto
{
    public int Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
}

public class RecentClientDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? PackageName { get; set; }
    public DateTime CreatedAt { get; set; }
}
