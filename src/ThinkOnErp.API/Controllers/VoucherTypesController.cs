using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Exceptions;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Exposes voucher and journal entry type definitions for the active tenant company.
/// </summary>
[ApiController]
[Route("api/accounting/voucher-types")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Accounting)]
[TenantScoped]
[Authorize]
public sealed class VoucherTypesController : ControllerBase
{
    private readonly IGlVoucherService _service;
    private readonly ILogger<VoucherTypesController> _logger;

    public VoucherTypesController(
        IGlVoucherService service,
        ILogger<VoucherTypesController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active voucher types defined for accounting entries.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlVoucherTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GlVoucherTypeDto>>>> GetVoucherTypes(
        CancellationToken cancellationToken)
    {
        var types = await _service.GetVoucherTypesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GlVoucherTypeDto>>.CreateSuccess(types, "Voucher types retrieved successfully"));
    }

    /// <summary>
    /// Retrieves a single voucher type definition by its numeric code.
    /// </summary>
    [HttpGet("{typeCode:int}")]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlVoucherTypeDto>>> GetVoucherTypeByCode(
        int typeCode,
        CancellationToken cancellationToken)
    {
        var type = await _service.GetVoucherTypeByCodeAsync(typeCode, cancellationToken);
        if (type == null)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Voucher type with code '{typeCode}' was not found.", statusCode: 404));
        }

        return Ok(ApiResponse<GlVoucherTypeDto>.CreateSuccess(type, "Voucher type retrieved successfully"));
    }

    /// <summary>
    /// Retrieves a single voucher type definition by its unique identifier.
    /// </summary>
    [HttpGet("id/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlVoucherTypeDto>>> GetVoucherTypeById(
        long id,
        CancellationToken cancellationToken)
    {
        var type = await _service.GetVoucherTypeByIdAsync(id, cancellationToken);
        if (type == null)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Voucher type with ID '{id}' was not found.", statusCode: 404));
        }

        return Ok(ApiResponse<GlVoucherTypeDto>.CreateSuccess(type, "Voucher type retrieved successfully"));
    }

    /// <summary>
    /// Creates a new custom voucher type definition for the active tenant company.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherTypeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GlVoucherTypeDto>>> CreateVoucherType(
        [FromBody] CreateGlVoucherTypeDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var username = User.Identity?.Name ?? "system";
            var created = await _service.CreateVoucherTypeAsync(dto, username, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<GlVoucherTypeDto>.CreateSuccess(created, "Voucher type created successfully", StatusCodes.Status201Created));
        }
        catch (AccountingException ex)
        {
            return BadRequest(ApiResponse<object>.CreateFailure(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
    }

    /// <summary>
    /// Updates an existing voucher type definition for the active tenant company.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlVoucherTypeDto>>> UpdateVoucherType(
        long id,
        [FromBody] UpdateGlVoucherTypeDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var username = User.Identity?.Name ?? "system";
            var updated = await _service.UpdateVoucherTypeAsync(id, dto, username, cancellationToken);
            return Ok(ApiResponse<GlVoucherTypeDto>.CreateSuccess(updated, "Voucher type updated successfully"));
        }
        catch (AccountingException ex)
        {
            return BadRequest(ApiResponse<object>.CreateFailure(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
    }

    /// <summary>
    /// Deletes a custom voucher type definition. System voucher types or types with recorded vouchers cannot be deleted.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteVoucherType(
        long id,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteVoucherTypeAsync(id, cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(new { Id = id }, "Voucher type deleted successfully"));
        }
        catch (AccountingException ex)
        {
            return BadRequest(ApiResponse<object>.CreateFailure(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
    }
}
