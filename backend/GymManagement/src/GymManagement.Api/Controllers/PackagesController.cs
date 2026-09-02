using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Package;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _packageService;

    public PackagesController(IPackageService packageService)
    {
        _packageService = packageService;
    }

    /// <summary>
    /// Get all packages (Admin only - includes inactive)
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "AdminOrStaff")]
    [ProducesResponseType(typeof(ApiResponse<List<PackageListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPackages([FromQuery] bool includeInactive = false)
    {
        var packages = await _packageService.GetAllPackagesAsync(includeInactive);
        return Ok(ApiResponse<List<PackageListDto>>.SuccessResponse(packages));
    }

    /// <summary>
    /// Get active packages (Public - for homepage)
    /// </summary>
    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<PackageListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePackages()
    {
        var packages = await _packageService.GetActivePackagesAsync();
        return Ok(ApiResponse<List<PackageListDto>>.SuccessResponse(packages));
    }

    /// <summary>
    /// Get package by ID (Admin only)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "AdminOrStaff")]
    [ProducesResponseType(typeof(ApiResponse<PackageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPackage(int id)
    {
        var package = await _packageService.GetPackageByIdAsync(id);

        if (package == null)
        {
            return NotFound(ApiResponse.FailResponse("Package not found"));
        }

        return Ok(ApiResponse<PackageDto>.SuccessResponse(package));
    }

    /// <summary>
    /// Create a new package (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PackageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePackage([FromBody] CreatePackageRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.FailResponse("Validation failed", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList()));
        }

        var package = await _packageService.CreatePackageAsync(request);
        return CreatedAtAction(nameof(GetPackage), new { id = package.Id },
            ApiResponse<PackageDto>.SuccessResponse(package, "Package created successfully"));
    }

    /// <summary>
    /// Update an existing package (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PackageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePackage(int id, [FromBody] UpdatePackageRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.FailResponse("Validation failed", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList()));
        }

        var package = await _packageService.UpdatePackageAsync(id, request);

        if (package == null)
        {
            return NotFound(ApiResponse.FailResponse("Package not found"));
        }

        return Ok(ApiResponse<PackageDto>.SuccessResponse(package, "Package updated successfully"));
    }

    /// <summary>
    /// Delete a package (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePackage(int id)
    {
        var result = await _packageService.DeletePackageAsync(id);

        if (!result)
        {
            return NotFound(ApiResponse.FailResponse("Package not found"));
        }

        return Ok(ApiResponse.SuccessResponse("Package deleted successfully"));
    }

    /// <summary>
    /// Toggle package active status (Admin only)
    /// </summary>
    [HttpPut("{id}/toggle-status")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TogglePackageStatus(int id)
    {
        var result = await _packageService.TogglePackageStatusAsync(id);

        if (!result)
        {
            return NotFound(ApiResponse.FailResponse("Package not found"));
        }

        return Ok(ApiResponse.SuccessResponse("Package status toggled successfully"));
    }
}
