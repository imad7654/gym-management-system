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
    /// Reverse a payment. Writes a second row cancelling the original and takes back the
    /// days it bought; the original row is never edited. Returns the reversal row.
    /// </summary>
    [HttpPost("{id}/reverse")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReversePayment(int id, [FromBody] ReversePaymentRequest? request)
    {
        var reversal = await _paymentService.ReversePaymentAsync(
            id, request?.Reason, _currentUserService.UserId);

        return Ok(ApiResponse<PaymentDto>.SuccessResponse(reversal, "Payment reversed"));
    }
}
