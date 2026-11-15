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
using ThinkOnErp.Application.DTOs.Sales;
using ThinkOnErp.Application.Services.Sales;

namespace ThinkOnErp.API.Controllers.Sales;

/// <summary>
/// Sales Quotations API (DocType 400): Manages sales quotations for customers.
/// </summary>
[ApiController]
[Route("api/sales/quotations")]
[ApiExplorerSettings(GroupName = ApiCategories.Sales)]
[TenantScoped]
[Authorize]
public sealed class SalesQuotationsController : ControllerBase
{
    private readonly ISalesDocumentService _salesService;

    public SalesQuotationsController(ISalesDocumentService salesService)
    {
        _salesService = salesService;
    }

    /// <summary>
    /// Searches and filters sales quotations with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<TrxDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<TrxDocumentDto>>>> GetQuotations(
        [FromQuery] TrxDocumentFilterDto filter,
        CancellationToken ct)
    {
        var result = await _salesService.GetQuotationsPagedAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single sales quotation by branch, document year, and document ID.
    /// </summary>
    [HttpGet("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> GetQuotationByKey(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var result = await _salesService.GetQuotationByKeyAsync(branchId, docYear, id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Creates a new sales quotation as Draft (DocType 400).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CreateQuotation(
        [FromBody] CreateSalesQuotationDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TrxDocumentDto>.CreateFailure("Validation failed", null));

        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _salesService.CreateQuotationAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Updates an existing draft sales quotation.
    /// </summary>
    [HttpPut("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> UpdateQuotation(
        long branchId,
        int docYear,
        long id,
        [FromBody] UpdateSalesQuotationDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _salesService.UpdateQuotationAsync(branchId, docYear, id, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Approves / finalizes a sales quotation.
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> PostQuotation(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _salesService.PostQuotationAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Cancels a sales quotation.
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CancelQuotation(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _salesService.CancelQuotationAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }
}
