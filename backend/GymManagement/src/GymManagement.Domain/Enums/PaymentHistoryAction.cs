namespace GymManagement.Domain.Enums;

public enum PaymentHistoryAction
{
    Created,
    Updated,
    Refunded,
    Cancelled,

    /// <summary>
    /// A reversal row was written against an earlier payment. Money rows are never edited
    /// or deleted, so a mistake at the desk is corrected by a second row pointing back at
    /// the first - not by changing what the first one says.
    /// </summary>
    Reversed
}
