using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Application.Services.Validation;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public class PosOrderService : IPosOrderService
{
    private readonly IPosOrderRepository _orderRepository;
    private readonly IPosShiftRepository _shiftRepository;
    private readonly IPosTableRepository _tableRepository;
    private readonly IPosCalculationEngine _calculationEngine;
    private readonly IPosAuditService _auditService;
    private readonly IPosModifierRepository _modifierRepository;
    private readonly IInvoiceBusinessValidationService _validationService;
    private readonly IDynamicValidationEngine _dynamicValidationEngine;
    private readonly IInvStockLedgerService _stockLedgerService;
    private readonly ISysSettingRepository _settingRepo;
    private readonly IInvWarehouseRepository _warehouseRepo;
    private readonly ILogger<PosOrderService> _logger;

    public PosOrderService(
        IPosOrderRepository orderRepository,
        IPosShiftRepository shiftRepository,
        IPosTableRepository tableRepository,
        IPosCalculationEngine calculationEngine,
        IPosAuditService auditService,
        IPosModifierRepository modifierRepository,
        IInvoiceBusinessValidationService validationService,
        IDynamicValidationEngine dynamicValidationEngine,
        IInvStockLedgerService stockLedgerService,
        ISysSettingRepository settingRepo,
        IInvWarehouseRepository warehouseRepo,
        ILogger<PosOrderService> logger)
    {
        _orderRepository = orderRepository;
        _shiftRepository = shiftRepository;
        _tableRepository = tableRepository;
        _calculationEngine = calculationEngine;
        _auditService = auditService;
        _modifierRepository = modifierRepository;
        _validationService = validationService;
        _dynamicValidationEngine = dynamicValidationEngine;
        _stockLedgerService = stockLedgerService;
        _settingRepo = settingRepo;
        _warehouseRepo = warehouseRepo;
        _logger = logger;
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

        // 2. Validate Active Shift
        var shift = await _shiftRepository.GetShiftByIdAsync(dto.ShiftId, ct);
        if (shift == null || shift.Status != PosShiftStatus.Open)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Cannot create order without an active, open shift", null, 400);

        // 2.1 Validate Mandatory Modifier Groups (BRD Section 4.5)
        foreach (var reqLine in dto.Lines)
        {
            var itemGroups = await _modifierRepository.GetGroupsByItemIdAsync(reqLine.ItemId, ct);
            foreach (var grp in itemGroups.Where(g => g.IsActive))
            {
                var optionNames = grp.Options.Where(o => o.IsActive).Select(o => o.OptionNameLocal).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var optionItemIds = grp.Options.Where(o => o.IsActive && o.RelatedItemId.HasValue).Select(o => o.RelatedItemId!.Value).ToHashSet();

                var selectedCount = reqLine.Modifiers.Count(m =>
                    optionNames.Contains(m.ModifierName) ||
                    (m.ModifierItemId > 0 && optionItemIds.Contains(m.ModifierItemId)));

                if (grp.IsRequired && selectedCount == 0)
                {
                    return ApiResponse<PosOrderSummaryDto>.CreateFailure(
                        $"Item '{reqLine.ItemName}' requires selecting at least one option from modifier group '{grp.GroupNameLocal}'",
                        null,
                        400);
                }

                if (grp.MaxSelections.HasValue && selectedCount > grp.MaxSelections.Value)
                {
                    return ApiResponse<PosOrderSummaryDto>.CreateFailure(
                        $"Item '{reqLine.ItemName}' exceeds maximum allowed selections ({grp.MaxSelections.Value}) for modifier group '{grp.GroupNameLocal}'",
                        null,
                        400);
                }
            }
        }

        // Dynamic Field & Schema Validation via DynamicValidationEngine
        var fieldValidation = _dynamicValidationEngine.Validate(dto);
        if (!fieldValidation.IsValid)
        {
            var firstErr = fieldValidation.Errors.First();
            return ApiResponse<PosOrderSummaryDto>.CreateFailure(firstErr.ErrorMessage, null, 400);
        }

        // Dynamic Business & Inventory Validation via InvoiceBusinessValidationService
        var bizValidation = await _validationService.ValidatePosOrderDtoAsync(dto, ct);
        if (!bizValidation.IsValid)
        {
            var errMessages = string.Join("; ", bizValidation.Errors.Select(e => e.Message));
            return ApiResponse<PosOrderSummaryDto>.CreateFailure(errMessages, null, 400);
        }

        // 3. Compute Totals using Calculation Engine (Dynamic Tax Resolution)
        var calcResult = await _calculationEngine.CalculateOrderTotalsAsync(dto.BranchId, dto, null, ct);

        // 4. Generate Shift-scoped Order Number (0001, 0002, ...) and Fiscal Invoice Number (INV-2026-000008)
        var orderNumber = await _orderRepository.GenerateNextShiftOrderNumberAsync(dto.ShiftId, isRefund: false, ct);
        var currentYear = DateTime.UtcNow.Year;
        var invoiceNumber = await _orderRepository.GenerateNextInvoiceNumberAsync(dto.BranchId, currentYear, isRefund: false, ct);

        var order = new PosOrderHeader
        {
            BranchId = dto.BranchId,
            ShiftId = dto.ShiftId,
            TillId = dto.TillId,
            OrderNumber = orderNumber,
            InvoiceNumber = invoiceNumber,
            ClientUuid = string.IsNullOrWhiteSpace(dto.ClientUuid) ? Guid.NewGuid().ToString() : dto.ClientUuid,
            OrderType = dto.OrderType,
            Status = dto.Status != 0 ? dto.Status : PosOrderStatus.Draft,
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
                KdsSentAt = (dto.Status == PosOrderStatus.SentToKitchen || dto.Status == PosOrderStatus.Completed) ? DateTime.UtcNow : null,
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

        // 6. Build Taxes Dynamically from calculated lines
        var taxGroups = calcResult.Lines.GroupBy(l => l.TaxPercent);
        foreach (var tg in taxGroups)
        {
            var sampleItemId = tg.First().ItemId;
            var taxRateEntity = await _validationService.GetEffectiveTaxRateAsync(sampleItemId, dto.BranchId, ct);
            var taxableAmt = tg.Sum(l => l.LineNetBeforeTax);
            var taxAmt = tg.Sum(l => l.TaxAmount);

            order.Taxes.Add(new PosOrderTax
            {
                TaxRateId = taxRateEntity?.Id ?? 1,
                TaxRateCode = taxRateEntity?.TaxRateCode ?? $"VAT_{tg.Key:0.#}",
                TaxPercent = tg.Key,
                TaxableAmount = taxableAmt,
                TaxAmount = taxAmt,
                IsInclusive = false
            });
        }

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

        if (order.IsPaid)
        {
            await DeductOrderStockIfRealtimeAsync(order, ct);
        }

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

        if (order.IsPaid)
        {
            await DeductOrderStockIfRealtimeAsync(order, ct);
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

        var orderNumber = await _orderRepository.GenerateNextShiftOrderNumberAsync(originalOrder.ShiftId, isRefund: true, ct);
        var currentYear = DateTime.UtcNow.Year;
        var invoiceNumber = await _orderRepository.GenerateNextInvoiceNumberAsync(originalOrder.BranchId, currentYear, isRefund: true, ct);

        var refundOrder = new PosOrderHeader
        {
            BranchId = originalOrder.BranchId,
            ShiftId = originalOrder.ShiftId,
            TillId = originalOrder.TillId,
            OrderNumber = orderNumber,
            InvoiceNumber = invoiceNumber,
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

        await RestockRefundOrderIfRealtimeAsync(refundOrder, originalOrder, ct);

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
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Completed, voided, or refunded orders cannot be modified", null, 400);

        if (dto.Status.HasValue && dto.Status.Value != 0)
        {
            if (dto.Status.Value == PosOrderStatus.SentToKitchen)
            {
                foreach (var line in order.Lines.Where(l => !l.IsVoided))
                {
                    if (!line.KdsSentAt.HasValue)
                        line.KdsSentAt = DateTime.UtcNow;
                    if (line.KdsStatus == 0)
                        line.KdsStatus = PosKdsStatus.Pending;
                }
            }
            order.Status = dto.Status.Value;
        }

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

    public async Task<ApiResponse<PosOrderSummaryDto>> UpdateOrderStatusAsync(long orderId, PosOrderStatus newStatus, string username, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (order == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order not found", null, 404);

        if (order.Status == PosOrderStatus.Voided)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Voided orders cannot be updated", null, 400);

        if (order.Status == PosOrderStatus.Refunded)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Refunded orders cannot be updated", null, 400);

        if (order.Status == PosOrderStatus.Completed && newStatus != PosOrderStatus.Refunded && newStatus != PosOrderStatus.Voided)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Completed orders cannot have their status reverted", null, 400);

        if (newStatus == PosOrderStatus.Parked && order.IsPaid)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Paid order cannot be parked", null, 400);

        if (newStatus == PosOrderStatus.SentToKitchen)
        {
            foreach (var line in order.Lines.Where(l => !l.IsVoided))
            {
                if (!line.KdsSentAt.HasValue)
                    line.KdsSentAt = DateTime.UtcNow;

                if (line.KdsStatus == 0)
                    line.KdsStatus = PosKdsStatus.Pending;
            }
        }
        else if (newStatus == PosOrderStatus.Voided)
        {
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
        }

        order.Status = newStatus;
        order.UpdateUser = username;
        order.UpdateDate = DateTime.UtcNow;

        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        return await GetOrderByIdAsync(order.Id, ct);
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
                    UnitPrice = m.UnitPrice,
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

    private async Task DeductOrderStockIfRealtimeAsync(PosOrderHeader order, CancellationToken ct)
    {
        try
        {
            var setting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosStockDeductionMode);
            var mode = SysCodeKeys.PosStockDeductionModes.RealTime;
            if (setting != null && int.TryParse(setting.SettingValue, out var configuredMode))
            {
                mode = configuredMode;
            }

            if (mode != SysCodeKeys.PosStockDeductionModes.RealTime)
            {
                _logger.LogInformation("POS Stock deduction mode is {Mode} (not RealTime). Skipping real-time deduction for Order {OrderNumber}", mode, order.OrderNumber);
                return;
            }

            var warehouseId = await ResolvePosWarehouseIdAsync(order.BranchId, ct);
            if (warehouseId <= 0)
            {
                _logger.LogWarning("No valid warehouse found for POS real-time stock deduction in branch {BranchId}", order.BranchId);
                return;
            }

            foreach (var line in order.Lines.Where(l => !l.IsVoided && l.Quantity > 0))
            {
                var moveReq = new StockMovementRequestDto
                {
                    ItemId = line.ItemId,
                    WarehouseId = warehouseId,
                    Quantity = line.Quantity,
                    UomCode = line.UomId > 0 ? line.UomId : 1,
                    TransactionType = SysCodeKeys.StockTransactionTypes.PosSales, // 1004
                    SourceDocId = order.InvoiceNumber ?? order.OrderNumber,
                    SourceDocType = "POS_INVOICE",
                    SourceModule = "POS",
                    Notes = $"POS Real-time deduction for Order #{order.OrderNumber}"
                };

                var postResult = await _stockLedgerService.PostMovementAsync(moveReq, ct);
                if (!postResult.Success)
                {
                    _logger.LogWarning("POS real-time stock deduction warning for Item {ItemId}: {Message}", line.ItemId, postResult.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform POS real-time stock deduction for Order {OrderId}", order.Id);
        }
    }

    private async Task RestockRefundOrderIfRealtimeAsync(PosOrderHeader refundOrder, PosOrderHeader originalOrder, CancellationToken ct)
    {
        try
        {
            var setting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosStockDeductionMode);
            var mode = SysCodeKeys.PosStockDeductionModes.RealTime;
            if (setting != null && int.TryParse(setting.SettingValue, out var configuredMode))
            {
                mode = configuredMode;
            }

            if (mode != SysCodeKeys.PosStockDeductionModes.RealTime)
            {
                return;
            }

            var warehouseId = await ResolvePosWarehouseIdAsync(originalOrder.BranchId, ct);
            if (warehouseId <= 0) return;

            foreach (var line in refundOrder.Lines.Where(l => !l.IsVoided && Math.Abs(l.Quantity) > 0))
            {
                var moveReq = new StockMovementRequestDto
                {
                    ItemId = line.ItemId,
                    WarehouseId = warehouseId,
                    Quantity = Math.Abs(line.Quantity),
                    UomCode = line.UomId > 0 ? line.UomId : 1,
                    TransactionType = SysCodeKeys.StockTransactionTypes.SalesReturnRestock, // 1501
                    SourceDocId = refundOrder.InvoiceNumber ?? refundOrder.OrderNumber,
                    SourceDocType = "POS_REFUND",
                    SourceModule = "POS",
                    Notes = $"POS Real-time restock for Refund #{refundOrder.OrderNumber}"
                };

                var postResult = await _stockLedgerService.PostMovementAsync(moveReq, ct);
                if (!postResult.Success)
                {
                    _logger.LogWarning("POS real-time stock restock warning for Item {ItemId}: {Message}", line.ItemId, postResult.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform POS real-time restock for Refund {RefundOrderId}", refundOrder.Id);
        }
    }

    private async Task<long> ResolvePosWarehouseIdAsync(long branchId, CancellationToken ct)
    {
        var whSetting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultWarehouseId);
        if (whSetting != null && long.TryParse(whSetting.SettingValue, out var whId) && whId > 0)
        {
            return whId;
        }

        try
        {
            var warehouses = await _warehouseRepo.GetAllByBranchAsync(branchId, ct);
            var activeWh = warehouses.FirstOrDefault(w => w.IsActive);
            if (activeWh != null) return activeWh.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching warehouses for branch {BranchId}", branchId);
        }

        return 61; // Default fallback POS warehouse
    }
}
