using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Member;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

/// <summary>
/// What a member can see about themselves, and nothing else.
///
/// Every route here resolves the membership from the signed-in user rather than from a
/// route parameter. There is deliberately no <c>/api/v1/me/{id}</c>: an id in the URL is an
/// id somebody can change, and the first bug that follows is one member reading another's
/// payment history.
///
/// This is the first controller in the system that is not AdminOnly.
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize(Policy = "ClientOnly")]
public class MeController : ControllerBase
{
    private readonly IMemberAccountService _memberAccounts;
    private readonly ICurrentUserService _currentUserService;

    public MeController(
        IMemberAccountService memberAccounts,
        ICurrentUserService currentUserService)
    {
        _memberAccounts = memberAccounts;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// The signed-in member's own membership: status, days left, package and dates.
    /// </summary>
    /// <remarks>
    /// An expired membership is returned normally, with a negative days-remaining, so the
    /// page can say how long ago it ran out. The 404 below means something different - the
    /// account is no longer attached to a membership at all, because the gym removed the
    /// member record.
    /// </remarks>
    [HttpGet("membership")]
    [ProducesResponseType(typeof(ApiResponse<MyMembershipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyMembership(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized(ApiResponse.FailResponse("User not authenticated"));
        }

        var membership = await _memberAccounts.GetMyMembershipAsync(
            _currentUserService.UserId.Value, cancellationToken);

        if (membership == null)
        {
            return NotFound(ApiResponse.FailResponse(
                "This account is not attached to a membership. Please speak to the gym."));
        }

        return Ok(ApiResponse<MyMembershipDto>.SuccessResponse(membership));
    }

    /// <summary>The signed-in member's own payment history, newest first.</summary>
    [HttpGet("payments")]
    [ProducesResponseType(typeof(ApiResponse<List<PaymentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyPayments(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized(ApiResponse.FailResponse("User not authenticated"));
        }

        var payments = await _memberAccounts.GetMyPaymentsAsync(
            _currentUserService.UserId.Value, cancellationToken);

        if (payments == null)
        {
            return NotFound(ApiResponse.FailResponse(
                "This account is not attached to a membership. Please speak to the gym."));
        }

        return Ok(ApiResponse<List<PaymentDto>>.SuccessResponse(payments));
    }
}
