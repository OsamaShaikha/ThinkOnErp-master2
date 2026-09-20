using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Features.Pos.Orders.Commands.CreateOrder;
using ThinkOnErp.Application.Features.Pos.Orders.Commands.ParkOrder;
using ThinkOnErp.Application.Features.Pos.Orders.Commands.ProcessPayment;
using ThinkOnErp.Application.Features.Pos.Orders.Commands.RefundOrder;
using ThinkOnErp.Application.Features.Pos.Orders.Commands.VoidOrderLine;
using ThinkOnErp.Application.Features.Pos.Orders.Queries.GetActiveOrders;
using ThinkOnErp.Application.Features.Pos.Orders.Queries.GetOrderById;
using ThinkOnErp.Application.DTOs.SysCode;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Universal POS Orders &amp; Sales API: Manages order lifecycle, paged history, split payments, line voids, and returns.
/// </summary>
[ApiController]
[Route("api/pos/orders")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosOrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPosOrderService _orderService;
    private readonly ISysCodeRepository _sysCodeRepo;

    public PosOrdersController(IMediator mediator, IPosOrderService orderService, ISysCodeRepository sysCodeRepo)
    {
        _mediator = mediator;
        _orderService = orderService;
        _sysCodeRepo = sysCodeRepo;
    }

    /// <summary>
    /// Retrieves a paginated list of POS orders with date, shift, status, and channel filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<PosOrderSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<PosOrderSummaryDto>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<PosOrderSummaryDto>>>> GetOrders(
        [FromQuery] long branchId,
        [FromQuery] long? shiftId,
        [FromQuery] PosOrderStatus? status,
        [FromQuery] PosOrderType? orderType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (pageIndex <= 0 || pageSize <= 0)
        {
            return BadRequest(ApiResponse<PagedResultDto<PosOrderSummaryDto>>.CreateFailure("Invalid pagination parameters. pageIndex and pageSize must be greater than zero.", null, 400));
        }

        var result = await _orderService.GetOrdersPagedAsync(branchId, shiftId, status, orderType, fromDate, toDate, pageIndex, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new POS order or invoice with automatic calculation of promotions, taxes, and modifiers.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> CreateOrder(
        [FromBody] CreatePosOrderDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new CreateOrderCommand(dto, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates an open or parked order's customer, table assignment, or manual discount.
    /// </summary>
    [HttpPut("{orderId:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> UpdateOrder(
        long orderId,
        [FromBody] UpdatePosOrderDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _orderService.UpdateOrderAsync(orderId, dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Cancels or deletes a draft/parked order that has no processed payments.
    /// </summary>
    [HttpDelete("{orderId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteOrder(
        long orderId,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _orderService.DeleteOrderAsync(orderId, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Adds tender payments to an open or parked order (supports split payments).
    /// </summary>
    [HttpPost("{orderId:long}/payments")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> ProcessPayment(
        long orderId,
        [FromBody] List<CreatePosOrderPaymentDto> payments,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new ProcessPaymentCommand(orderId, payments, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Parks an active order to free the cashier terminal for subsequent customers.
    /// </summary>
    [HttpPost("{orderId:long}/park")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> ParkOrder(
        long orderId,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new ParkOrderCommand(orderId, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Issues a full or partial refund for a previously completed order and reverses inventory/commissions.
    /// </summary>
    [HttpPost("refund")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> RefundOrder(
        [FromBody] RefundOrderDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new RefundOrderCommand(dto, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Voids a specific line from an open order with mandatory supervisor approval tracking.
    /// </summary>
    [HttpPost("void-line")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> VoidOrderLine(
        [FromBody] VoidLineDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new VoidOrderLineCommand(dto, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves a single POS order by its ID.
    /// </summary>
    [HttpGet("{orderId:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> GetOrderById(
        long orderId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderByIdQuery(orderId), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Retrieves all active and parked orders for a specific branch.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosOrderSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosOrderSummaryDto>>>> GetActiveOrders(
        [FromQuery] long branchId,
        [FromQuery] PosOrderStatus? status,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetActiveOrdersQuery(branchId, status), ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all dynamic POS Order Types from SYS_CODE (CODE_MGR = 34).
    /// </summary>
    /// <param name="lang">Optional language filter (1 = Arabic, 2 = English).</param>
    /// <returns>List of order types with localized names and active state.</returns>
    [HttpGet("orderType")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetOrderTypes([FromQuery] int? lang = null)
    {
        var rawCodes = await _sysCodeRepo.GetActiveByCodeMgrAsync(SysCodeKeys.PosOrderTypes.Mgr);

        var lookupList = rawCodes
            .Where(c => c.CodeMnr > 0)
            .GroupBy(c => c.CodeMnr)
            .Select(g =>
            {
                var ar = g.FirstOrDefault(x => x.CodeLang == 1);
                var en = g.FirstOrDefault(x => x.CodeLang == 2);
                var first = g.First();

                var arDesc = ar?.CodeDesc ?? first.CodeDesc;
                var enDesc = en?.CodeDesc ?? first.CodeDesc;
                var val = !string.IsNullOrEmpty(first.CodeValue)
                    ? first.CodeValue
                    : (en?.CodeValue ?? ar?.CodeValue ?? string.Empty);

                var isEnglish = lang == 2;
                var localizedName = isEnglish
                    ? (!string.IsNullOrEmpty(enDesc) ? enDesc : arDesc)
                    : (!string.IsNullOrEmpty(arDesc) ? arDesc : enDesc);

                return new SysCodeLookupDto
                {
                    Code = g.Key,
                    Value = val,
                    NameAr = arDesc,
                    NameEn = enDesc,
                    Name = localizedName,
                    IsActive = first.IsActive
                };
            })
            .OrderBy(x => x.Code)
            .ToList();

        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(lookupList, "Order types retrieved successfully"));
    }
}
