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
}
