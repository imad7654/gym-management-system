using GymManagement.Application.DTOs.Auth;
using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Member;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IMemberAccountService _memberAccounts;
    private readonly IPasswordResetService _passwordReset;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(
        IAuthService authService,
        IMemberAccountService memberAccounts,
        IPasswordResetService passwordReset,
        ICurrentUserService currentUserService)
    {
        _authService = authService;
        _memberAccounts = memberAccounts;
        _passwordReset = passwordReset;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (result == null)
        {
            return Unauthorized(ApiResponse.FailResponse("Invalid email or password"));
        }

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(result, "Login successful"));
    }

    /// <summary>
    /// Sign up as a member, by matching a membership the gym already created.
    /// </summary>
    /// <remarks>
    /// Anonymous on purpose - the person signing up has no account yet. It is still not
    /// free signup: the phone number and surname have to match a member record the owner
    /// made at the desk, so nobody can add themselves to the member list.
    ///
    /// An expired membership signs up and signs in exactly like a current one. Being able
    /// to see "your membership ended 12 days ago" and a renew button is how a lapsed member
    /// comes back; locking them out hides it from the people the gym most wants to reach.
    /// </remarks>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterMemberRequest request)
    {
        var result = await _memberAccounts.RegisterAsync(request);

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(
            result, "Your account is ready."));
    }

    /// <summary>
    /// Asks for a password reset link. Works for administrators and members alike.
    /// </summary>
    /// <remarks>
    /// Always reports success, whether or not the address belongs to an account. This
    /// endpoint is public, so an answer that differed for a real address would turn it into
    /// a way of testing which emails the gym holds.
    /// </remarks>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _passwordReset.RequestResetAsync(request.Email, cancellationToken);

        return Ok(ApiResponse.SuccessResponse(
            "If that address has an account, a reset link is on its way. It works once and "
            + "expires in an hour."));
    }

    /// <summary>
    /// Sets a new password using the token from the emailed link.
    /// </summary>
    /// <remarks>
    /// Unknown, already-used and expired tokens are all refused identically - telling them
    /// apart would say which guesses were close. A successful reset ends every session that
    /// account holds.
    /// </remarks>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordWithTokenRequest request, CancellationToken cancellationToken)
    {
        var done = await _passwordReset.ResetAsync(
            request.Token, request.NewPassword, cancellationToken);

        if (!done)
        {
            return BadRequest(ApiResponse.FailResponse(
                "That reset link has already been used, or has expired. Ask for a new one."));
        }

        return Ok(ApiResponse.SuccessResponse(
            "Your password has been changed. Sign in with it now."));
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);

        if (result == null)
        {
            return Unauthorized(ApiResponse.FailResponse("Invalid or expired refresh token"));
        }

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(result, "Token refreshed successfully"));
    }

    /// <summary>
    /// Logout (revoke refresh token)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return Ok(ApiResponse.SuccessResponse("Logged out successfully"));
    }

    /// <summary>
    /// Get current user information
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized(ApiResponse.FailResponse("User not authenticated"));
        }

        var user = await _authService.GetCurrentUserAsync(_currentUserService.UserId.Value);

        if (user == null)
        {
            return Unauthorized(ApiResponse.FailResponse("User not found"));
        }

        return Ok(ApiResponse<UserInfo>.SuccessResponse(user));
    }

    /// <summary>
    /// Change password
    /// </summary>
    [HttpPut("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized(ApiResponse.FailResponse("User not authenticated"));
        }

        var result = await _authService.ChangePasswordAsync(_currentUserService.UserId.Value, request);

        if (!result)
        {
            return BadRequest(ApiResponse.FailResponse("Current password is incorrect"));
        }

        return Ok(ApiResponse.SuccessResponse("Password changed successfully"));
    }
}
