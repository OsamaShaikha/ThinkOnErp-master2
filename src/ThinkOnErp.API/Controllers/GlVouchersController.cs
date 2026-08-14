using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Exposes journal entries and voucher management endpoints for the active tenant company.
/// </summary>
[ApiController]
[Route("api/accounting/vouchers")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Accounting)]
[TenantScoped]
[Authorize]
public sealed class GlVouchersController : ControllerBase
{
    private readonly IGlVoucherService _service;
    private readonly ILogger<GlVouchersController> _logger;

    public GlVouchersController(
        IGlVoucherService service,
        ILogger<GlVouchersController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Searches and filters journal entries and vouchers with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<GlVoucherHeaderDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<GlVoucherHeaderDto>>>> GetVouchers(
        [FromQuery] GlVoucherFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPagedVouchersAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResultDto<GlVoucherHeaderDto>>.CreateSuccess(result, "Vouchers retrieved successfully"));
    }

    /// <summary>
    /// Retrieves a single journal entry header and details by ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlVoucherHeaderDto>>> GetVoucherById(
        long id,
        CancellationToken cancellationToken)
    {
        var voucher = await _service.GetByIdAsync(id, cancellationToken);
        if (voucher == null)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Voucher with ID '{id}' was not found.", statusCode: 404));
        }

        return Ok(ApiResponse<GlVoucherHeaderDto>.CreateSuccess(voucher, "Voucher retrieved successfully"));
    }

    /// <summary>
    /// Creates a new journal entry / voucher with automatic serial generation and double-entry balance validation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherHeaderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GlVoucherHeaderDto>>> CreateVoucher(
        [FromBody] CreateGlVoucherDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "API_USER";
        var voucher = await _service.CreateVoucherAsync(dto, username, cancellationToken);

        return CreatedAtAction(
            nameof(GetVoucherById),
            new { id = voucher.Id },
            ApiResponse<GlVoucherHeaderDto>.CreateSuccess(voucher, "Voucher created successfully", 201));
    }

    /// <summary>
    /// Marks a draft journal entry as reviewed.
    /// </summary>
    [HttpPost("{id:long}/review")]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlVoucherHeaderDto>>> ReviewVoucher(
        long id,
        CancellationToken cancellationToken)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "API_USER";
        var voucher = await _service.ReviewVoucherAsync(id, username, cancellationToken);

        return Ok(ApiResponse<GlVoucherHeaderDto>.CreateSuccess(voucher, "Voucher reviewed successfully"));
    }

    /// <summary>
    /// Posts a journal entry to the general ledger.
    /// </summary>
    [HttpPost("{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlVoucherHeaderDto>>> PostVoucher(
        long id,
        CancellationToken cancellationToken)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "API_USER";
        var voucher = await _service.PostVoucherAsync(id, username, cancellationToken);

        return Ok(ApiResponse<GlVoucherHeaderDto>.CreateSuccess(voucher, "Voucher posted successfully"));
    }

    /// <summary>
    /// Reverses a posted journal entry by creating an opposite reversal voucher.
    /// </summary>
    [HttpPost("{id:long}/reverse")]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlVoucherHeaderDto>>> ReverseVoucher(
        long id,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "API_USER";
        var reversalVoucher = await _service.ReverseVoucherAsync(id, username, reason, cancellationToken);

        return Ok(ApiResponse<GlVoucherHeaderDto>.CreateSuccess(reversalVoucher, "Voucher reversed successfully"));
    }
}
