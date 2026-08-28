using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.GymInfo;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

[ApiController]
// Explicit kebab-case route: [controller] would render as "GymInfo", which no client asks for.
[Route("api/v1/gym-info")]
public class GymInfoController : ControllerBase
{
    private readonly IGymInfoService _gymInfoService;
    private readonly ICurrentUserService _currentUserService;

    public GymInfoController(IGymInfoService gymInfoService, ICurrentUserService currentUserService)
    {
        _gymInfoService = gymInfoService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get gym information (Public - for homepage)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<GymInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGymInfo()
    {
        var gymInfo = await _gymInfoService.GetGymInfoAsync();

        if (gymInfo == null)
        {
            return NotFound(ApiResponse.FailResponse("Gym information not found"));
        }

        return Ok(ApiResponse<GymInfoDto>.SuccessResponse(gymInfo));
    }

    /// <summary>
    /// Update gym information (Admin only)
    /// </summary>
    [HttpPut]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<GymInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGymInfo([FromBody] UpdateGymInfoRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.FailResponse("Validation failed", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList()));
        }

        var gymInfo = await _gymInfoService.UpdateGymInfoAsync(request, _currentUserService.UserId);

        if (gymInfo == null)
        {
            return NotFound(ApiResponse.FailResponse("Gym information not found"));
        }

        return Ok(ApiResponse<GymInfoDto>.SuccessResponse(gymInfo, "Gym information updated successfully"));
    }
}
