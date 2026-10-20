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
/// Point of Sale Payment Vouchers API (سندات الصرف): Handles mid-shift petty cash and card expense payouts, vendor payments, refundable deposits, and automated GL posting.
/// </summary>
[ApiController]
[Route("api/pos/payments")]
[Route("api/pos/vouchers/payments")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosPaymentsController : ControllerBase
{
    private readonly IPosShiftService _shiftService;

    public PosPaymentsController(IPosShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    /// <summary>
    /// Creates a new Payment Voucher (سند صرف) from the active POS shift without requiring GL accounts. Uses the default expense account and tender PaymentMethod.
    /// </summary>
    /// <param name="dto">Payment voucher details including ShiftId, Amount, PaymentMethod (determines drawer side), VendorCode (optional), BeneficiaryName, TaxInvoiceNumber, and Reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary of the created payment voucher and its assigned serial number.</returns>
    [HttpPost]
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
    /// Retrieves all Payment Vouchers (سندات الصرف) recorded for a given POS shift session with optional payment method filter.
    /// </summary>
    /// <param name="shiftId">ID of the cashier shift.</param>
    /// <param name="paymentMethod">Optional filter: 1 = Cash, 2 = Card, 5 = Cheque, 6 = Bank Transfer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of payment vouchers matching the criteria.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>>> GetShiftPayments(
        [FromQuery] long shiftId,
        [FromQuery] int? paymentMethod = null,
        CancellationToken ct = default)
    {
        var result = await _shiftService.GetShiftPaymentsAsync(shiftId, paymentMethod, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a lookup list of common GL expense and payable accounts commonly used for POS payment / petty cash vouchers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of account codes and their Arabic/English names categorized for POS payment vouchers.</returns>
    [HttpGet("accounts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosExpenseAccountLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosExpenseAccountLookupDto>>>> GetPaymentAccountsLookup(
        CancellationToken ct = default)
    {
        var result = await _shiftService.GetExpenseAccountsLookupAsync(ct);
        return Ok(result);
    }
}
