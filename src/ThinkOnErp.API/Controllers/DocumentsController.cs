using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Application.DTOs.Documents;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Application.Features.Documents.Commands.DeleteDocument;
using ThinkOnErp.Application.Features.Documents.Commands.UploadDocument;
using ThinkOnErp.Application.Features.Documents.Commands.UpdateDocument;
using ThinkOnErp.Application.Features.Documents.Queries.GetDocument;
using ThinkOnErp.Application.Features.Documents.Queries.GetDocuments;
using ThinkOnErp.Application.Features.Documents.Queries.DownloadDocument;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IMediator mediator, ILogger<DocumentsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("metadata")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> GetMetadata()
    {
        var metadata = new
        {
            ownerTypes = new[]
            {
                new { value = "Company", label = "Company", hint = "Documents belong to a company" },
                new { value = "Branch", label = "Branch", hint = "Documents belong to a branch" },
                new { value = "SuperAdmin", label = "Super Admin", hint = "Personal documents for super admin" }
            },
            categories = SysDocument.AllowedDocumentCategories.Select(c => new
            {
                value = c,
                label = c
            }),
            allowedExtensions = SysDocument.AllowedFileExtensions,
            maxFileSizeMB = SysDocument.MaxFileSizeBytes / (1024 * 1024)
        };

        return Ok(ApiResponse<object>.CreateSuccess(metadata, "Document metadata retrieved successfully"));
    }

    [HttpPost("upload")]
    [ProducesResponseType(typeof(ApiResponse<DocumentUploadResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<DocumentUploadResult>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DocumentUploadResult>>> Upload(
        [FromForm] string ownerType,
        [FromForm] long ownerId,
        [FromForm] string? description,
        [FromForm] string? category,
        [FromForm] string? tags)
    {
        try
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<DocumentUploadResult>.CreateFailure("No file uploaded."));
            }

            _logger.LogInformation("Uploading document {FileName} for {OwnerType} {OwnerId}",
                file.FileName, ownerType, ownerId);

            var command = new UploadDocumentCommand
            {
                FileStream = file.OpenReadStream(),
                FileName = file.FileName,
                FileSize = file.Length,
                ContentType = file.ContentType,
                Description = description,
                Category = category,
                Tags = tags,
                OwnerType = ownerType,
                OwnerId = ownerId,
                CreationUser = User.Identity?.Name ?? "system"
            };

            var result = await _mediator.Send(command);

            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            throw;
        }
    }

    [HttpPost("upload-bulk")]
    [ProducesResponseType(typeof(ApiResponse<List<DocumentUploadResult>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<List<DocumentUploadResult>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<DocumentUploadResult>>>> UploadBulk(
        [FromForm] string ownerType,
        [FromForm] long ownerId,
        [FromForm] string? description,
        [FromForm] string? category,
        [FromForm] string? tags)
    {
        try
        {
            var files = Request.Form.Files;
            if (files == null || files.Count == 0)
            {
                return BadRequest(ApiResponse<List<DocumentUploadResult>>.CreateFailure("No files uploaded."));
            }

            _logger.LogInformation("Uploading {FileCount} document(s) for {OwnerType} {OwnerId}",
                files.Count, ownerType, ownerId);

            var results = new List<DocumentUploadResult>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var command = new UploadDocumentCommand
                    {
                        FileStream = file.OpenReadStream(),
                        FileName = file.FileName,
                        FileSize = file.Length,
                        ContentType = file.ContentType,
                        Description = description,
                        Category = category,
                        Tags = tags,
                        OwnerType = ownerType,
                        OwnerId = ownerId,
                        CreationUser = User.Identity?.Name ?? "system"
                    };

                    var result = await _mediator.Send(command);

                    if (result.Success)
                    {
                        results.Add(result.Data!);
                    }
                    else
                    {
                        errors.Add($"{file.FileName}: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading file {FileName} in bulk", file.FileName);
                    errors.Add($"{file.FileName}: {ex.Message}");
                }
            }

            if (results.Count == 0)
            {
                return BadRequest(ApiResponse<List<DocumentUploadResult>>.CreateFailure(
                    "All uploads failed.", errors));
            }

            var response = ApiResponse<List<DocumentUploadResult>>.CreateSuccess(
                results,
                $"{results.Count} document(s) uploaded successfully. {errors.Count} failure(s).",
                201);
            response.Errors = errors.Count > 0 ? errors : null;

            return CreatedAtAction(nameof(GetList), null, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk document upload");
            throw;
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentDto>>>> GetList(
        [FromQuery] GetDocumentsQuery query)
    {
        try
        {
            var result = await _mediator.Send(query);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching documents");
            throw;
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> GetById(long id)
    {
        try
        {
            var result = await _mediator.Send(new GetDocumentQuery { Id = id });

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching document {DocumentId}", id);
            throw;
        }
    }

    [HttpGet("{id}/download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(long id)
    {
        try
        {
            _logger.LogInformation("Downloading document {DocumentId}", id);

            var result = await _mediator.Send(new DownloadDocumentQuery { Id = id });

            return File(result.FileStream, result.MimeType, result.FileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error downloading document: {ErrorMessage}", ex.Message);
            return NotFound(ApiResponse<object>.CreateFailure(ex.Message, statusCode: 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId}", id);
            throw;
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> Update(long id,
        [FromBody] UpdateDocumentCommand command)
    {
        try
        {
            if (id != command.Id)
            {
                return BadRequest(ApiResponse<DocumentDto>.CreateFailure(
                    "Document ID in URL does not match the ID in the request body"));
            }

            command.UpdateUser = User.Identity?.Name ?? "system";

            var result = await _mediator.Send(command);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document {DocumentId}", id);
            throw;
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(long id)
    {
        try
        {
            var result = await _mediator.Send(new SoftDeleteDocumentCommand
            {
                Ids = new[] { id },
                UpdateUser = User.Identity?.Name ?? "system"
            });

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", id);
            throw;
        }
    }

    [HttpPost("bulk-delete")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> BulkDelete(
        [FromBody] SoftDeleteDocumentCommand command)
    {
        try
        {
            command.UpdateUser = User.Identity?.Name ?? "system";

            var result = await _mediator.Send(command);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk-deleting documents");
            throw;
        }
    }

    [HttpGet("company/{companyId}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentDto>>>> GetByCompany(
        long companyId, [FromQuery] GetDocumentsQuery query)
    {
        query.OwnerType = "Company";
        query.OwnerId = companyId;
        return await GetList(query);
    }

    [HttpGet("branch/{branchId}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentDto>>>> GetByBranch(
        long branchId, [FromQuery] GetDocumentsQuery query)
    {
        query.OwnerType = "Branch";
        query.OwnerId = branchId;
        return await GetList(query);
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentDto>>>> GetMy(
        [FromQuery] GetDocumentsQuery query)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        query.OwnerType = "SuperAdmin";
        query.OwnerId = long.TryParse(userId, out var id) ? id : 0;
        return await GetList(query);
    }
}
