using System.ComponentModel.DataAnnotations;
using GymManagement.Domain.Enums;

namespace GymManagement.Application.DTOs.Payment;

public class CreatePaymentRequest
{
    [Required]
    public int ClientId { get; set; }

    [Required]
    public int PackageId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Required]
    public DateTime PeriodStartDate { get; set; }

    [Required]
    public DateTime PeriodEndDate { get; set; }

    [MaxLength(100)]
    public string? TransactionReference { get; set; }

    public string? Notes { get; set; }
}
