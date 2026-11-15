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
using ThinkOnErp.Application.DTOs.Purchasing;
using ThinkOnErp.Application.Services.Purchasing;

namespace ThinkOnErp.API.Controllers.Purchasing;

/// <summary>
/// Purchase Orders API (DocType 450): Manages procurement orders sent to vendors.
/// </summary>
[ApiController]
[Route("api/purchasing/orders")]
[ApiExplorerSettings(GroupName = ApiCategories.Purchasing)]
[TenantScoped]
[Authorize]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchasingDocumentService _purchasingService;

    public PurchaseOrdersController(IPurchasingDocumentService purchasingService)
    {
        _purchasingService = purchasingService;
    }

    /// <summary>
    /// Searches and filters purchase orders with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<TrxDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<TrxDocumentDto>>>> GetOrders(
        [FromQuery] TrxDocumentFilterDto filter,
        CancellationToken ct)
    {
        var result = await _purchasingService.GetOrdersPagedAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single purchase order by branch, document year, and document ID.
    /// </summary>
    [HttpGet("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> GetOrderByKey(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var result = await _purchasingService.GetOrderByKeyAsync(branchId, docYear, id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Creates a new purchase order as Draft (DocType 450).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CreateOrder(
        [FromBody] CreatePurchaseOrderDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TrxDocumentDto>.CreateFailure("Validation failed", null));

        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _purchasingService.CreateOrderAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Updates an existing draft purchase order.
    /// </summary>
    [HttpPut("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> UpdateOrder(
        long branchId,
        int docYear,
        long id,
        [FromBody] UpdatePurchaseOrderDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _purchasingService.UpdateOrderAsync(branchId, docYear, id, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Approves / finalizes a purchase order.
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> PostOrder(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _purchasingService.PostOrderAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Cancels a purchase order.
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CancelOrder(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _purchasingService.CancelOrderAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }
}
