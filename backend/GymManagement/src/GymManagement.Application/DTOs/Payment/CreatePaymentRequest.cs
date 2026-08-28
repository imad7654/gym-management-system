using System.ComponentModel.DataAnnotations;
using GymManagement.Domain.Enums;

namespace GymManagement.Application.DTOs.Payment;

/// <summary>
/// What reception actually knows at the desk: who is paying, for what, how, and how much
/// changed hands.
///
/// Deliberately carries neither the price nor the membership period. Both are worked out on
/// the server from the package - previously the browser sent them and the service copied
/// them onto the client, which meant anyone who could reach this endpoint could grant
/// themselves a year for one month's money.
/// </summary>
public class CreatePaymentRequest
{
    [Required]
    public int ClientId { get; set; }

    [Required]
    public int PackageId { get; set; }

    /// <summary>
    /// What the member handed over, in <see cref="Currency"/>. Checked against the package
    /// price on the server; anything short is recorded without extending the membership.
    /// </summary>
    [Required]
    [Range(0.01, 1_000_000_000, ErrorMessage = "Amount received must be greater than zero")]
    public decimal AmountReceived { get; set; }

    public Currency Currency { get; set; } = Currency.Usd;

    /// <summary>
    /// LBP per USD. Required when paying in LBP, ignored otherwise. Reception can override
    /// the day's rate for a single payment.
    /// </summary>
    [Range(0.01, 100_000_000, ErrorMessage = "Exchange rate must be greater than zero")]
    public decimal? ExchangeRate { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(100)]
    public string? TransactionReference { get; set; }

    public string? Notes { get; set; }
}
