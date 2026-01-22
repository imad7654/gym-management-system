using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUserService;

    public PaymentsController(IPaymentService paymentService, ICurrentUserService currentUserService)
    {
        _paymentService = paymentService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all payments with pagination and filters
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<PaymentListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments([FromQuery] PaymentQueryParameters parameters)
    {
        var result = await _paymentService.GetPaymentsAsync(parameters);
        return Ok(ApiResponse<PaginatedResult<PaymentListDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get payment by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(int id)
    {
        var payment = await _paymentService.GetPaymentByIdAsync(id);

        if (payment == null)
        {
            return NotFound(ApiResponse.FailResponse("Payment not found"));
        }

        return Ok(ApiResponse<PaymentDto>.SuccessResponse(payment));
    }

    /// <summary>
    /// Create a new payment
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.FailResponse("Validation failed", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList()));
        }

        var payment = await _paymentService.CreatePaymentAsync(request, _currentUserService.UserId);
        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id },
            ApiResponse<PaymentDto>.SuccessResponse(payment, "Payment created successfully"));
    }

    /// <summary>
    /// Refund a payment
    /// </summary>
    [HttpPost("{id}/refund")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefundPayment(int id)
    {
        var result = await _paymentService.RefundPaymentAsync(id, _currentUserService.UserId);

        if (!result)
        {
            return NotFound(ApiResponse.FailResponse("Payment not found"));
        }

        return Ok(ApiResponse.SuccessResponse("Payment refunded successfully"));
    }
}
