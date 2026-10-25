using System;
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
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Vouchers API: Handles mid-shift Receipt and Payment Vouchers (سندات القبض والصرف) with multi-tender support (Cash, Card, Bank, Cheque) and automatic shift reconciliation and GL posting.
/// </summary>
[ApiController]
[Route("api/pos/vouchers")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosVouchersController : ControllerBase
{
    private readonly IPosShiftService _shiftService;

    public PosVouchersController(IPosShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    /// <summary>
    /// Creates a new Receipt Voucher (سند قبض) for an active POS shift without requiring GL accounts. Uses the default receipt account and tender PaymentMethod.
    /// </summary>
    /// <param name="dto">Receipt details including ShiftId, Amount, PaymentMethod (determines drawer side), CustomerCode (optional), PayerName, ReferenceNumber, and Reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary of the created receipt voucher and its assigned serial number.</returns>
    [HttpPost("receipt")]
    [ProducesResponseType(typeof(ApiResponse<PosVoucherSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosVoucherSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosVoucherSummaryDto>>> CreateReceipt(
        [FromBody] CreatePosReceiptVoucherDto dto,
        CancellationToken ct = default)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _shiftService.CreateReceiptVoucherAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Creates a new Payment Voucher (سند صرف) for an active POS shift without requiring GL accounts. Uses the default expense account and tender PaymentMethod.
    /// </summary>
    /// <param name="dto">Payment details including ShiftId, Amount, PaymentMethod (determines drawer side), VendorCode (optional), BeneficiaryName, TaxInvoiceNumber, and Reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary of the created payment voucher and its assigned serial number.</returns>
    [HttpPost("payment")]
    [ProducesResponseType(typeof(ApiResponse<PosVoucherSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosVoucherSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosVoucherSummaryDto>>> CreatePayment(
        [FromBody] CreatePosPaymentVoucherDto dto,
        CancellationToken ct = default)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _shiftService.CreatePaymentVoucherAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves all vouchers recorded for a given POS shift session with optional filters for voucher type and payment method.
    /// </summary>
    /// <param name="shiftId">ID of the cashier shift.</param>
    /// <param name="voucherType">Optional filter: 1 = Receipt (قبض), 2 = Payment (صرف).</param>
    /// <param name="paymentMethod">Optional filter: 1 = Cash, 2 = Card, 5 = Cheque, 6 = Bank Transfer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of vouchers matching the criteria.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>>> GetShiftVouchers(
        [FromQuery] long shiftId,
        [FromQuery] int? voucherType = null,
        [FromQuery] int? paymentMethod = null,
        CancellationToken ct = default)
    {
        var result = await _shiftService.GetShiftVouchersAsync(shiftId, voucherType, paymentMethod, ct);
        return Ok(result);
    }
}
