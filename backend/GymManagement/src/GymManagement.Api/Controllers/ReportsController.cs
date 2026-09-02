using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Reports;
using GymManagement.Application.Services;
using GymManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

/// <summary>
/// The money reports.
///
/// Two of these are desk work and one is not. Reception chases the people who owe money
/// and counts its own drawer at the end of a shift; reading back over past days, or over
/// who did what, is the owner auditing the business rather than running it.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = "AdminOrStaff")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMembershipClock _clock;

    public ReportsController(
        IReportService reportService,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        IMembershipClock clock)
    {
        _reportService = reportService;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _clock = clock;
    }

    /// <summary>
    /// Members who part-paid for a package and still owe the difference, longest
    /// outstanding first.
    /// </summary>
    [HttpGet("who-owes")]
    [ProducesResponseType(typeof(ApiResponse<WhoOwesMoneyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWhoOwesMoney(CancellationToken cancellationToken)
    {
        var report = await _reportService.GetWhoOwesMoneyAsync(cancellationToken);
        return Ok(ApiResponse<WhoOwesMoneyDto>.SuccessResponse(report));
    }

    /// <summary>
    /// One day's money, split into what should be in the drawer and what arrived by phone.
    /// </summary>
    /// <param name="date">
    /// A gym-calendar date as yyyy-MM-dd. Defaults to today in the gym's timezone, which is
    /// not necessarily the server's or the browser's.
    /// </param>
    [HttpGet("daily-takings")]
    [ProducesResponseType(typeof(ApiResponse<DailyTakingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDailyTakings(
        [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        // Reception counts today's drawer against today's figure. Reading back over past
        // days is revenue history by another name - the same thing the dashboard's all-time
        // total is withheld for - so the date is refused rather than quietly ignored, which
        // would show them today's money under yesterday's heading.
        if (date.HasValue && !_currentUserService.IsAdmin && date.Value != _clock.Today)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.FailResponse(
                "Reception can only see today's takings. Ask the owner for another day."));
        }

        var report = await _reportService.GetDailyTakingsAsync(date, cancellationToken);
        return Ok(ApiResponse<DailyTakingsDto>.SuccessResponse(report));
    }

    /// <summary>
    /// The audit trail: who did what, newest first.
    /// </summary>
    // The trail exists to check the people who use the system, reception included. Letting
    // it read its own entries would defeat the point of keeping one.
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("audit")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<AuditEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditTrail(
        [FromQuery] AuditQueryParameters parameters, CancellationToken cancellationToken)
    {
        var entries = await _auditService.GetEntriesAsync(parameters, cancellationToken);
        return Ok(ApiResponse<PaginatedResult<AuditEntryDto>>.SuccessResponse(entries));
    }
    /// <summary>
    /// Revenue and membership month by month, for the chart.
    /// </summary>
    /// <remarks>
    /// The owner's. Revenue history is the one thing reception is deliberately not shown,
    /// and this is exactly that.
    ///
    /// Money is counted as cash in - a payment belongs to the month it was taken, whole,
    /// even when it bought three months. That keeps this chart agreeing with the drawer,
    /// the daily takings report and the bank.
    /// </remarks>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(ApiResponse<RevenueTrendDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueTrend(
        [FromQuery] int months = 12, CancellationToken cancellationToken = default)
    {
        var trend = await _reportService.GetRevenueTrendAsync(months, cancellationToken);
        return Ok(ApiResponse<RevenueTrendDto>.SuccessResponse(trend));
    }

    /// <summary>
    /// One month of the chart opened up: every payment in it, split the way the daily
    /// takings report splits a day.
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("revenue/{year:int}/{month:int}")]
    [ProducesResponseType(typeof(ApiResponse<RevenueMonthDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRevenueMonth(
        int year, int month, CancellationToken cancellationToken)
    {
        var detail = await _reportService.GetRevenueMonthAsync(year, month, cancellationToken);
        return Ok(ApiResponse<RevenueMonthDetailDto>.SuccessResponse(detail));
    }
}
