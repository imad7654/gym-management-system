using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.ExchangeRate;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

/// <summary>
/// Today's LBP-per-USD rate: set once each morning by the owner, read all day by the
/// payment form.
/// </summary>
[ApiController]
[Route("api/v1/exchange-rates")]
[Authorize(Policy = "AdminOrStaff")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ICurrentUserService _currentUserService;

    public ExchangeRatesController(
        IExchangeRateService exchangeRateService,
        ICurrentUserService currentUserService)
    {
        _exchangeRateService = exchangeRateService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// The rate the payment form should offer. 200 with null data when none has ever been
    /// set - that is a gym that has not started yet, not an error.
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var rate = await _exchangeRateService.GetCurrentAsync(cancellationToken);
        return Ok(ApiResponse<ExchangeRateDto?>.SuccessResponse(rate));
    }

    /// <summary>
    /// Set or correct today's rate.
    /// </summary>
    // Reception reads the rate to take an LBP payment; moving it changes what every LBP
    // payment that day is worth, so the owner sets it once each morning.
    [Authorize(Policy = "AdminOnly")]
    [HttpPut("today")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetToday(
        [FromBody] SetExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var rate = await _exchangeRateService.SetTodaysRateAsync(
            request.Rate, _currentUserService.UserId, cancellationToken);

        return Ok(ApiResponse<ExchangeRateDto>.SuccessResponse(rate, "Today's rate saved."));
    }
}
