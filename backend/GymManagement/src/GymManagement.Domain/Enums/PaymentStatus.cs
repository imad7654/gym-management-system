namespace GymManagement.Domain.Enums;

/// <summary>Whether a member is square with the gym. Stored as a string.</summary>
public enum PaymentStatus
{
    Paid,
    Pending,
    Overdue,

    /// <summary>
    /// Handed over less than the package price. The payment is recorded but the membership
    /// is not extended - a half payment that silently unlocks a full month is how a gym
    /// loses track of its income.
    /// </summary>
    Partial
}
