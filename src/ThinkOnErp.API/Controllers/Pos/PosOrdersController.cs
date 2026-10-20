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
    /// Updates the status of an existing order (e.g. Parked -> SentToKitchen, Draft -> SentToKitchen, SentToKitchen -> Ready).
    /// Accepts numeric SYS_CODE (1: Draft, 2: Parked, 3: SentToKitchen, 4: Ready, 5: Completed, 6: Voided, 7: Refunded) or enum string name.
    /// </summary>
    [HttpPut("{orderId:long}/status")]
    [HttpPatch("{orderId:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> UpdateOrderStatus(
        long orderId,
        [FromBody] UpdatePosOrderStatusDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var newStatus = Enum.IsDefined(typeof(PosOrderStatus), dto.Status) ? (PosOrderStatus)dto.Status : PosOrderStatus.Draft;
        var result = await _orderService.UpdateOrderStatusAsync(orderId, newStatus, username, ct);
        return result.Success 
            ? Ok(result) 
            : (result.StatusCode == 404 ? NotFound(result) : BadRequest(result));
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
    /// Appends new supplementary items (Round 2 / Add-ons) to an open or active order.
    /// Only new items are dispatched to kitchen display stations (KDS) without duplicating previously sent items.
    /// </summary>
    [HttpPost("{orderId:long}/lines")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> AddOrderLines(
        long orderId,
        [FromBody] AddOrderLinesDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _orderService.AddOrderLinesAsync(orderId, dto, username, ct);
        return result.Success 
            ? Ok(result) 
            : (result.StatusCode == 404 ? NotFound(result) : BadRequest(result));
    }

    /// <summary>
    /// Updates an existing order line (e.g. quantity or special preparation instructions).
    /// Guarded: prevents modification if kitchen status is Preparing, Ready, or Served.
    /// </summary>
    [HttpPut("{orderId:long}/lines/{lineId:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosOrderSummaryDto>>> UpdateOrderLine(
        long orderId,
        long lineId,
        [FromBody] UpdateOrderLineDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _orderService.UpdateOrderLineAsync(orderId, lineId, dto, username, ct);
        return result.Success 
            ? Ok(result) 
            : (result.StatusCode == 404 ? NotFound(result) : BadRequest(result));
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
    [HttpGet("order-types")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetOrderTypes([FromQuery] int? lang = null)
    {
        var rawCodes = await _sysCodeRepo.GetActiveByCodeMgrAsync(SysCodeKeys.PosOrderTypes.Mgr);

        var lookupList = rawCodes.Count > 0
            ? SysCodeLookupHelper.MapToLookupDtos(rawCodes, lang)
            : SysCodeLookupHelper.GetFallbackLookups(SysCodeKeys.PosOrderTypes.Mgr, lang);

        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(lookupList, "Order types retrieved successfully"));
    }

    /// <summary>
    /// Retrieves all dynamic POS Order Statuses from SYS_CODE (CODE_MGR = 36).
    /// </summary>
    /// <param name="lang">Optional language filter (1 = Arabic, 2 = English).</param>
    /// <returns>List of order statuses with localized names.</returns>
    [HttpGet("order-statuses")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetOrderStatuses([FromQuery] int? lang = null)
    {
        var rawCodes = await _sysCodeRepo.GetActiveByCodeMgrAsync(SysCodeKeys.PosOrderStatuses.Mgr);

        var lookupList = rawCodes.Count > 0
            ? SysCodeLookupHelper.MapToLookupDtos(rawCodes, lang)
            : SysCodeLookupHelper.GetFallbackLookups(SysCodeKeys.PosOrderStatuses.Mgr, lang);

        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(lookupList, "Order statuses retrieved successfully"));
    }


    /// <summary>
    /// Retrieves all POS Stock Deduction Modes from SYS_CODE (CODE_MGR = 35).
    /// </summary>
    /// <param name="lang">Optional language filter (1 = Arabic, 2 = English).</param>
    /// <returns>List of stock deduction modes with localized descriptions.</returns>
    [HttpGet("stock-deduction-modes")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetStockDeductionModes([FromQuery] int? lang = null)
    {
        var rawCodes = await _sysCodeRepo.GetActiveByCodeMgrAsync(SysCodeKeys.PosStockDeductionModes.Mgr);

        var lookupList = rawCodes.Count > 0
            ? SysCodeLookupHelper.MapToLookupDtos(rawCodes, lang)
            : SysCodeLookupHelper.GetFallbackLookups(SysCodeKeys.PosStockDeductionModes.Mgr, lang);

        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(lookupList, "Stock deduction modes retrieved successfully"));
    }

    /// <summary>
    /// Splits items/quantities from an active order into a separate sub-order, with optional immediate payment and fiscal invoice generation.
    /// فصل أصناف/كميات محددة من الفاتورة الحالية إلى فاتورة فرعية جديدة مستقلة (مع إمكانية السداد الفوري للزبون الأول).
    /// </summary>
    /// <param name="orderId">Original Order ID (معرف الفاتورة الأصلية المراد الفصل منها).</param>
    /// <param name="dto">Details of lines and quantities to split, and optional immediate settlement tenders.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{orderId:long}/split")]
    [ProducesResponseType(typeof(ApiResponse<SplitOrderResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SplitOrderResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SplitOrderResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SplitOrderResultDto>>> SplitOrder(
        [FromRoute] long orderId,
        [FromBody] SplitPosOrderDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _orderService.SplitOrderAsync(orderId, dto, username, ct);
        if (!result.Success)
        {
            return result.StatusCode == 404 ? NotFound(result) : BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Performs an atomic Item Exchange: Generates a fiscal credit note/refund for returned items, creates a new sales order for replacement items, and settles the net price difference.
    /// تنفيذ فاتورة استبدال أصناف: إصدار إشعار دائن/إرجاع للأصناف المعادة + إصدار فاتورة مبيعات جديدة للأصناف البديلة + مقاصة وتسوية فارق السعر تلقائياً.
    /// </summary>
    /// <param name="dto">Exchange details containing return lines, new replacement lines, and difference settlement tenders.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("exchange")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeOrderResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExchangeOrderResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ExchangeOrderResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ExchangeOrderResultDto>>> ExchangeOrder(
        [FromBody] ExchangeOrderDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _orderService.ExchangeOrderAsync(dto, username, ct);
        if (!result.Success)
        {
            return result.StatusCode == 404 ? NotFound(result) : BadRequest(result);
        }
        return Ok(result);
    }
}
