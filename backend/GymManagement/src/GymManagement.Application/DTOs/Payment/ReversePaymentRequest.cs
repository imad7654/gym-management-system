using System.ComponentModel.DataAnnotations;

namespace GymManagement.Application.DTOs.Payment;

public class ReversePaymentRequest
{
    /// <summary>
    /// Why the payment was reversed - wrong amount, wrong member, wrong package. Optional,
    /// but it is what the owner reads months later when checking why the takings moved.
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}
