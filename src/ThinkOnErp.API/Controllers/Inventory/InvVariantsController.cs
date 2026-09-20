using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Variants;
using ThinkOnErp.Application.Services.Inventory;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Product Variants &amp; Attributes API: Manages attributes (Size, Color, etc.), item variant matrices, SKU generation, and variant pricing.
/// </summary>
[ApiController]
[Route("api/inventory")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public class InvVariantsController : ControllerBase
{
    private readonly IInvVariantService _variantService;

    public InvVariantsController(IInvVariantService variantService)
    {
        _variantService = variantService;
    }

    /// <summary>
    /// Retrieves all product attribute definitions and their option values (e.g. Size, Color).
    /// </summary>
    /// <param name="activeOnly">Filter by active attributes only (default true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of attributes with their values.</returns>
    [HttpGet("attributes")]
    [ProducesResponseType(typeof(ApiResponse<List<InvAttributeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttributes([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var response = await _variantService.GetAttributesAsync(activeOnly, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Creates a new product attribute with predefined option values.
    /// </summary>
    /// <param name="request">Attribute creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created attribute details.</returns>
    [HttpPost("attributes")]
    [ProducesResponseType(typeof(ApiResponse<InvAttributeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvAttributeDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAttribute([FromBody] CreateAttributeDto request, CancellationToken cancellationToken)
    {
        var response = await _variantService.CreateAttributeAsync(request, cancellationToken);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a paginated list of all product variants across the system, searchable by SKU, barcode, or name.
    /// </summary>
    /// <param name="pageNumber">Page index (default 1).</param>
    /// <param name="pageSize">Page size (default 20).</param>
    /// <param name="itemId">Optional filter by parent item.</param>
    /// <param name="search">Search keyword (SKU, barcode, name).</param>
    /// <param name="activeOnly">Filter by active status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated variants list.</returns>
    [HttpGet("variants")]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<InvVariantDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVariants(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? itemId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _variantService.GetVariantsPagedAsync(pageNumber, pageSize, itemId, search, activeOnly, cancellationToken);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a specific product variant by identifier.
    /// </summary>
    /// <param name="id">Variant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Variant details.</returns>
    [HttpGet("variants/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvVariantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvVariantDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVariantById(long id, CancellationToken cancellationToken)
    {
        var response = await _variantService.GetVariantByIdAsync(id, cancellationToken);
        if (!response.Success)
            return NotFound(response);

        return Ok(response);
    }

    /// <summary>
    /// Retrieves all variants belonging to a specific parent item.
    /// </summary>
    /// <param name="itemId">Parent item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of item variants.</returns>
    [HttpGet("items/{itemId:long}/variants")]
    [ProducesResponseType(typeof(ApiResponse<List<InvVariantDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItemVariants(long itemId, CancellationToken cancellationToken)
    {
        var response = await _variantService.GetVariantsByItemIdAsync(itemId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Manually registers a specific variant for a parent item with custom SKU, price, and attributes.
    /// </summary>
    /// <param name="itemId">Parent item identifier.</param>
    /// <param name="request">Variant details payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created variant details.</returns>
    [HttpPost("items/{itemId:long}/variants/manual")]
    [ProducesResponseType(typeof(ApiResponse<InvVariantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvVariantDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateVariantManual(long itemId, [FromBody] CreateVariantManualDto request, CancellationToken cancellationToken)
    {
        request.ItemId = itemId;
        var response = await _variantService.CreateVariantManualAsync(request, cancellationToken);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    /// <summary>
    /// Automatically generates the Cartesian combination matrix of variants from selected attribute values and assigns unique SKUs.
    /// </summary>
    /// <param name="itemId">Parent item identifier.</param>
    /// <param name="request">Selected attribute values and pricing defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all generated variants.</returns>
    [HttpPost("items/{itemId:long}/variants/generate")]
    [ProducesResponseType(typeof(ApiResponse<List<InvVariantDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<InvVariantDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateVariantsMatrix(long itemId, [FromBody] GenerateVariantsMatrixRequestDto request, CancellationToken cancellationToken)
    {
        request.ItemId = itemId;
        var response = await _variantService.GenerateVariantsMatrixAsync(request, cancellationToken);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    /// <summary>
    /// Updates details of an existing variant (SKU, name, barcode, price, active state).
    /// </summary>
    /// <param name="id">Variant identifier.</param>
    /// <param name="request">Update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated variant details.</returns>
    [HttpPut("variants/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvVariantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvVariantDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateVariant(long id, [FromBody] UpdateVariantDto request, CancellationToken cancellationToken)
    {
        var response = await _variantService.UpdateVariantAsync(id, request, cancellationToken);
        if (!response.Success)
            return NotFound(response);

        return Ok(response);
    }

    /// <summary>
    /// Deletes a variant.
    /// </summary>
    /// <param name="id">Variant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation response.</returns>
    [HttpDelete("variants/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVariant(long id, CancellationToken cancellationToken)
    {
        var response = await _variantService.DeleteVariantAsync(id, cancellationToken);
        if (!response.Success)
            return NotFound(response);

        return Ok(response);
    }
}
