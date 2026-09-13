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
/// Point of Sale Gift Cards &amp; Store Credit API: Issue, query balance, and redeem gift cards/vouchers.
/// </summary>
[ApiController]
[Route("api/pos/gift-cards")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosGiftCardsController : ControllerBase
{
    private readonly IPosGiftCardService _giftCardService;

    public PosGiftCardsController(IPosGiftCardService giftCardService)
    {
        _giftCardService = giftCardService;
    }

    /// <summary>
    /// Retrieves a paginated list of gift cards with filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<GiftCardSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<GiftCardSummaryDto>>>> GetGiftCards(
        [FromQuery] long branchId,
        [FromQuery] string? cardCode,
        [FromQuery] bool? isActive,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _giftCardService.GetGiftCardsPagedAsync(branchId, cardCode, isActive, pageIndex, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves detailed gift card information including transaction history by ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<GiftCardDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GiftCardDetailsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GiftCardDetailsDto>>> GetGiftCardById(
        long id,
        CancellationToken ct)
    {
        var result = await _giftCardService.GetGiftCardByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Issues a new gift card with an initial stored value balance.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<GiftCardSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GiftCardSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GiftCardSummaryDto>>> CreateGiftCard(
        [FromBody] CreateGiftCardDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _giftCardService.CreateGiftCardAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates gift card details (status, expiration date).
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<GiftCardSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GiftCardSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GiftCardSummaryDto>>> UpdateGiftCard(
        long id,
        [FromBody] UpdateGiftCardDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _giftCardService.UpdateGiftCardAsync(id, dto, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Deactivates / soft-deletes a gift card.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteGiftCard(
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _giftCardService.DeleteGiftCardAsync(id, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Checks the current active balance and validity of a gift card by its code.
    /// </summary>
    [HttpGet("balance")]
    [ProducesResponseType(typeof(ApiResponse<GiftCardSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GiftCardSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GiftCardSummaryDto>>> GetCardBalance(
        [FromQuery] long branchId,
        [FromQuery] string cardCode,
        CancellationToken ct)
    {
        var result = await _giftCardService.GetCardBalanceAsync(branchId, cardCode, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Redeems a specified amount from a gift card against a POS order payment.
    /// </summary>
    [HttpPost("redeem")]
    [ProducesResponseType(typeof(ApiResponse<GiftCardSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GiftCardSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GiftCardSummaryDto>>> RedeemCard(
        [FromBody] RedeemGiftCardDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _giftCardService.RedeemCardAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
