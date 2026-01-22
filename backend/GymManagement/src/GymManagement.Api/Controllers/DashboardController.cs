using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Dashboard;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "AdminOnly")]
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
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _dashboardService.GetStatsAsync();
        return Ok(ApiResponse<DashboardStatsDto>.SuccessResponse(stats));
    }

    /// <summary>
    /// Get revenue chart data
    /// </summary>
    [HttpGet("revenue-chart")]
    [ProducesResponseType(typeof(ApiResponse<RevenueChartDataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueChart([FromQuery] int months = 6)
    {
        var data = await _dashboardService.GetRevenueChartAsync(months);
        return Ok(ApiResponse<RevenueChartDataDto>.SuccessResponse(data));
    }

    /// <summary>
    /// Get expiring memberships
    /// </summary>
    [HttpGet("expiring-memberships")]
    [ProducesResponseType(typeof(ApiResponse<List<ExpiringMembershipDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpiringMemberships([FromQuery] int days = 7)
    {
        var data = await _dashboardService.GetExpiringMembershipsAsync(days);
        return Ok(ApiResponse<List<ExpiringMembershipDto>>.SuccessResponse(data));
    }

    /// <summary>
    /// Get recent payments
    /// </summary>
    [HttpGet("recent-payments")]
    [ProducesResponseType(typeof(ApiResponse<List<RecentPaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentPayments([FromQuery] int count = 5)
    {
        var data = await _dashboardService.GetRecentPaymentsAsync(count);
        return Ok(ApiResponse<List<RecentPaymentDto>>.SuccessResponse(data));
    }

    /// <summary>
    /// Get recent clients
    /// </summary>
    [HttpGet("recent-clients")]
    [ProducesResponseType(typeof(ApiResponse<List<RecentClientDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentClients([FromQuery] int count = 5)
    {
        var data = await _dashboardService.GetRecentClientsAsync(count);
        return Ok(ApiResponse<List<RecentClientDto>>.SuccessResponse(data));
    }
}
