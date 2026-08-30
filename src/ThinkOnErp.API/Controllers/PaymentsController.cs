using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/payments")]
[TenantScoped]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IReceiptPaymentVoucherService _voucherService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IReceiptPaymentVoucherService voucherService, ILogger<PaymentsController> logger)
    {
        _voucherService = voucherService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PaymentVoucherDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PaymentVoucherDto>>>> GetPayments(
        [FromQuery] long? branchId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? vendorCode,
        CancellationToken cancellationToken)
    {
        var payments = await _voucherService.GetPaymentVouchersAsync(branchId, fromDate, toDate, vendorCode, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PaymentVoucherDto>>.CreateSuccess(
            payments,
            ResponseCodes.PaymentVouchersRetrieved,
            200));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentVoucherDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDto>>> GetPaymentById(
        long id,
        CancellationToken cancellationToken)
    {
        var payment = await _voucherService.GetPaymentVoucherByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<PaymentVoucherDto>.CreateSuccess(
            payment,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PaymentVoucherDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDto>>> CreatePayment(
        [FromBody] CreatePaymentVoucherDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var payment = await _voucherService.CreatePaymentVoucherAsync(dto, username, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PaymentVoucherDto>.CreateSuccess(
            payment,
            ResponseCodes.PaymentVoucherCreated,
            201));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PaymentVoucherDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDto>>> UpdatePayment(
        long id,
        [FromBody] UpdatePaymentVoucherDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var payment = await _voucherService.UpdatePaymentVoucherAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<PaymentVoucherDto>.CreateSuccess(
            payment,
            ResponseCodes.PaymentVoucherUpdated,
            200));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> DeletePayment(
        long id,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _voucherService.DeletePaymentVoucherAsync(id, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            ResponseCodes.PaymentVoucherDeleted,
            200));
    }
}
