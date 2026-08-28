using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Import;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymManagement.Api.Controllers;

/// <summary>
/// The one-time route in for the gym's existing member list.
///
/// Separate from ClientsController because it is a two-step conversation - check, then
/// confirm - rather than the single create/update/delete the rest of the client endpoints
/// do, and because it is the only place in the system that accepts an uploaded file.
/// </summary>
[ApiController]
[Route("api/v1/clients/import")]
[Authorize(Policy = "AdminOnly")]
public class ClientImportController : ControllerBase
{
    private readonly IMemberImportService _importService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMemberImportFileReader _fileReader;
    private readonly long _maxFileSizeBytes;

    public ClientImportController(
        IMemberImportService importService,
        ICurrentUserService currentUserService,
        IMemberImportFileReader fileReader,
        IConfiguration configuration)
    {
        _importService = importService;
        _currentUserService = currentUserService;
        _fileReader = fileReader;
        _maxFileSizeBytes = (configuration.GetValue<int?>("FileUpload:MaxFileSizeMB") ?? 5) * 1024L * 1024L;
    }

    /// <summary>
    /// Download a starter file with the headings the import expects.
    /// </summary>
    [HttpGet("template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetTemplate()
    {
        return File(_importService.BuildTemplateCsv(), "text/csv", "fit-bear-members-template.csv");
    }

    /// <summary>
    /// Check a member list and report what would happen. Writes nothing.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(ApiResponse<MemberImportPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview(IFormFile file, CancellationToken cancellationToken)
    {
        ValidateUpload(file);

        await using var stream = file.OpenReadStream();
        var preview = await _importService.PreviewAsync(stream, file.FileName, cancellationToken);

        return Ok(ApiResponse<MemberImportPreviewDto>.SuccessResponse(preview));
    }

    /// <summary>
    /// Import the rows that passed. All or nothing.
    /// </summary>
    /// <param name="fileHash">The hash the preview returned, proving this is the file that was checked.</param>
    /// <param name="acknowledgeSkipped">Set when the owner has chosen to import without the rows that failed.</param>
    [HttpPost("commit")]
    [ProducesResponseType(typeof(ApiResponse<MemberImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Commit(
        IFormFile file,
        [FromForm] string fileHash,
        [FromForm] bool acknowledgeSkipped,
        CancellationToken cancellationToken)
    {
        ValidateUpload(file);

        if (string.IsNullOrWhiteSpace(fileHash))
        {
            throw new BusinessException("Check the file before importing it.");
        }

        await using var stream = file.OpenReadStream();
        var result = await _importService.CommitAsync(
            stream, file.FileName, fileHash, acknowledgeSkipped, _currentUserService.UserId, cancellationToken);

        return Ok(ApiResponse<MemberImportResultDto>.SuccessResponse(
            result, $"Imported {result.ImportedCount} members."));
    }

    /// <summary>
    /// Rejects what should never reach the parser. Checked in the controller rather than the
    /// service because size and file name are transport facts, not import rules.
    /// </summary>
    private void ValidateUpload(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            throw new BusinessException("No file was uploaded, or the file is empty.");
        }

        if (file.Length > _maxFileSizeBytes)
        {
            throw new BusinessException(
                $"That file is larger than the {_maxFileSizeBytes / 1024 / 1024} MB limit.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_fileReader.SupportedExtensions.Contains(extension))
        {
            throw new BusinessException(
                $"'{file.FileName}' is not a supported file. Upload a "
                + string.Join(" or ", _fileReader.SupportedExtensions) + " file.");
        }
    }
}
