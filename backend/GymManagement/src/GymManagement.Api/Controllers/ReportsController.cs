using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Reports;
using GymManagement.Application.Services;
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

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
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
}
