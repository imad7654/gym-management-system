using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Dashboard;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "AdminOrStaff")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Get dashboard statistics
    /// </summary>
    // Carries all-time revenue, which is the owner's business and nobody else's.
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _dashboardService.GetStatsAsync();
        return Ok(ApiResponse<DashboardStatsDto>.SuccessResponse(stats));
    }

    /// <summary>
    /// Get expiring memberships
    /// </summary>
    [HttpGet("expiring-memberships")]
    [ProducesResponseType(typeof(ApiResponse<List<ExpiringMembershipDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpiringMemberships(
        [FromQuery] int days = Client.ExpiringWindowDays)
    {
        var data = await _dashboardService.GetExpiringMembershipsAsync(days);
        return Ok(ApiResponse<List<ExpiringMembershipDto>>.SuccessResponse(data));
    }
    /// <summary>
    /// The first screen of the day: the drawer, who to ring, and who owes.
    /// </summary>
    /// <remarks>
    /// Open to reception as well as the owner. Everything on it - today's takings, the call
    /// sheet, who part-paid - is already theirs to see; the figure they are not shown, and
    /// which is not on this screen at all, is revenue history.
    /// </remarks>
    [HttpGet("today")]
    [ProducesResponseType(typeof(ApiResponse<TodayDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetToday(CancellationToken cancellationToken)
    {
        var today = await _dashboardService.GetTodayAsync(cancellationToken);
        return Ok(ApiResponse<TodayDto>.SuccessResponse(today));
    }

    /// <summary>
    /// Records that somebody rang this member about renewing, or takes the mark back off.
    /// </summary>
    /// <remarks>
    /// Desk work, so reception can do it - they are usually the ones making the calls, and
    /// a list that only the owner could tick would be ticked by nobody.
    /// </remarks>
    [HttpPost("chased/{clientId}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkChased(
        int clientId, [FromBody] MarkChasedRequest? request, CancellationToken cancellationToken)
    {
        var called = request?.Called ?? true;
        var done = await _dashboardService.MarkChasedAsync(clientId, called, cancellationToken);

        if (!done)
        {
            return NotFound(ApiResponse.FailResponse("Member not found"));
        }

        return Ok(ApiResponse.SuccessResponse(called ? "Marked as called" : "Mark removed"));
    }
}
