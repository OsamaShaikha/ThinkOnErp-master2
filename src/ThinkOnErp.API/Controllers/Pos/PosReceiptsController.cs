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
/// Point of Sale Receipt Vouchers API (سندات القبض): Handles mid-shift cash and non-cash receipt vouchers, customer settlements, float additions, and automated GL posting.
/// </summary>
[ApiController]
[Route("api/pos/receipts")]
[Route("api/pos/vouchers/receipts")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosReceiptsController : ControllerBase
{
    private readonly IPosShiftService _shiftService;

    public PosReceiptsController(IPosShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    /// <summary>
    /// Creates a new Receipt Voucher (سند قبض) for an active POS shift without requiring GL accounts. Uses the default receipt account and tender PaymentMethod.
    /// </summary>
    /// <param name="dto">Receipt voucher details including ShiftId, Amount, PaymentMethod (determines drawer side), CustomerCode (optional), PayerName, ReferenceNumber, and Reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary of the created receipt voucher and its assigned serial number.</returns>
    [HttpPost]
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
    /// Retrieves all Receipt Vouchers (سندات القبض) recorded for a given POS shift session with optional payment method filter.
    /// </summary>
    /// <param name="shiftId">ID of the cashier shift.</param>
    /// <param name="paymentMethod">Optional filter: 1 = Cash, 2 = Card, 5 = Cheque, 6 = Bank Transfer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of receipt vouchers matching the criteria.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>>> GetShiftReceipts(
        [FromQuery] long shiftId,
        [FromQuery] int? paymentMethod = null,
        CancellationToken ct = default)
    {
        var result = await _shiftService.GetShiftReceiptsAsync(shiftId, paymentMethod, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a lookup list of GL revenue, debtor, deposit, and asset accounts commonly used for POS receipt vouchers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of account codes and their Arabic/English names categorized for POS receipt vouchers.</returns>
    [HttpGet("accounts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosExpenseAccountLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosExpenseAccountLookupDto>>>> GetReceiptAccountsLookup(
        CancellationToken ct = default)
    {
        var result = await _shiftService.GetReceiptAccountsLookupAsync(ct);
        return Ok(result);
    }
}
