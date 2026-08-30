using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/receipts")]
[TenantScoped]
[Authorize]
public class ReceiptsController : ControllerBase
{
    private readonly IReceiptPaymentVoucherService _voucherService;
    private readonly ILogger<ReceiptsController> _logger;

    public ReceiptsController(IReceiptPaymentVoucherService voucherService, ILogger<ReceiptsController> logger)
    {
        _voucherService = voucherService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReceiptVoucherDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ReceiptVoucherDto>>>> GetReceipts(
        [FromQuery] long? branchId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? customerCode,
        CancellationToken cancellationToken)
    {
        var receipts = await _voucherService.GetReceiptVouchersAsync(branchId, fromDate, toDate, customerCode, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReceiptVoucherDto>>.CreateSuccess(
            receipts,
            ResponseCodes.ReceiptVouchersRetrieved,
            200));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptVoucherDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReceiptVoucherDto>>> GetReceiptById(
        long id,
        CancellationToken cancellationToken)
    {
        var receipt = await _voucherService.GetReceiptVoucherByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ReceiptVoucherDto>.CreateSuccess(
            receipt,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptVoucherDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ReceiptVoucherDto>>> CreateReceipt(
        [FromBody] CreateReceiptVoucherDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var receipt = await _voucherService.CreateReceiptVoucherAsync(dto, username, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ReceiptVoucherDto>.CreateSuccess(
            receipt,
            ResponseCodes.ReceiptVoucherCreated,
            201));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptVoucherDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReceiptVoucherDto>>> UpdateReceipt(
        long id,
        [FromBody] UpdateReceiptVoucherDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var receipt = await _voucherService.UpdateReceiptVoucherAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<ReceiptVoucherDto>.CreateSuccess(
            receipt,
            ResponseCodes.ReceiptVoucherUpdated,
            200));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteReceipt(
        long id,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _voucherService.DeleteReceiptVoucherAsync(id, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            ResponseCodes.ReceiptVoucherDeleted,
            200));
    }
}
