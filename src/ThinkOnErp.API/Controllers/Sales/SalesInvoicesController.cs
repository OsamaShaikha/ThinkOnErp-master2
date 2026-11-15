using System;
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
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers.Sales;

/// <summary>
/// Sales Invoices API (DocType 100): Manages cash, credit, and export sales invoices.
/// </summary>
[ApiController]
[Route("api/sales/invoices")]
[ApiExplorerSettings(GroupName = ApiCategories.Sales)]
[TenantScoped]
[Authorize]
public sealed class SalesInvoicesController : ControllerBase
{
    private readonly ISalesDocumentService _salesService;

    public SalesInvoicesController(ISalesDocumentService salesService)
    {
        _salesService = salesService;
    }

    /// <summary>
    /// Searches and filters sales invoices with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<TrxDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<TrxDocumentDto>>>> GetInvoices(
        [FromQuery] TrxDocumentFilterDto filter,
        CancellationToken ct)
    {
        var result = await _salesService.GetInvoicesPagedAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single sales invoice by branch, document year, and document ID.
    /// </summary>
    [HttpGet("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> GetInvoiceByKey(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var result = await _salesService.GetInvoiceByKeyAsync(branchId, docYear, id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Creates a new sales invoice as Draft (DocType 100).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CreateInvoice(
        [FromBody] CreateSalesInvoiceDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TrxDocumentDto>.CreateFailure("Validation failed", null));

        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _salesService.CreateInvoiceAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Updates an existing draft sales invoice.
    /// </summary>
    [HttpPut("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> UpdateInvoice(
        long branchId,
        int docYear,
        long id,
        [FromBody] UpdateSalesInvoiceDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _salesService.UpdateInvoiceAsync(branchId, docYear, id, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Posts a sales invoice to General Ledger (GL) and Inventory Stock Ledger.
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> PostInvoice(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _salesService.PostInvoiceAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Cancels / voids a sales invoice (reverses GL entry and stock movements).
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CancelInvoice(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _salesService.CancelInvoiceAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }
}
