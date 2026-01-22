using GymManagement.Application.DTOs.Client;
using GymManagement.Application.DTOs.Common;
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
    private readonly ICurrentUserService _currentUserService;

    public ClientsController(
        IClientService clientService,
        IPaymentService paymentService,
        ICurrentUserService currentUserService)
    {
        _clientService = clientService;
        _paymentService = paymentService;
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
        var result = await _clientService.DeleteClientAsync(id);

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
        var result = await _clientService.RestoreClientAsync(id);

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
    public async Task<IActionResult> GetExpiringClients([FromQuery] int days = 7)
    {
        var clients = await _clientService.GetExpiringClientsAsync(days);
        return Ok(ApiResponse<List<ClientListDto>>.SuccessResponse(clients));
    }
}
