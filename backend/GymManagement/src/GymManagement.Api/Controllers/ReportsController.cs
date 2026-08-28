using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Reports;
using GymManagement.Application.Services;
using GymManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

/// <summary>
/// The owner's money reports - the numbers they check the business against.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = "AdminOnly")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IAuditService _auditService;

    public ReportsController(IReportService reportService, IAuditService auditService)
    {
        _reportService = reportService;
        _auditService = auditService;
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
        var report = await _reportService.GetDailyTakingsAsync(date, cancellationToken);
        return Ok(ApiResponse<DailyTakingsDto>.SuccessResponse(report));
    }

    /// <summary>
    /// The audit trail: who did what, newest first.
    /// </summary>
    [HttpGet("audit")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<AuditEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditTrail(
        [FromQuery] AuditQueryParameters parameters, CancellationToken cancellationToken)
    {
        var entries = await _auditService.GetEntriesAsync(parameters, cancellationToken);
        return Ok(ApiResponse<PaginatedResult<AuditEntryDto>>.SuccessResponse(entries));
    }
}
