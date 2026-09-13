using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public class PosOrderService : IPosOrderService
{
    private readonly IPosOrderRepository _orderRepository;
    private readonly IPosShiftRepository _shiftRepository;
    private readonly IPosTableRepository _tableRepository;
    private readonly IPosCalculationEngine _calculationEngine;
    private readonly IPosAuditService _auditService;

    public PosOrderService(
        IPosOrderRepository orderRepository,
        IPosShiftRepository shiftRepository,
        IPosTableRepository tableRepository,
        IPosCalculationEngine calculationEngine,
        IPosAuditService auditService)
    {
        _orderRepository = orderRepository;
        _shiftRepository = shiftRepository;
        _tableRepository = tableRepository;
        _calculationEngine = calculationEngine;
        _auditService = auditService;
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> CreateOrderAsync(CreatePosOrderDto dto, string username, CancellationToken ct = default)
    {
        // 1. Idempotency Check: if ClientUuid already exists for this branch, return existing order
        if (!string.IsNullOrWhiteSpace(dto.ClientUuid))
        {
            var existing = await _orderRepository.GetOrderByClientUuidAsync(dto.BranchId, dto.ClientUuid, ct);
            if (existing != null)
                return ApiResponse<PosOrderSummaryDto>.CreateSuccess(MapToSummary(existing), "Order retrieved from cache (idempotent)");
        }

        // 2. Validate Shift
        var shift = await _shiftRepository.GetShiftByIdAsync(dto.ShiftId, ct);
        if (shift == null || shift.Status != PosShiftStatus.Open)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Active shift not found or shift is closed", null, 400);

        // 3. Compute Totals using Calculation Engine
        var calcResult = await _calculationEngine.CalculateOrderTotalsAsync(dto.BranchId, dto, 15m, ct);

        // 4. Generate Order Number
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var count = await _orderRepository.GetOrderCountTodayAsync(dto.BranchId, ct);
        var orderNumber = $"ORD-{dto.BranchId}-{today}-{count + 1:D4}";

        var order = new PosOrderHeader
        {
            BranchId = dto.BranchId,
            ShiftId = dto.ShiftId,
            TillId = dto.TillId,
            OrderNumber = orderNumber,
            InvoiceNumber = $"INV-{dto.BranchId}-{today}-{count + 1:D4}",
            ClientUuid = string.IsNullOrWhiteSpace(dto.ClientUuid) ? Guid.NewGuid().ToString() : dto.ClientUuid,
            OrderType = dto.OrderType,
            Status = dto.Status == PosOrderStatus.Parked ? PosOrderStatus.Parked : PosOrderStatus.Draft,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            TableId = dto.TableId,
            Covers = dto.Covers,
            PriceListId = dto.PriceListId,

            SubtotalAmount = calcResult.SubtotalAmount,
            DiscountAmount = calcResult.TotalDiscountAmount,
            DiscountPercent = dto.ManualDiscountPercent,
            DiscountReason = dto.DiscountReason,
            TaxAmount = calcResult.TaxAmount,
            ServiceChargeAmount = calcResult.ServiceChargeAmount,
            DeliveryFee = calcResult.DeliveryFee,
            TipAmount = calcResult.TipAmount,
            TotalAmount = calcResult.TotalAmount,

            Notes = dto.Notes,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        // 5. Build Lines
        int lineIdx = 1;
        foreach (var reqLine in dto.Lines)
        {
            var calculated = calcResult.Lines.FirstOrDefault(l => l.LineNumber == reqLine.LineNumber);
            var orderLine = new PosOrderLine
            {
                LineNumber = lineIdx++,
                ItemId = reqLine.ItemId,
                ItemCode = reqLine.ItemCode,
                ItemName = reqLine.ItemName,
                Quantity = reqLine.Quantity,
                UomId = reqLine.UomId,
                UnitPrice = reqLine.UnitPrice,
                DiscountAmount = calculated?.LineDiscount ?? 0m,
                DiscountPercent = reqLine.DiscountPercent,
                TaxAmount = calculated?.TaxAmount ?? 0m,
                TaxPercent = calculated?.TaxPercent ?? 15m,
                LineTotal = calculated?.LineTotal ?? (reqLine.Quantity * reqLine.UnitPrice),
                PrepStation = reqLine.PrepStation,
                KdsStatus = PosKdsStatus.Pending,
                IsScaleItem = reqLine.IsScaleItem,
                ScaleWeight = reqLine.ScaleWeight,
                ScaleBarcode = reqLine.ScaleBarcode,
                SalesEmployeeId = reqLine.SalesEmployeeId,
                SpecialInstructions = reqLine.SpecialInstructions
            };

            foreach (var mod in reqLine.Modifiers)
            {
                orderLine.Modifiers.Add(new PosOrderLineModifier
                {
                    ModifierItemId = mod.ModifierItemId,
                    ModifierName = mod.ModifierName,
                    Quantity = mod.Quantity,
                    UnitPrice = mod.UnitPrice,
                    ExtraPrice = mod.ExtraPrice
                });
            }

            order.Lines.Add(orderLine);
        }

        // 6. Build Taxes
        order.Taxes.Add(new PosOrderTax
        {
            TaxRateId = 1, // Default VAT
            TaxRateCode = "VAT_15",
            TaxPercent = 15m,
            TaxableAmount = calcResult.NetTaxableAmount,
            TaxAmount = calcResult.TaxAmount,
            IsInclusive = false
        });

        // 7. Process Initial Payments if provided
        if (dto.Payments.Any())
        {
            decimal totalPaid = 0m;
            foreach (var p in dto.Payments)
            {
                order.Payments.Add(new PosOrderPayment
                {
                    PaymentMethod = p.PaymentMethod,
                    Amount = p.Amount,
                    TenderedAmount = p.TenderedAmount,
                    ChangeAmount = p.ChangeAmount,
                    CardNumberMasked = p.CardNumberMasked,
                    CardType = p.CardType,
                    TransactionReference = p.TransactionReference,
                    AuthCode = p.AuthCode,
                    TerminalId = p.TerminalId,
                    ChequeNumber = p.ChequeNumber,
                    GiftCardCode = p.GiftCardCode,
                    LoyaltyPointsRedeemed = p.LoyaltyPointsRedeemed,
                    PaymentDate = DateTime.UtcNow,
                    CreationUser = username
                });
                totalPaid += p.Amount;

                // Update shift cash/card totals
                if (p.PaymentMethod == PosPaymentMethod.Cash)
                    shift.TotalCashSales += p.Amount;
                else if (p.PaymentMethod == PosPaymentMethod.Card)
                    shift.TotalCardSales += p.Amount;
                else
                    shift.TotalOtherSales += p.Amount;
            }

            order.PaidAmount = totalPaid;
            order.ChangeAmount = Math.Max(0, totalPaid - order.TotalAmount);
            if (totalPaid >= order.TotalAmount)
            {
                order.IsPaid = true;
                order.Status = PosOrderStatus.Completed;
            }
        }

        // 8. Update Table status if applicable
        if (order.TableId.HasValue)
        {
            await _tableRepository.UpdateTableStatusAsync(
                order.TableId.Value,
                order.IsPaid ? PosTableStatus.Cleaning : PosTableStatus.Occupied,
                order.IsPaid ? null : order.Id,
                ct);
        }

        await _orderRepository.AddOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);
        await _shiftRepository.UpdateShiftAsync(shift, ct);
        await _shiftRepository.SaveChangesAsync(ct);

        return await GetOrderByIdAsync(order.Id, ct);
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> ProcessPaymentAsync(long orderId, List<CreatePosOrderPaymentDto> payments, string username, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (order == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order not found", null, 404);

        if (order.IsPaid)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order is already paid in full", null, 400);

        var shift = await _shiftRepository.GetShiftByIdAsync(order.ShiftId, ct);

        decimal addedPayment = 0m;
        foreach (var p in payments)
        {
            order.Payments.Add(new PosOrderPayment
            {
                OrderId = order.Id,
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount,
                TenderedAmount = p.TenderedAmount,
                ChangeAmount = p.ChangeAmount,
                CardNumberMasked = p.CardNumberMasked,
                CardType = p.CardType,
                TransactionReference = p.TransactionReference,
                AuthCode = p.AuthCode,
                TerminalId = p.TerminalId,
                PaymentDate = DateTime.UtcNow,
                CreationUser = username
            });
            addedPayment += p.Amount;

            if (shift != null)
            {
                if (p.PaymentMethod == PosPaymentMethod.Cash)
                    shift.TotalCashSales += p.Amount;
                else if (p.PaymentMethod == PosPaymentMethod.Card)
                    shift.TotalCardSales += p.Amount;
                else
                    shift.TotalOtherSales += p.Amount;
            }
        }

        order.PaidAmount += addedPayment;
        order.ChangeAmount = Math.Max(0, order.PaidAmount - order.TotalAmount);

        if (order.PaidAmount >= order.TotalAmount)
        {
            order.IsPaid = true;
            order.Status = PosOrderStatus.Completed;

            if (order.TableId.HasValue)
            {
                await _tableRepository.UpdateTableStatusAsync(order.TableId.Value, PosTableStatus.Cleaning, null, ct);
            }
        }

        order.UpdateUser = username;
        order.UpdateDate = DateTime.UtcNow;

        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        if (shift != null)
        {
            await _shiftRepository.UpdateShiftAsync(shift, ct);
            await _shiftRepository.SaveChangesAsync(ct);
        }

        return await GetOrderByIdAsync(order.Id, ct);
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> ParkOrderAsync(long orderId, string username, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (order == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order not found", null, 404);

        if (order.IsPaid)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Paid order cannot be parked", null, 400);

        order.Status = PosOrderStatus.Parked;
        order.UpdateUser = username;
        order.UpdateDate = DateTime.UtcNow;

        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        return await GetOrderByIdAsync(order.Id, ct);
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> RefundOrderAsync(RefundOrderDto dto, string username, CancellationToken ct = default)
    {
        var originalOrder = await _orderRepository.GetOrderByIdAsync(dto.OrderId, ct);
        if (originalOrder == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Original order not found", null, 404);

        if (!originalOrder.IsPaid)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Cannot refund an unpaid order", null, 400);

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var count = await _orderRepository.GetOrderCountTodayAsync(originalOrder.BranchId, ct);

        var refundOrder = new PosOrderHeader
        {
            BranchId = originalOrder.BranchId,
            ShiftId = originalOrder.ShiftId,
            TillId = originalOrder.TillId,
            OrderNumber = $"REF-{originalOrder.BranchId}-{today}-{count + 1:D4}",
            InvoiceNumber = $"RF-INV-{originalOrder.BranchId}-{today}-{count + 1:D4}",
            ClientUuid = Guid.NewGuid().ToString(),
            OrderType = originalOrder.OrderType,
            Status = PosOrderStatus.Refunded,
            IsRefund = true,
            OriginalOrderId = originalOrder.Id,
            RefundReason = dto.RefundReason,
            CustomerId = originalOrder.CustomerId,
            CustomerName = originalOrder.CustomerName,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        decimal refundSubtotal = 0;
        decimal refundTax = 0;

        foreach (var refLine in dto.RefundLines)
        {
            var origLine = originalOrder.Lines.FirstOrDefault(l => l.Id == refLine.OrderLineId);
            if (origLine != null)
            {
                var qty = Math.Min(refLine.QuantityToRefund, origLine.Quantity);
                var lineAmount = qty * origLine.UnitPrice;
                var lineTax = qty * (origLine.TaxAmount / (origLine.Quantity > 0 ? origLine.Quantity : 1));

                refundSubtotal += lineAmount;
                refundTax += lineTax;

                refundOrder.Lines.Add(new PosOrderLine
                {
                    LineNumber = origLine.LineNumber,
                    ItemId = origLine.ItemId,
                    ItemCode = origLine.ItemCode,
                    ItemName = origLine.ItemName,
                    Quantity = -qty, // Negative quantity for return
                    UomId = origLine.UomId,
                    UnitPrice = origLine.UnitPrice,
                    LineTotal = -lineAmount,
                    TaxAmount = -lineTax,
                    TaxPercent = origLine.TaxPercent
                });
            }
        }

        refundOrder.SubtotalAmount = -refundSubtotal;
        refundOrder.TaxAmount = -refundTax;
        refundOrder.TotalAmount = -(refundSubtotal + refundTax);
        refundOrder.PaidAmount = -(refundSubtotal + refundTax);
        refundOrder.IsPaid = true;

        // Shift refund adjustment
        var shift = await _shiftRepository.GetShiftByIdAsync(originalOrder.ShiftId, ct);
        if (shift != null)
        {
            shift.TotalCashRefunds += Math.Abs(refundOrder.TotalAmount);
            await _shiftRepository.UpdateShiftAsync(shift, ct);
        }

        await _orderRepository.AddOrderAsync(refundOrder, ct);
        await _orderRepository.SaveChangesAsync(ct);

        await _auditService.LogRefundOrderAsync(
            originalOrder.BranchId,
            originalOrder.Id,
            refundOrder.Id,
            Math.Abs(refundOrder.TotalAmount),
            dto.RefundReason,
            null,
            username,
            ct);

        return await GetOrderByIdAsync(refundOrder.Id, ct);
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> VoidOrderLineAsync(VoidLineDto dto, string username, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(dto.OrderId, ct);
        if (order == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order not found", null, 404);

        if (order.IsPaid)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Cannot void lines on a paid order (use refund instead)", null, 400);

        var line = order.Lines.FirstOrDefault(l => l.Id == dto.OrderLineId);
        if (line == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order line not found", null, 404);

        line.IsVoided = true;
        line.VoidReason = dto.Reason;
        line.VoidApprovedBy = dto.ApprovedBy;

        // Recalculate order totals
        order.SubtotalAmount -= (line.Quantity * line.UnitPrice);
        order.TaxAmount -= line.TaxAmount;
        order.TotalAmount = Math.Max(0, order.TotalAmount - line.LineTotal);

        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        await _auditService.LogVoidLineAsync(
            order.BranchId,
            order.Id,
            line.Id,
            line.ItemCode,
            line.Quantity,
            line.LineTotal,
            dto.Reason,
            dto.ApprovedBy,
            username,
            ct);

        return await GetOrderByIdAsync(order.Id, ct);
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> GetOrderByIdAsync(long orderId, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (order == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order not found", null, 404);

        return ApiResponse<PosOrderSummaryDto>.CreateSuccess(MapToSummary(order));
    }

    public async Task<ApiResponse<IReadOnlyList<PosOrderSummaryDto>>> GetActiveOrdersAsync(long branchId, PosOrderStatus? status = null, CancellationToken ct = default)
    {
        var orders = await _orderRepository.GetActiveOrdersAsync(branchId, status, ct);
        var summaries = orders.Select(MapToSummary).ToList();
        return ApiResponse<IReadOnlyList<PosOrderSummaryDto>>.CreateSuccess(summaries);
    }

    public async Task<ApiResponse<PagedResultDto<PosOrderSummaryDto>>> GetOrdersPagedAsync(
        long branchId,
        long? shiftId = null,
        PosOrderStatus? status = null,
        PosOrderType? orderType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _orderRepository.GetOrdersPagedAsync(
            branchId, shiftId, status, orderType, fromDate, toDate, pageIndex, pageSize, ct);

        var dtos = items.Select(MapToSummary).ToList();
        var paged = new PagedResultDto<PosOrderSummaryDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<PosOrderSummaryDto>>.CreateSuccess(paged);
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> UpdateOrderAsync(long orderId, UpdatePosOrderDto dto, string username, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (order == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order not found", null, 404);

        if (order.Status == PosOrderStatus.Completed || order.Status == PosOrderStatus.Voided || order.Status == PosOrderStatus.Refunded)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Only Draft or Parked orders can be updated", null, 400);

        order.CustomerId = dto.CustomerId;
        order.CustomerName = dto.CustomerName;
        order.TableId = dto.TableId;
        order.Covers = dto.Covers > 0 ? dto.Covers : 1;
        order.Notes = dto.Notes;
        order.DiscountAmount = dto.ManualDiscountAmount;
        order.DiscountPercent = dto.ManualDiscountPercent;
        order.DiscountReason = dto.DiscountReason;
        order.ServiceChargeAmount = dto.ServiceChargeAmount;
        order.DeliveryFee = dto.DeliveryFee;
        order.TipAmount = dto.TipAmount;

        // Recalculate totals
        var linesNet = order.Lines.Where(l => !l.IsVoided).Sum(l => l.LineTotal);
        order.SubtotalAmount = linesNet;
        order.TotalAmount = Math.Max(0, linesNet - dto.ManualDiscountAmount + dto.ServiceChargeAmount + dto.DeliveryFee + dto.TipAmount);

        order.UpdateUser = username;
        order.UpdateDate = DateTime.UtcNow;

        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        return ApiResponse<PosOrderSummaryDto>.CreateSuccess(MapToSummary(order), "Order updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteOrderAsync(long orderId, string username, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (order == null)
            return ApiResponse<bool>.CreateFailure("Order not found", null, 404);

        if (order.PaidAmount > 0 || order.Payments.Any(p => p.Amount > 0))
            return ApiResponse<bool>.CreateFailure("Cannot delete an order that has processed payments", null, 400);

        // If seated at table, release table
        if (order.TableId.HasValue)
        {
            var table = await _tableRepository.GetTableByIdAsync(order.TableId.Value, ct);
            if (table != null && table.ActiveOrderId == order.Id)
            {
                table.Status = PosTableStatus.Available;
                table.ActiveOrderId = null;
                table.StatusChangedAt = DateTime.UtcNow;
            }
        }

        order.IsActive = false;
        order.Status = PosOrderStatus.Voided;
        order.Notes = (order.Notes ?? "") + $" [Order deleted by {username} at {DateTime.UtcNow:s}]";
        order.UpdateUser = username;
        order.UpdateDate = DateTime.UtcNow;

        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Order deleted successfully");
    }

    private static PosOrderSummaryDto MapToSummary(PosOrderHeader o)
    {
        return new PosOrderSummaryDto
        {
            Id = o.Id,
            BranchId = o.BranchId,
            ShiftId = o.ShiftId,
            TillId = o.TillId,
            OrderNumber = o.OrderNumber,
            InvoiceNumber = o.InvoiceNumber,
            ClientUuid = o.ClientUuid,
            OrderType = o.OrderType,
            Status = o.Status,
            CustomerId = o.CustomerId,
            CustomerName = o.CustomerName,
            TableId = o.TableId,
            TableNumber = o.Table?.TableNumber,
            Covers = o.Covers,
            SubtotalAmount = o.SubtotalAmount,
            DiscountAmount = o.DiscountAmount,
            TaxAmount = o.TaxAmount,
            ServiceChargeAmount = o.ServiceChargeAmount,
            DeliveryFee = o.DeliveryFee,
            TipAmount = o.TipAmount,
            TotalAmount = o.TotalAmount,
            PaidAmount = o.PaidAmount,
            ChangeAmount = o.ChangeAmount,
            IsPaid = o.IsPaid,
            IsRefund = o.IsRefund,
            EInvoiceQrCode = o.EInvoiceQrCode,
            EInvoiceHash = o.EInvoiceHash,
            CreationDate = o.CreationDate,
            CreationUser = o.CreationUser,
            Lines = o.Lines.Select(l => new PosOrderLineSummaryDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                ItemId = l.ItemId,
                ItemCode = l.ItemCode,
                ItemName = l.ItemName,
                Quantity = l.Quantity,
                UomId = l.UomId,
                UnitPrice = l.UnitPrice,
                DiscountAmount = l.DiscountAmount,
                TaxAmount = l.TaxAmount,
                LineTotal = l.LineTotal,
                KdsStatus = l.KdsStatus,
                PrepStation = l.PrepStation,
                IsScaleItem = l.IsScaleItem,
                ScaleWeight = l.ScaleWeight,
                IsVoided = l.IsVoided,
                VoidReason = l.VoidReason,
                Modifiers = l.Modifiers.Select(m => new PosOrderLineModifierDto
                {
                    Id = m.Id,
                    ModifierItemId = m.ModifierItemId,
                    ModifierName = m.ModifierName,
                    Quantity = m.Quantity,
                    ExtraPrice = m.ExtraPrice
                }).ToList()
            }).ToList(),
            Payments = o.Payments.Select(p => new PosOrderPaymentSummaryDto
            {
                Id = p.Id,
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount,
                TenderedAmount = p.TenderedAmount,
                ChangeAmount = p.ChangeAmount,
                CardNumberMasked = p.CardNumberMasked,
                TransactionReference = p.TransactionReference,
                PaymentDate = p.PaymentDate
            }).ToList()
        };
    }
}
