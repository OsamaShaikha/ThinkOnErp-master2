using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;

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
}
