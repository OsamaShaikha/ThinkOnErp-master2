using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Universal Trade and Movement Documents API: Manages all sales invoices, purchase bills, returns, goods receipts, goods issues, transfers, quotations, and purchase orders.
/// </summary>
[ApiController]
[Route("api/documents")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class TrxDocumentsController : ControllerBase
{
    private readonly ITrxDocumentService _docService;

    public TrxDocumentsController(ITrxDocumentService docService)
    {
        _docService = docService;
    }

    /// <summary>
    /// Creates a new trade or inventory movement document.
    /// </summary>
    /// <param name="dto">The document header and lines creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created document with generated ID and computed totals.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CreateDocument(
        [FromBody] CreateTrxDocumentDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TrxDocumentDto>.CreateFailure("Validation failed", null));

        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _docService.CreateDocumentAsync(dto, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.DocumentCreated;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves a single document by its composite primary key.
    /// </summary>
    /// <param name="branchId">Branch identifier.</param>
    /// <param name="docYear">Fiscal document year.</param>
    /// <param name="docType">Document type code.</param>
    /// <param name="id">Document sequence identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching document details.</returns>
    [HttpGet("{branchId:long}/{docYear:int}/{docType:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> GetDocumentByKey(
        long branchId,
        int docYear,
        int docType,
        long id,
        CancellationToken ct)
    {
        var result = await _docService.GetDocumentByKeyAsync(branchId, docYear, docType, id, ct);
        if (result.Success)
            result.Message = ResponseCodes.DocumentDetailsRetrieved;

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Searches and filters trade and movement documents with pagination.
    /// </summary>
    /// <param name="filter">Filtering and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of matching documents and total record count.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<(List<TrxDocumentDto> Items, int TotalCount)>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<(List<TrxDocumentDto> Items, int TotalCount)>>> GetDocuments(
        [FromQuery] TrxDocumentFilterDto filter,
        CancellationToken ct)
    {
        var result = await _docService.GetDocumentsPagedAsync(filter, ct);
        if (result.Success)
            result.Message = ResponseCodes.DocumentsRetrieved;

        return Ok(result);
    }

    /// <summary>
    /// Updates an existing draft document.
    /// </summary>
    /// <param name="branchId">Branch identifier.</param>
    /// <param name="docYear">Fiscal document year.</param>
    /// <param name="docType">Document type code.</param>
    /// <param name="id">Document sequence identifier.</param>
    /// <param name="dto">Update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated document.</returns>
    [HttpPut("{branchId:long}/{docYear:int}/{docType:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> UpdateDocument(
        long branchId,
        int docYear,
        int docType,
        long id,
        [FromBody] UpdateTrxDocumentDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _docService.UpdateDocumentAsync(branchId, docYear, docType, id, dto, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.DocumentUpdated;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Deletes or cancels a draft document.
    /// </summary>
    /// <param name="branchId">Branch identifier.</param>
    /// <param name="docYear">Fiscal document year.</param>
    /// <param name="docType">Document type code.</param>
    /// <param name="id">Document sequence identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("{branchId:long}/{docYear:int}/{docType:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteDocument(
        long branchId,
        int docYear,
        int docType,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _docService.DeleteDocumentAsync(branchId, docYear, docType, id, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.TrxDocumentDeleted;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Posts and finalizes a document to General Ledger (GL) and Inventory Stock Ledger.
    /// </summary>
    /// <param name="branchId">Branch identifier.</param>
    /// <param name="docYear">Fiscal document year.</param>
    /// <param name="docType">Document type code.</param>
    /// <param name="id">Document sequence identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The posted document details.</returns>
    [HttpPost("{branchId:long}/{docYear:int}/{docType:int}/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> PostDocument(
        long branchId,
        int docYear,
        int docType,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _docService.PostDocumentAsync(branchId, docYear, docType, id, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.DocumentPosted;

        return result.Success ? Ok(result) : BadRequest(result);
    }
}
