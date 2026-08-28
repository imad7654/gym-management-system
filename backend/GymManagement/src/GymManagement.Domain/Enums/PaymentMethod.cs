namespace GymManagement.Domain.Enums;

public enum PaymentMethod
{
    Cash,
    Card,
    BankTransfer,
    Other,

    /// <summary>Paid by the member through the hosted checkout, not at the desk.</summary>
    Online
}
