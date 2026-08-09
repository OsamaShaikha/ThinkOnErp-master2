using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Validates and imports the approved chart-of-accounts XLSX workbook.
/// </summary>
[ApiController]
[Route("api/accounting/coa-import")]
[TenantScoped]
[Authorize(Policy = "TenantAdminOnly")]
public sealed class CoaImportController : ControllerBase
{
    private const long MaxWorkbookSizeBytes = 10L * 1024 * 1024;
    private const long MaxRequestSizeBytes = MaxWorkbookSizeBytes + (64L * 1024);

    private readonly ICoaExcelImportService _service;
    private readonly ILogger<CoaImportController> _logger;

    public CoaImportController(
        ICoaExcelImportService service,
        ILogger<CoaImportController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates an XLSX workbook without changing tenant data.
    /// </summary>
    [HttpPost("validate")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeBytes)]
    [ProducesResponseType(typeof(ApiResponse<CoaImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CoaImportResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ApiResponse<CoaImportResultDto>>> Validate(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var uploadError = ValidateUpload(file);
        if (uploadError is not null)
        {
            return BadRequest(ApiResponse<CoaImportResultDto>.CreateFailure(uploadError));
        }

        var safeFileName = Path.GetFileName(file!.FileName);
        await using var workbook = file.OpenReadStream();
        var result = await _service.ValidateAsync(workbook, cancellationToken);

        _logger.LogInformation(
            "Validated COA workbook {FileName}: {RowCount} rows and {ErrorCount} errors",
            safeFileName,
            result.TotalRows,
            result.Errors.Count);

        var message = result.IsValid
            ? "Chart-of-accounts workbook is valid"
            : "Chart-of-accounts workbook validation completed with errors";

        return Ok(ApiResponse<CoaImportResultDto>.CreateSuccess(result, message));
    }

    /// <summary>
    /// Imports the approved 315-account workbook into an empty tenant chart of accounts.
    /// </summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeBytes)]
    [ProducesResponseType(typeof(ApiResponse<CoaImportResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CoaImportResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CoaImportResultDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ApiResponse<CoaImportResultDto>>> Import(
        IFormFile? file,
        [FromForm] long defaultBranchId,
        CancellationToken cancellationToken)
    {
        var uploadError = ValidateUpload(file);
        if (uploadError is not null)
        {
            return BadRequest(ApiResponse<CoaImportResultDto>.CreateFailure(uploadError));
        }

        if (defaultBranchId <= 0)
        {
            return BadRequest(ApiResponse<CoaImportResultDto>.CreateFailure(
                "A positive defaultBranchId is required."));
        }

        var safeFileName = Path.GetFileName(file!.FileName);
        await using var workbook = file.OpenReadStream();
        var result = await _service.ImportAsync(
            workbook,
            defaultBranchId,
            cancellationToken);

        if (!result.IsValid)
        {
            var statusCode = result.Errors.Any(error =>
                    string.Equals(
                        error.Code,
                        "COA_ALREADY_INITIALIZED",
                        StringComparison.Ordinal))
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;

            _logger.LogWarning(
                "Rejected COA import for workbook {FileName}, branch {BranchId}: {ErrorCount} errors",
                safeFileName,
                defaultBranchId,
                result.Errors.Count);

            return StatusCode(
                statusCode,
                CreateFailureWithResult(
                    result,
                    "Chart-of-accounts import was rejected",
                    statusCode));
        }

        _logger.LogInformation(
            "Imported {ImportedCount} GL accounts from workbook {FileName} for branch {BranchId}",
            result.ImportedCount,
            safeFileName,
            defaultBranchId);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CoaImportResultDto>.CreateSuccess(
                result,
                "Chart of accounts imported successfully",
                StatusCodes.Status201Created));
    }

    private static string? ValidateUpload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return "A non-empty XLSX workbook is required.";
        }

        if (file.Length > MaxWorkbookSizeBytes)
        {
            return "The workbook cannot exceed 10 MB.";
        }

        var extension = Path.GetExtension(Path.GetFileName(file.FileName));
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "Only .xlsx workbooks are accepted.";
        }

        return null;
    }

    private static ApiResponse<CoaImportResultDto> CreateFailureWithResult(
        CoaImportResultDto result,
        string message,
        int statusCode)
    {
        return new ApiResponse<CoaImportResultDto>
        {
            Success = false,
            StatusCode = statusCode,
            Message = message,
            Data = result,
            Errors = result.Errors.Select(FormatError).ToList(),
            Timestamp = DateTime.UtcNow,
            TraceId = Guid.NewGuid().ToString()
        };
    }

    private static string FormatError(CoaImportErrorDto error)
    {
        var location = error.RowNumber.HasValue
            ? $"Row {error.RowNumber.Value}"
            : "Workbook";

        if (!string.IsNullOrWhiteSpace(error.Column))
        {
            location += $", {error.Column}";
        }

        return $"{location}: {error.Code} - {error.Message}";
    }
}
