using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.User;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

/// <summary>
/// The accounts that can sign in and run the gym. Members are not here - they get their own
/// accounts separately, matched to a record the owner already created.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUserService;

    public UsersController(IUserService userService, ICurrentUserService currentUserService)
    {
        _userService = userService;
        _currentUserService = currentUserService;
    }

    /// <summary>Everyone who can sign in, switched-off accounts included.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<UserListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userService.GetUsersAsync(_currentUserService.UserId ?? 0);
        return Ok(ApiResponse<List<UserListDto>>.SuccessResponse(users));
    }

    /// <summary>Adds another administrator.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserListDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = await _userService.CreateUserAsync(request, _currentUserService.UserId);

        return CreatedAtAction(
            nameof(GetUsers), null,
            ApiResponse<UserListDto>.SuccessResponse(user, "Account created"));
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userService.UpdateUserAsync(id, request, _currentUserService.UserId);

        if (user == null)
        {
            return NotFound(ApiResponse.FailResponse("Account not found"));
        }

        return Ok(ApiResponse<UserListDto>.SuccessResponse(user, "Account updated"));
    }

    /// <summary>
    /// Stops this account signing in and ends its live sessions. Refused for the last
    /// administrator, and for your own account.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var done = await _userService.DeactivateUserAsync(id, _currentUserService.UserId ?? 0);

        if (!done)
        {
            return NotFound(ApiResponse.FailResponse("Account not found"));
        }

        return Ok(ApiResponse.SuccessResponse("Account switched off"));
    }

    [HttpPost("{id}/restore")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreUser(int id)
    {
        var done = await _userService.RestoreUserAsync(id, _currentUserService.UserId);

        if (!done)
        {
            return NotFound(ApiResponse.FailResponse("Account not found"));
        }

        return Ok(ApiResponse.SuccessResponse("Account switched back on"));
    }

    /// <summary>
    /// Sets someone else's password, for when they have forgotten it and ask at the desk.
    /// The current password is not required - that is the point - so every use is recorded
    /// in the audit trail.
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetUserPasswordRequest request)
    {
        var done = await _userService.ResetPasswordAsync(id, request, _currentUserService.UserId);

        if (!done)
        {
            return NotFound(ApiResponse.FailResponse("Account not found"));
        }

        return Ok(ApiResponse.SuccessResponse(
            "Password reset. They will need to sign in again."));
    }
}
