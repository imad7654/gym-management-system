using GymManagement.Application.DTOs.Client;
using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Member;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly IPaymentService _paymentService;
    private readonly IMemberAccountService _memberAccounts;
    private readonly ICurrentUserService _currentUserService;

    public ClientsController(
        IClientService clientService,
        IPaymentService paymentService,
        IMemberAccountService memberAccounts,
        ICurrentUserService currentUserService)
    {
        _clientService = clientService;
        _paymentService = paymentService;
        _memberAccounts = memberAccounts;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all clients with pagination and filters
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<ClientListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClients([FromQuery] ClientQueryParameters parameters)
    {
        var result = await _clientService.GetClientsAsync(parameters);
        return Ok(ApiResponse<PaginatedResult<ClientListDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get client by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ClientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClient(int id)
    {
        var client = await _clientService.GetClientByIdAsync(id);

        if (client == null)
        {
            return NotFound(ApiResponse.FailResponse("Client not found"));
        }

        return Ok(ApiResponse<ClientDto>.SuccessResponse(client));
    }

    /// <summary>
    /// Create a new client
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ClientDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.FailResponse("Validation failed", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList()));
        }

        var client = await _clientService.CreateClientAsync(request, _currentUserService.UserId);
        return CreatedAtAction(nameof(GetClient), new { id = client.Id },
            ApiResponse<ClientDto>.SuccessResponse(client, "Client created successfully"));
    }

    /// <summary>
    /// Update an existing client
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ClientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateClient(int id, [FromBody] UpdateClientRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.FailResponse("Validation failed", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList()));
        }

        var client = await _clientService.UpdateClientAsync(id, request, _currentUserService.UserId);

        if (client == null)
        {
            return NotFound(ApiResponse.FailResponse("Client not found"));
        }

        return Ok(ApiResponse<ClientDto>.SuccessResponse(client, "Client updated successfully"));
    }

    /// <summary>
    /// Soft delete a client
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClient(int id)
    {
        var result = await _clientService.DeleteClientAsync(id, _currentUserService.UserId);

        if (!result)
        {
            return NotFound(ApiResponse.FailResponse("Client not found"));
        }

        return Ok(ApiResponse.SuccessResponse("Client deleted successfully"));
    }

    /// <summary>
    /// Restore a soft-deleted client
    /// </summary>
    [HttpPost("{id}/restore")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreClient(int id)
    {
        var result = await _clientService.RestoreClientAsync(id, _currentUserService.UserId);

        if (!result)
        {
            return NotFound(ApiResponse.FailResponse("Client not found"));
        }

        return Ok(ApiResponse.SuccessResponse("Client restored successfully"));
    }

    /// <summary>
    /// Get client's payment history
    /// </summary>
    [HttpGet("{id}/payments")]
    [ProducesResponseType(typeof(ApiResponse<List<Application.DTOs.Payment.PaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientPayments(int id)
    {
        var payments = await _paymentService.GetClientPaymentsAsync(id);
        return Ok(ApiResponse<List<Application.DTOs.Payment.PaymentDto>>.SuccessResponse(payments));
    }

    /// <summary>
    /// Get clients with expiring memberships
    /// </summary>
    [HttpGet("expiring")]
    [ProducesResponseType(typeof(ApiResponse<List<ClientListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpiringClients(
        [FromQuery] int days = GymManagement.Domain.Entities.Client.ExpiringWindowDays)
    {
        var clients = await _clientService.GetExpiringClientsAsync(days);
        return Ok(ApiResponse<List<ClientListDto>>.SuccessResponse(clients));
    }

    /// <summary>
    /// Everything the member page shows - details, status, what they owe, and their payment
    /// history - in one request.
    /// </summary>
    [HttpGet("{id}/summary")]
    [ProducesResponseType(typeof(ApiResponse<MemberSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberSummary(int id)
    {
        var summary = await _clientService.GetMemberSummaryAsync(id);

        if (summary == null)
        {
            return NotFound(ApiResponse.FailResponse("Client not found"));
        }

        return Ok(ApiResponse<MemberSummaryDto>.SuccessResponse(summary));
    }

    /// <summary>
    /// Money this member has put toward packages they have not finished paying for.
    ///
    /// Used by the payment desk to credit a new payment against what is already down, so
    /// reception is told the payment completes the package rather than warned that it falls
    /// short of the full price.
    /// </summary>
    [HttpGet("{id}/outstanding")]
    [ProducesResponseType(typeof(ApiResponse<List<OutstandingPackageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutstanding(int id)
    {
        var outstanding = await _clientService.GetOutstandingAsync(id);
        return Ok(ApiResponse<List<OutstandingPackageDto>>.SuccessResponse(outstanding));
    }

    /// <summary>
    /// Freezes a membership for travel or injury. The dates are left untouched - a freeze
    /// stops them being let in, it does not give days back.
    /// </summary>
    [HttpPost("{id}/suspend")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendClient(int id, [FromBody] SuspendClientRequest? request)
    {
        var result = await _clientService.SetSuspendedAsync(
            id, suspended: true, request?.Reason, _currentUserService.UserId);

        if (!result)
        {
            return NotFound(ApiResponse.FailResponse("Client not found"));
        }

        return Ok(ApiResponse.SuccessResponse("Membership frozen"));
    }

    /// <summary>
    /// Lifts a freeze. The membership goes straight back to whatever its dates say.
    /// </summary>
    [HttpPost("{id}/resume")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeClient(int id)
    {
        var result = await _clientService.SetSuspendedAsync(
            id, suspended: false, null, _currentUserService.UserId);

        if (!result)
        {
            return NotFound(ApiResponse.FailResponse("Client not found"));
        }

        return Ok(ApiResponse.SuccessResponse("Membership unfrozen"));
    }
    /// <summary>
    /// Whether this member has a login of their own, for the owner's view of them.
    /// </summary>
    [HttpGet("{id}/account")]
    [ProducesResponseType(typeof(ApiResponse<MemberAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberAccount(int id, CancellationToken cancellationToken)
    {
        var account = await _memberAccounts.GetAccountForClientAsync(id, cancellationToken);
        return Ok(ApiResponse<MemberAccountDto>.SuccessResponse(account));
    }

    /// <summary>
    /// Sets a member's password for them, when they have forgotten it.
    /// </summary>
    /// <remarks>
    /// The only recovery a member has. There is no email anywhere in this system, so there
    /// is no reset link to send - the member asks at the desk and the owner sets it here.
    /// Every session they hold is ended, and the reset is written to the audit trail.
    /// </remarks>
    [HttpPost("{id}/account/reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetMemberPassword(
        int id, [FromBody] ResetMemberPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _memberAccounts.ResetMemberPasswordAsync(
            id, request, _currentUserService.UserId, cancellationToken);

        if (!result)
        {
            return NotFound(ApiResponse.FailResponse("That member has no account."));
        }

        return Ok(ApiResponse.SuccessResponse(
            "Password reset. Tell the member the new password - it is not stored anywhere."));
    }
}
