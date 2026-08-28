namespace GymManagement.Domain.Enums;

/// <summary>
/// What the member physically handed over. Packages are always priced in USD; LBP cash is
/// converted at the rate captured on the day and never recalculated afterwards.
/// </summary>
public enum Currency
{
    Usd,
    Lbp
}
