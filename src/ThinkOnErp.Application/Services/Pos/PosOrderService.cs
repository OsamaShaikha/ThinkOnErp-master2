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

using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;

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
    private readonly ICustomerRepository? _customerRepository;
    private readonly IPaymentMethodRepository? _paymentMethodRepository;
    private readonly IGlVoucherService? _voucherService;
    private readonly IGlFiscalPeriodRepository? _fiscalPeriodRepository;
    private readonly IArSubledgerRepository? _arSubledgerRepository;
    private readonly IPosBranchSettingRepository? _branchSettingRepository;

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
        ILogger<PosOrderService> logger,
        ICustomerRepository? customerRepository = null,
        IPaymentMethodRepository? paymentMethodRepository = null,
        IGlVoucherService? voucherService = null,
        IGlFiscalPeriodRepository? fiscalPeriodRepository = null,
        IArSubledgerRepository? arSubledgerRepository = null,
        IPosBranchSettingRepository? branchSettingRepository = null)
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
        _customerRepository = customerRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _voucherService = voucherService;
        _fiscalPeriodRepository = fiscalPeriodRepository;
        _arSubledgerRepository = arSubledgerRepository;
        _branchSettingRepository = branchSettingRepository;
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

        // Validate Table existence if TableId is supplied
        if (dto.TableId.HasValue)
        {
            if (dto.TableId.Value <= 0)
            {
                dto.TableId = null;
            }
            else
            {
                var table = await _tableRepository.GetTableByIdAsync(dto.TableId.Value, ct);
                if (table == null)
                {
                    return ApiResponse<PosOrderSummaryDto>.CreateFailure(
                        $"الطاولة المحددة رقم ({dto.TableId.Value}) غير موجودة.",
                        null,
                        400);
                }
            }
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
            OrderType = Enum.IsDefined(typeof(PosOrderType), dto.OrderType) ? (PosOrderType)dto.OrderType : PosOrderType.DineIn,
            Status = Enum.IsDefined(typeof(PosOrderStatus), dto.Status) && dto.Status != 0 ? (PosOrderStatus)dto.Status : PosOrderStatus.Draft,
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
                KdsSentAt = (dto.Status == (int)PosOrderStatus.SentToKitchen || dto.Status == (int)PosOrderStatus.Completed) ? DateTime.UtcNow : null,
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
        decimal customerAccountAmount = 0m;
        decimal custAdvanceDeducted = 0m;
        decimal custReceivableCharged = 0m;

        if (dto.Payments.Any())
        {
            // Validate any Customer Account payments require CustomerId and Credit Limit checks
            decimal totalCustAccountPayment = 0m;
            foreach (var p in dto.Payments)
            {
                var classification = await ResolvePaymentClassificationAsync(p.PaymentMethod, ct);
                if (classification.IsCustomerAccount)
                {
                    totalCustAccountPayment += p.Amount;
                }
            }

            if (totalCustAccountPayment > 0)
            {
                if (!dto.CustomerId.HasValue || dto.CustomerId.Value <= 0)
                {
                    return ApiResponse<PosOrderSummaryDto>.CreateFailure("يجب تحديد واختيار العميل عند الدفع على الحساب (ذمة عميل).", null, 400);
                }

                var (valid, errorMsg, advDed, recChg, _, _) =
                    await ValidateAndCalculateCustomerAccountPaymentAsync(dto.CustomerId.Value, totalCustAccountPayment, ct);

                if (!valid)
                {
                    return ApiResponse<PosOrderSummaryDto>.CreateFailure(errorMsg!, null, 400);
                }

                custAdvanceDeducted = advDed;
                custReceivableCharged = recChg;
            }

            decimal totalPaid = 0m;
            foreach (var p in dto.Payments)
            {
                var classification = await ResolvePaymentClassificationAsync(p.PaymentMethod, ct);
                var methodEnum = classification.IsCustomerAccount
                    ? PosPaymentMethod.CustomerAccount
                    : (classification.IsHospitality ? PosPaymentMethod.Hospitality : (PosPaymentMethod)p.PaymentMethod);

                var txRef = p.TransactionReference;
                if (classification.IsCustomerAccount && string.IsNullOrWhiteSpace(txRef))
                {
                    txRef = $"ADV:{custAdvanceDeducted:F4}|REC:{custReceivableCharged:F4}";
                }

                order.Payments.Add(new PosOrderPayment
                {
                    PaymentMethod = methodEnum,
                    Amount = p.Amount,
                    TenderedAmount = p.TenderedAmount,
                    ChangeAmount = p.ChangeAmount,
                    CardNumberMasked = p.CardNumberMasked,
                    CardType = p.CardType,
                    TransactionReference = txRef,
                    AuthCode = p.AuthCode,
                    TerminalId = p.TerminalId,
                    ChequeNumber = p.ChequeNumber,
                    GiftCardCode = p.GiftCardCode,
                    LoyaltyPointsRedeemed = p.LoyaltyPointsRedeemed,
                    PaymentDate = DateTime.UtcNow,
                    CreationUser = username
                });
                totalPaid += p.Amount;

                // Update shift tender totals
                if (classification.IsCash)
                    shift.TotalCashSales += p.Amount;
                else if (classification.IsCard)
                    shift.TotalCardSales += p.Amount;
                else if (classification.IsCustomerAccount)
                {
                    shift.TotalCustomerAccountSales += p.Amount;
                    customerAccountAmount += p.Amount;
                }
                else if (classification.IsHospitality)
                    shift.TotalHospitalitySales += p.Amount;
                else
                    shift.TotalOtherSales += p.Amount;
            }

            // Calculate actual paid: Cash/Card/Wallet + Advance Deducted (NOT new receivable charged)
            decimal actualPaid = totalPaid - custReceivableCharged;
            bool isHospitality = dto.Payments.Any(p => p.PaymentMethod == 98 || p.PaymentMethod == (int)PosPaymentMethod.Hospitality);
            if (isHospitality)
            {
                actualPaid = 0m;
            }

            order.PaidAmount = actualPaid;
            order.ChangeAmount = Math.Max(0, actualPaid - order.TotalAmount);

            bool isSettled = (actualPaid >= order.TotalAmount) || (order.TotalAmount == 0) || isHospitality;
            order.IsPaid = isSettled;

            // An order workflow is completed if total tendered covers the order OR if financially settled
            if (totalPaid >= order.TotalAmount || isSettled)
            {
                order.Status = PosOrderStatus.Completed;
            }
        }
        else if (order.TotalAmount == 0)
        {
            order.PaidAmount = 0m;
            order.ChangeAmount = 0m;
            order.IsPaid = true;
            order.Status = PosOrderStatus.Completed;
        }

        await _orderRepository.AddOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);
        await _shiftRepository.UpdateShiftAsync(shift, ct);
        await _shiftRepository.SaveChangesAsync(ct);

        // Record customer receivable in GL / AR subledger if paid on account
        if (customerAccountAmount > 0)
        {
            await RecordCustomerAccountReceivableAsync(order, customerAccountAmount, username, ct, custAdvanceDeducted, custReceivableCharged);
        }

        // 8. Update Table status if applicable (Executed AFTER order is persisted so order.Id is valid)
        if (order.TableId.HasValue && order.TableId.Value > 0)
        {
            bool releaseTable = order.IsPaid || order.Status == PosOrderStatus.Completed;
            await _tableRepository.UpdateTableStatusAsync(
                order.TableId.Value,
                releaseTable ? PosTableStatus.Cleaning : PosTableStatus.Occupied,
                releaseTable ? null : order.Id,
                ct);
        }

        if (order.IsPaid || order.Status == PosOrderStatus.Completed)
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
        decimal customerAccountAmount = 0m;
        decimal custAdvanceDeducted = 0m;
        decimal custReceivableCharged = 0m;

        // Validate customer account requirement and credit limit
        decimal totalCustAccountPayment = 0m;
        foreach (var p in payments)
        {
            var classification = await ResolvePaymentClassificationAsync(p.PaymentMethod, ct);
            if (classification.IsCustomerAccount)
            {
                totalCustAccountPayment += p.Amount;
            }
        }

        if (totalCustAccountPayment > 0)
        {
            if (!order.CustomerId.HasValue || order.CustomerId.Value <= 0)
            {
                return ApiResponse<PosOrderSummaryDto>.CreateFailure("يجب ربط الطلب بعميل مسجل للدفع على الحساب (ذمة عميل).", null, 400);
            }

            var (valid, errorMsg, advDed, recChg, _, _) =
                await ValidateAndCalculateCustomerAccountPaymentAsync(order.CustomerId.Value, totalCustAccountPayment, ct);

            if (!valid)
            {
                return ApiResponse<PosOrderSummaryDto>.CreateFailure(errorMsg!, null, 400);
            }

            custAdvanceDeducted = advDed;
            custReceivableCharged = recChg;
        }

        foreach (var p in payments)
        {
            var classification = await ResolvePaymentClassificationAsync(p.PaymentMethod, ct);
            var methodEnum = classification.IsCustomerAccount
                ? PosPaymentMethod.CustomerAccount
                : (classification.IsHospitality ? PosPaymentMethod.Hospitality : (PosPaymentMethod)p.PaymentMethod);

            var txRef = p.TransactionReference;
            if (classification.IsCustomerAccount && string.IsNullOrWhiteSpace(txRef))
            {
                txRef = $"ADV:{custAdvanceDeducted:F4}|REC:{custReceivableCharged:F4}";
            }

            order.Payments.Add(new PosOrderPayment
            {
                OrderId = order.Id,
                PaymentMethod = methodEnum,
                Amount = p.Amount,
                TenderedAmount = p.TenderedAmount,
                ChangeAmount = p.ChangeAmount,
                CardNumberMasked = p.CardNumberMasked,
                CardType = p.CardType,
                TransactionReference = txRef,
                AuthCode = p.AuthCode,
                TerminalId = p.TerminalId,
                PaymentDate = DateTime.UtcNow,
                CreationUser = username
            });
            addedPayment += p.Amount;

            if (shift != null)
            {
                if (classification.IsCash)
                    shift.TotalCashSales += p.Amount;
                else if (classification.IsCard)
                    shift.TotalCardSales += p.Amount;
                else if (classification.IsCustomerAccount)
                {
                    shift.TotalCustomerAccountSales += p.Amount;
                    customerAccountAmount += p.Amount;
                }
                else if (classification.IsHospitality)
                    shift.TotalHospitalitySales += p.Amount;
                else
                    shift.TotalOtherSales += p.Amount;
            }
        }

        decimal actualAddedPaid = addedPayment - custReceivableCharged;
        bool isHospitalityPayment = payments.Any(p => p.PaymentMethod == 98 || p.PaymentMethod == (int)PosPaymentMethod.Hospitality);
        if (isHospitalityPayment)
        {
            actualAddedPaid = 0m;
        }

        order.PaidAmount += actualAddedPaid;
        order.ChangeAmount = Math.Max(0, order.PaidAmount - order.TotalAmount);

        decimal totalTendered = order.Payments.Sum(p => p.Amount);
        bool isHospitality = order.Payments.Any(p => p.PaymentMethod == PosPaymentMethod.Hospitality);
        bool isSettled = (order.PaidAmount >= order.TotalAmount) || (order.TotalAmount == 0) || isHospitality;
        order.IsPaid = isSettled;

        if (totalTendered >= order.TotalAmount || isSettled)
        {
            order.Status = PosOrderStatus.Completed;

            if (order.TableId.HasValue)
            {
                await _tableRepository.UpdateTableStatusAsync(order.TableId.Value, PosTableStatus.Cleaning, null, ct);

                // Also release any linked/merged secondary tables associated with this primary table or order
                var mergedTables = await _tableRepository.GetMergedTablesAsync(order.TableId.Value, ct);
                foreach (var secTable in mergedTables)
                {
                    secTable.Status = PosTableStatus.Cleaning;
                    secTable.ActiveOrderId = null;
                    secTable.ParentTableId = null;
                    secTable.StatusChangedAt = DateTime.UtcNow;
                    await _tableRepository.UpdateTableAsync(secTable, ct);
                }
                await _tableRepository.SaveChangesAsync(ct);
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

        // Record customer receivable in GL / AR subledger if paid on account
        if (customerAccountAmount > 0)
        {
            await RecordCustomerAccountReceivableAsync(order, customerAccountAmount, username, ct, custAdvanceDeducted, custReceivableCharged);
        }

        if (order.IsPaid || order.Status == PosOrderStatus.Completed)
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
        if (dto == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("بيانات الإرجاع غير صالحة.", null, 400);

        PosOrderHeader? originalOrder = null;

        if (dto.OrderId > 0)
        {
            originalOrder = await _orderRepository.GetOrderByIdAsync(dto.OrderId, ct);
        }

        if (originalOrder == null && !string.IsNullOrWhiteSpace(dto.InvoiceNumber))
        {
            originalOrder = await _orderRepository.GetOrderByInvoiceNumberAsync(dto.InvoiceNumber.Trim(), ct);
        }

        if (originalOrder == null && !string.IsNullOrWhiteSpace(dto.OrderNumber))
        {
            originalOrder = await _orderRepository.GetOrderByOrderNumberAsync(dto.BranchId, dto.OrderNumber.Trim(), ct);
        }

        if (originalOrder == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("لم يتم العثور على الفاتورة الأصلية المراد إرجاعها (يرجى التأكد من معرّف الطلب أو رقم الفاتورة).", null, 404);

        if (originalOrder.IsRefund)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("لا يمكن إرجاع فاتورة مرتجع (إشعار دائن).", null, 400);

        if (originalOrder.Status == PosOrderStatus.Refunded)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("تم إرجاع هذه الفاتورة بالكامل مسبقاً، ولا يمكن عمل أي مرتجع إضافي لها.", null, 400);

        if (!originalOrder.IsPaid && originalOrder.Status != PosOrderStatus.Completed)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("لا يمكن إرجاع فاتورة غير مكتملة أو غير مسددة بالكامل.", null, 400);

        // Fetch prior refunds for this order to prevent duplicate/over-refunding
        var priorRefunds = await _orderRepository.GetRefundOrdersByOriginalOrderIdAsync(originalOrder.Id, ct) ?? new List<PosOrderHeader>();

        // Calculate already refunded quantity per line in original order
        var lineRefundHistory = originalOrder.Lines.Where(l => !l.IsVoided).ToDictionary(
            l => l.Id,
            l =>
            {
                var origQtyAbs = Math.Abs(l.Quantity);
                var alreadyRefunded = priorRefunds.Where(r => r.Lines != null).SelectMany(r => r.Lines)
                    .Where(rl => rl.LineNumber == l.LineNumber && rl.ItemId == l.ItemId)
                    .Sum(rl => Math.Abs(rl.Quantity));
                var remaining = Math.Max(0m, origQtyAbs - alreadyRefunded);
                return new { OrigQty = origQtyAbs, AlreadyRefunded = alreadyRefunded, Remaining = remaining };
            });

        // If specific lines requested, validate remaining quantities
        List<RefundOrderLineDto> linesToProcess;
        if (dto.RefundLines != null && dto.RefundLines.Any())
        {
            foreach (var refLine in dto.RefundLines)
            {
                var origLine = originalOrder.Lines.FirstOrDefault(l => l.Id == refLine.OrderLineId && !l.IsVoided);
                if (origLine == null)
                    return ApiResponse<PosOrderSummaryDto>.CreateFailure($"البند رقم #{refLine.OrderLineId} غير موجود بالفاتورة الأصلية أو ملغي.", null, 400);

                if (refLine.QuantityToRefund <= 0)
                    return ApiResponse<PosOrderSummaryDto>.CreateFailure($"كمية الإرجاع للصنف ({origLine.ItemName}) يجب أن تكون أكبر من الصفر.", null, 400);

                var history = lineRefundHistory[origLine.Id];
                if (history.Remaining <= 0)
                {
                    return ApiResponse<PosOrderSummaryDto>.CreateFailure($"الصنف ({origLine.ItemName}) تم إرجاع كامل كميته مسبقاً (الكمية الأصلية: {history.OrigQty:G29}، المرتجع سابقاً: {history.AlreadyRefunded:G29})، ولا توجد كميات قابلة للإرجاع.", null, 400);
                }

                if (refLine.QuantityToRefund > history.Remaining)
                {
                    return ApiResponse<PosOrderSummaryDto>.CreateFailure($"الكمية المطلوبة للإرجاع ({refLine.QuantityToRefund:G29}) للصنف ({origLine.ItemName}) تتجاوز الكمية المتبقية القابلة للإرجاع ({history.Remaining:G29}). تم إرجاع {history.AlreadyRefunded:G29} سابقاً من أصل {history.OrigQty:G29}.", null, 400);
                }
            }

            linesToProcess = dto.RefundLines;
        }
        else
        {
            // Entire order refund requested: refund only remaining quantities
            linesToProcess = originalOrder.Lines.Where(l => !l.IsVoided)
                .Where(l => lineRefundHistory[l.Id].Remaining > 0)
                .Select(l => new RefundOrderLineDto
                {
                    OrderLineId = l.Id,
                    QuantityToRefund = lineRefundHistory[l.Id].Remaining
                }).ToList();

            if (!linesToProcess.Any())
            {
                return ApiResponse<PosOrderSummaryDto>.CreateFailure("تم إرجاع كافة بنود هذه الفاتورة بالكامل مسبقاً، ولا توجد أي أصناف متبقية قابلة للإرجاع.", null, 400);
            }
        }

        // Determine current shift and till context
        var targetTillId = dto.TillId.HasValue && dto.TillId.Value > 0 ? dto.TillId.Value : originalOrder.TillId;
        long targetShiftId = dto.ShiftId.HasValue && dto.ShiftId.Value > 0 ? dto.ShiftId.Value : 0;
        if (targetShiftId <= 0)
        {
            var activeShift = await _shiftRepository.GetActiveShiftByTillAsync(targetTillId, ct);
            targetShiftId = activeShift?.Id ?? originalOrder.ShiftId;
        }

        var orderNumber = await _orderRepository.GenerateNextShiftOrderNumberAsync(targetShiftId, isRefund: true, ct);
        var currentYear = DateTime.UtcNow.Year;
        var invoiceNumber = await _orderRepository.GenerateNextInvoiceNumberAsync(originalOrder.BranchId, currentYear, isRefund: true, ct);

        var refundOrder = new PosOrderHeader
        {
            BranchId = originalOrder.BranchId,
            ShiftId = targetShiftId,
            TillId = targetTillId,
            OrderNumber = orderNumber,
            InvoiceNumber = invoiceNumber,
            ClientUuid = Guid.NewGuid().ToString("D"),
            OrderType = originalOrder.OrderType,
            Status = PosOrderStatus.Refunded,
            IsRefund = true,
            OriginalOrderId = originalOrder.Id,
            OriginalOrder = originalOrder,
            RefundReason = dto.RefundReason,
            CustomerId = originalOrder.CustomerId,
            CustomerName = originalOrder.CustomerName,
            TableId = originalOrder.TableId,
            PriceListId = originalOrder.PriceListId,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        decimal refundSubtotal = 0m;
        decimal refundTax = 0m;
        decimal refundTotal = 0m;

        foreach (var refLine in linesToProcess)
        {
            var origLine = originalOrder.Lines.FirstOrDefault(l => l.Id == refLine.OrderLineId && !l.IsVoided);
            if (origLine != null)
            {
                var history = lineRefundHistory[origLine.Id];
                var qty = Math.Min(refLine.QuantityToRefund, history.Remaining);
                if (qty <= 0) continue;

                var origQtySafe = history.OrigQty > 0 ? history.OrigQty : 1m;
                var ratio = qty / origQtySafe;
                var modExtraPerUnit = origLine.Modifiers.Sum(m => m.Quantity * m.ExtraPrice) / origQtySafe;
                var discountPerUnit = origLine.DiscountAmount / origQtySafe;
                var netUnitPrice = origLine.UnitPrice + modExtraPerUnit - discountPerUnit;
                var lineSubtotal = qty * netUnitPrice;
                var lineTax = qty * (origLine.TaxAmount / origQtySafe);
                var lineDiscount = origLine.DiscountAmount * ratio;
                var lineTotal = lineSubtotal + lineTax;

                refundSubtotal += lineSubtotal;
                refundTax += lineTax;
                refundTotal += lineTotal;

                var refundLine = new PosOrderLine
                {
                    LineNumber = origLine.LineNumber,
                    ItemId = origLine.ItemId,
                    ItemCode = origLine.ItemCode,
                    ItemName = origLine.ItemName,
                    Quantity = -qty, // Negative quantity for return
                    UomId = origLine.UomId,
                    UnitPrice = origLine.UnitPrice,
                    DiscountAmount = -lineDiscount,
                    DiscountPercent = origLine.DiscountPercent,
                    DiscountReason = origLine.DiscountReason,
                    TaxAmount = -lineTax,
                    TaxPercent = origLine.TaxPercent,
                    LineTotal = -lineTotal,
                    PrepStation = origLine.PrepStation,
                    SpecialInstructions = origLine.SpecialInstructions
                };

                // Copy modifiers proportionally
                foreach (var origMod in origLine.Modifiers)
                {
                    refundLine.Modifiers.Add(new PosOrderLineModifier
                    {
                        ModifierItemId = origMod.ModifierItemId,
                        ModifierName = origMod.ModifierName,
                        Quantity = origMod.Quantity * ratio,
                        UnitPrice = origMod.UnitPrice,
                        ExtraPrice = origMod.ExtraPrice
                    });
                }

                refundOrder.Lines.Add(refundLine);
            }
        }

        if (!refundOrder.Lines.Any())
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("لم يتم تحديد أي بنود صالحة للإرجاع.", null, 400);

        refundOrder.SubtotalAmount = -refundSubtotal;
        refundOrder.TaxAmount = -refundTax;
        refundOrder.TotalAmount = -refundTotal;
        refundOrder.PaidAmount = -refundTotal;
        refundOrder.IsPaid = true;

        // Check if original order was paid via Customer Account
        decimal originalCustAccountPaid = 0m;
        foreach (var origPayment in originalOrder.Payments)
        {
            var classification = await ResolvePaymentClassificationAsync((int)origPayment.PaymentMethod, ct);
            if (classification.IsCustomerAccount)
            {
                originalCustAccountPaid += origPayment.Amount;
            }
        }

        decimal custAccountRefundAmount = 0m;
        decimal cashRefundAmount = 0m;
        decimal cardRefundAmount = 0m;

        if (dto.RefundPayments != null && dto.RefundPayments.Any())
        {
            foreach (var p in dto.RefundPayments)
            {
                var classification = await ResolvePaymentClassificationAsync(p.PaymentMethod, ct);
                var methodEnum = classification.IsCustomerAccount
                    ? PosPaymentMethod.CustomerAccount
                    : (classification.IsHospitality ? PosPaymentMethod.Hospitality : (PosPaymentMethod)p.PaymentMethod);

                var amt = Math.Abs(p.Amount);
                if (classification.IsCustomerAccount) custAccountRefundAmount += amt;
                else if (classification.IsCash) cashRefundAmount += amt;
                else if (classification.IsCard) cardRefundAmount += amt;

                refundOrder.Payments.Add(new PosOrderPayment
                {
                    PaymentMethod = methodEnum,
                    Amount = -amt,
                    TenderedAmount = -Math.Abs(p.TenderedAmount > 0 ? p.TenderedAmount : amt),
                    ChangeAmount = p.ChangeAmount,
                    CardNumberMasked = p.CardNumberMasked,
                    TransactionReference = p.TransactionReference,
                    PaymentDate = DateTime.UtcNow,
                    CreationUser = username
                });
            }
        }
        else
        {
            // Reverse original payment method(s) automatically
            var totalRefundAbs = Math.Abs(refundOrder.TotalAmount);
            if (originalCustAccountPaid > 0)
            {
                custAccountRefundAmount = Math.Min(totalRefundAbs, originalCustAccountPaid);
                refundOrder.Payments.Add(new PosOrderPayment
                {
                    PaymentMethod = PosPaymentMethod.CustomerAccount,
                    Amount = -custAccountRefundAmount,
                    TenderedAmount = -custAccountRefundAmount,
                    PaymentDate = DateTime.UtcNow,
                    CreationUser = username
                });

                var remaining = totalRefundAbs - custAccountRefundAmount;
                if (remaining > 0)
                {
                    cashRefundAmount = remaining;
                    refundOrder.Payments.Add(new PosOrderPayment
                    {
                        PaymentMethod = PosPaymentMethod.Cash,
                        Amount = -remaining,
                        TenderedAmount = -remaining,
                        PaymentDate = DateTime.UtcNow,
                        CreationUser = username
                    });
                }
            }
            else
            {
                var wasCard = originalOrder.Payments.Any(p => p.PaymentMethod == PosPaymentMethod.Card || p.PaymentMethod == PosPaymentMethod.DigitalWallet);
                if (wasCard)
                {
                    cardRefundAmount = totalRefundAbs;
                    refundOrder.Payments.Add(new PosOrderPayment
                    {
                        PaymentMethod = PosPaymentMethod.Card,
                        Amount = -totalRefundAbs,
                        TenderedAmount = -totalRefundAbs,
                        PaymentDate = DateTime.UtcNow,
                        CreationUser = username
                    });
                }
                else
                {
                    cashRefundAmount = totalRefundAbs;
                    refundOrder.Payments.Add(new PosOrderPayment
                    {
                        PaymentMethod = PosPaymentMethod.Cash,
                        Amount = -totalRefundAbs,
                        TenderedAmount = -totalRefundAbs,
                        PaymentDate = DateTime.UtcNow,
                        CreationUser = username
                    });
                }
            }
        }

        // Shift refund adjustment
        var shift = await _shiftRepository.GetShiftByIdAsync(targetShiftId, ct);
        if (shift != null)
        {
            if (cashRefundAmount > 0)
                shift.TotalCashRefunds += cashRefundAmount;
            if (cardRefundAmount > 0)
                shift.TotalCardRefunds += cardRefundAmount;

            await _shiftRepository.UpdateShiftAsync(shift, ct);
            await _shiftRepository.SaveChangesAsync(ct);
        }

        await _orderRepository.AddOrderAsync(refundOrder, ct);
        await _orderRepository.SaveChangesAsync(ct);

        // Check if all lines of the original order are now fully refunded
        bool allLinesFullyRefunded = originalOrder.Lines.Where(l => !l.IsVoided).All(l =>
        {
            var history = lineRefundHistory[l.Id];
            var newlyRefunded = refundOrder.Lines
                .Where(rl => rl.LineNumber == l.LineNumber && rl.ItemId == l.ItemId)
                .Sum(rl => Math.Abs(rl.Quantity));
            return (history.AlreadyRefunded + newlyRefunded) >= history.OrigQty;
        });

        if (allLinesFullyRefunded)
        {
            originalOrder.Status = PosOrderStatus.Refunded;
            originalOrder.UpdateUser = username;
            originalOrder.UpdateDate = DateTime.UtcNow;
            await _orderRepository.UpdateOrderAsync(originalOrder, ct);
            await _orderRepository.SaveChangesAsync(ct);
        }

        // Record customer credit in GL / AR subledger if original was customer account
        if (custAccountRefundAmount > 0)
        {
            await RecordCustomerAccountRefundCreditAsync(refundOrder, originalOrder, custAccountRefundAmount, username, ct);
        }

        if (dto.ReturnToInventory)
        {
            await RestockRefundOrderIfRealtimeAsync(refundOrder, originalOrder, ct);
        }

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

        var summary = MapToSummary(order);
        if (summary.IsCreditSale && order.CustomerId.HasValue && _arSubledgerRepository != null && _customerRepository != null)
        {
            try
            {
                var customer = await _customerRepository.GetByIdAsync(order.CustomerId.Value, ct);
                if (customer != null && !string.IsNullOrWhiteSpace(customer.CustomerCode))
                {
                    var bal = await _arSubledgerRepository.GetCustomerBalanceAsync(customer.CustomerCode, ct);
                    summary.CustomerBalanceAfter = bal;
                    summary.CustomerAvailableCreditAfter = customer.CreditLimit.HasValue ? customer.CreditLimit.Value - bal : null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load customer balance for order {OrderId}", orderId);
            }
        }

        // Enrich refund tracking details
        try
        {
            var priorRefunds = await _orderRepository.GetRefundOrdersByOriginalOrderIdAsync(order.Id, ct);
            if (priorRefunds.Any())
            {
                summary.RefundedAmount = priorRefunds.Sum(r => Math.Abs(r.TotalAmount));
                summary.RemainingRefundableAmount = Math.Max(0m, summary.TotalAmount - summary.RefundedAmount);
                summary.RefundInvoices = priorRefunds.Select(r => r.InvoiceNumber ?? r.OrderNumber).Where(s => !string.IsNullOrEmpty(s)).ToList()!;

                foreach (var line in summary.Lines)
                {
                    line.RefundedQuantity = priorRefunds.SelectMany(r => r.Lines)
                        .Where(rl => rl.LineNumber == line.LineNumber && rl.ItemId == line.ItemId)
                        .Sum(rl => Math.Abs(rl.Quantity));
                    line.RemainingRefundableQuantity = Math.Max(0m, Math.Abs(line.Quantity) - line.RefundedQuantity);
                    line.IsFullyRefunded = line.RemainingRefundableQuantity == 0;
                }

                bool allLinesDone = summary.Lines.Where(l => !l.IsVoided).All(l => l.IsFullyRefunded);
                if (allLinesDone || order.Status == PosOrderStatus.Refunded)
                {
                    summary.RefundStatus = "FullyRefunded";
                    summary.RefundStatusCode = 2;
                    summary.RefundStatusNameAr = "مرتجع بالكامل";
                }
                else
                {
                    summary.RefundStatus = "PartiallyRefunded";
                    summary.RefundStatusCode = 1;
                    summary.RefundStatusNameAr = "مرتجع جزئياً";
                }
            }
            else
            {
                summary.RemainingRefundableAmount = summary.TotalAmount;
                foreach (var line in summary.Lines)
                {
                    line.RefundedQuantity = 0m;
                    line.RemainingRefundableQuantity = Math.Abs(line.Quantity);
                    line.IsFullyRefunded = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load refund tracking details for order {OrderId}", orderId);
        }

        return ApiResponse<PosOrderSummaryDto>.CreateSuccess(summary);
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
            if (dto.Status.Value == (int)PosOrderStatus.SentToKitchen)
            {
                foreach (var line in order.Lines.Where(l => !l.IsVoided))
                {
                    if (!line.KdsSentAt.HasValue)
                        line.KdsSentAt = DateTime.UtcNow;
                    if (line.KdsStatus == 0)
                        line.KdsStatus = PosKdsStatus.Pending;
                }
            }
            order.Status = Enum.IsDefined(typeof(PosOrderStatus), dto.Status.Value) ? (PosOrderStatus)dto.Status.Value : order.Status;
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
        var custPayments = o.Payments.Where(p => p.PaymentMethod == PosPaymentMethod.CustomerAccount).ToList();
        var custAccountAmount = custPayments.Sum(p => p.Amount);
        var isCreditSale = custAccountAmount > 0;
        decimal advanceDeducted = 0m;
        decimal receivableCharged = 0m;

        foreach (var cp in custPayments)
        {
            if (!string.IsNullOrWhiteSpace(cp.TransactionReference) && cp.TransactionReference.StartsWith("ADV:"))
            {
                var parts = cp.TransactionReference.Split('|');
                if (parts.Length == 2 && parts[0].StartsWith("ADV:") && parts[1].StartsWith("REC:"))
                {
                    if (decimal.TryParse(parts[0][4..], out var a)) advanceDeducted += a;
                    if (decimal.TryParse(parts[1][4..], out var r)) receivableCharged += r;
                    continue;
                }
            }
            receivableCharged += cp.Amount;
        }

        var paymentStatus = o.PaymentStatus;
        string paymentStatusNameAr = paymentStatus switch
        {
            PosPaymentStatus.Paid => "مدفوع بالكامل",
            PosPaymentStatus.PaidFromAdvance => "مدفوع بالكامل من رصيد مقدم (أمانات)",
            PosPaymentStatus.CustomerAccount => "ذمم (بيع آجل بالكامل)",
            PosPaymentStatus.PartiallyPaidFromAdvance => "مدفوع جزئياً من رصيد مقدم (والباقي ذمم)",
            PosPaymentStatus.PartiallyPaid => "مدفوع جزئياً",
            PosPaymentStatus.Hospitality => "ضيافة",
            _ => "غير مدفوع"
        };
        decimal remainingAmount = o.RemainingAmount;

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
            RemainingAmount = remainingAmount,
            ChangeAmount = o.ChangeAmount,
            IsPaid = o.IsPaid,
            PaymentStatus = paymentStatus.ToString(),
            PaymentStatusCode = (int)paymentStatus,
            PaymentStatusNameAr = paymentStatusNameAr,
            IsRefund = o.IsRefund,
            OriginalOrderId = o.OriginalOrderId,
            OriginalInvoiceNumber = o.OriginalOrder?.InvoiceNumber,
            OriginalOrderNumber = o.OriginalOrder?.OrderNumber,
            RefundReason = o.RefundReason,
            EInvoiceQrCode = o.EInvoiceQrCode,
            EInvoiceHash = o.EInvoiceHash,
            CreationDate = o.CreationDate,
            CreationUser = o.CreationUser,
            IsCreditSale = isCreditSale,
            CustomerAccountAmount = custAccountAmount,
            CustomerAdvanceDeducted = advanceDeducted,
            CustomerReceivableCharged = receivableCharged,
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
        // 1. Branch-specific POS setting
        if (_branchSettingRepository != null)
        {
            try
            {
                var branchSetting = await _branchSettingRepository.GetByBranchIdAsync(branchId, ct);
                if (branchSetting?.DefaultWarehouseId != null && branchSetting.DefaultWarehouseId.Value > 0)
                {
                    return branchSetting.DefaultWarehouseId.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get branch setting for branch {BranchId}", branchId);
            }
        }

        // 2. Global setting 51
        var whSetting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultWarehouseId);
        if (whSetting != null && long.TryParse(whSetting.SettingValue, out var whId) && whId > 0)
        {
            return whId;
        }

        // 3. Active warehouse for this branch
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

        return 101; // Default fallback POS warehouse
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> AddOrderLinesAsync(long orderId, AddOrderLinesDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null || dto.Lines == null || dto.Lines.Count == 0)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("يجب تقديم صنف واحد على الأقل لإضافته للطلب.", null, 400);

        var order = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (order == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order not found", null, 404);

        if (order.IsPaid)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("لا يمكن إضافة أصناف لطلب تم سداده بالكامل (مدفوع).", null, 400);

        if (order.Status == PosOrderStatus.Completed || order.Status == PosOrderStatus.Voided || order.Status == PosOrderStatus.Refunded)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("لا يمكن تعديل أو إضافة أصناف لطلب مكتمل أو ملغي أو مسترجع.", null, 400);

        // 1. Validate mandatory modifiers for new lines
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

        // 2. Build full calculation DTO combining existing active lines + new lines
        int maxLineNumber = order.Lines.Any() ? order.Lines.Max(l => l.LineNumber) : 0;

        var combinedCalcDto = new CreatePosOrderDto
        {
            BranchId = order.BranchId,
            ShiftId = order.ShiftId,
            TillId = order.TillId,
            OrderType = (int)order.OrderType,
            CustomerId = order.CustomerId ?? 0,
            TableId = order.TableId ?? 0,
            ManualDiscountAmount = order.DiscountAmount,
            ManualDiscountPercent = order.DiscountPercent,
            ServiceChargeAmount = order.ServiceChargeAmount,
            DeliveryFee = order.DeliveryFee,
            TipAmount = order.TipAmount
        };

        // Existing lines for calc
        foreach (var exLine in order.Lines.Where(l => !l.IsVoided))
        {
            combinedCalcDto.Lines.Add(new CreatePosOrderLineDto
            {
                LineNumber = exLine.LineNumber,
                ItemId = exLine.ItemId,
                ItemCode = exLine.ItemCode,
                ItemName = exLine.ItemName,
                Quantity = exLine.Quantity,
                UomId = exLine.UomId,
                UnitPrice = exLine.UnitPrice,
                PrepStation = exLine.PrepStation,
                SpecialInstructions = exLine.SpecialInstructions
            });
        }

        // Assign new line numbers to incoming lines
        var kdsSentTime = dto.SendToKitchen ? DateTime.UtcNow : (DateTime?)null;

        foreach (var reqLine in dto.Lines)
        {
            reqLine.LineNumber = ++maxLineNumber;
            combinedCalcDto.Lines.Add(reqLine);
        }

        // Run calculation engine
        var calcResult = await _calculationEngine.CalculateOrderTotalsAsync(order.BranchId, combinedCalcDto, null, ct);

        // Append new lines with proper tax & discount
        foreach (var reqLine in dto.Lines)
        {
            var calculated = calcResult.Lines.FirstOrDefault(l => l.LineNumber == reqLine.LineNumber);
            var orderLine = new PosOrderLine
            {
                OrderId = order.Id,
                LineNumber = reqLine.LineNumber,
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
                KdsSentAt = kdsSentTime,
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

        // Recalculate order header amounts
        order.SubtotalAmount = calcResult.SubtotalAmount;
        order.DiscountAmount = calcResult.TotalDiscountAmount;
        order.TaxAmount = calcResult.TaxAmount;
        order.ServiceChargeAmount = calcResult.ServiceChargeAmount;
        order.DeliveryFee = calcResult.DeliveryFee;
        order.TipAmount = calcResult.TipAmount;
        order.TotalAmount = calcResult.TotalAmount;

        // Rebuild order taxes dynamically
        order.Taxes.Clear();
        var taxGroups = calcResult.Lines.GroupBy(l => l.TaxPercent);
        foreach (var tg in taxGroups)
        {
            var sampleItemId = tg.First().ItemId;
            var taxRateEntity = await _validationService.GetEffectiveTaxRateAsync(sampleItemId, order.BranchId, ct);
            var taxableAmt = tg.Sum(l => l.LineNetBeforeTax);
            var taxAmt = tg.Sum(l => l.TaxAmount);

            order.Taxes.Add(new PosOrderTax
            {
                OrderId = order.Id,
                TaxRateId = taxRateEntity?.Id ?? 1,
                TaxRateCode = taxRateEntity?.TaxRateCode ?? $"VAT_{tg.Key:0.#}",
                TaxPercent = tg.Key,
                TaxableAmount = taxableAmt,
                TaxAmount = taxAmt,
                IsInclusive = false
            });
        }

        // Order status
        if (dto.SendToKitchen && order.Status == PosOrderStatus.Draft)
        {
            order.Status = PosOrderStatus.SentToKitchen;
        }

        if (!string.IsNullOrWhiteSpace(dto.AdditionalNotes))
        {
            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? dto.AdditionalNotes
                : $"{order.Notes} | {dto.AdditionalNotes}";
        }

        order.UpdateUser = username;
        order.UpdateDate = DateTime.UtcNow;

        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        return await GetOrderByIdAsync(order.Id, ct);
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> UpdateOrderLineAsync(long orderId, long lineId, UpdateOrderLineDto dto, string username, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (order == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order not found", null, 404);

        if (order.IsPaid)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("لا يمكن تعديل صنف في طلب مدفوع بالكامل.", null, 400);

        if (order.Status == PosOrderStatus.Completed || order.Status == PosOrderStatus.Voided || order.Status == PosOrderStatus.Refunded)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("لا يمكن تعديل صنف في طلب مكتمل أو ملغي أو مسترجع.", null, 400);

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("Order line not found", null, 404);

        if (line.IsVoided)
            return ApiResponse<PosOrderSummaryDto>.CreateFailure("الصنف ملغي مسبقاً ولا يمكن تعديله.", null, 400);

        // Check KDS preparation status
        if (line.KdsStatus == PosKdsStatus.Preparing || line.KdsStatus == PosKdsStatus.Ready || line.KdsStatus == PosKdsStatus.Served)
        {
            string statusName = line.KdsStatus switch
            {
                PosKdsStatus.Preparing => "جاري التحضير (Preparing)",
                PosKdsStatus.Ready => "جاهز للتسليم (Ready)",
                PosKdsStatus.Served => "تم التقديم (Served)",
                _ => line.KdsStatus.ToString()
            };
            return ApiResponse<PosOrderSummaryDto>.CreateFailure(
                $"لا يمكن تعديل الصنف مباشرة لأن المطبخ بدأ بتحضيره بالفعل أو أصبح جاهزاً ({statusName}). يرجى التنسيق مع طاقم المطبخ أو إلغاء الصنف بموافقة المشرف.",
                null,
                400);
        }

        if (dto.Quantity.HasValue && dto.Quantity.Value > 0)
        {
            var oldQty = line.Quantity;
            line.Quantity = dto.Quantity.Value;
            line.LineTotal = (line.Quantity * line.UnitPrice) - line.DiscountAmount;
            if (oldQty > 0)
            {
                line.TaxAmount = (line.TaxAmount / oldQty) * line.Quantity;
            }
        }

        if (dto.SpecialInstructions != null)
        {
            line.SpecialInstructions = dto.SpecialInstructions;
        }

        // Recalculate order totals
        var linesNet = order.Lines.Where(l => !l.IsVoided).Sum(l => l.LineTotal);
        order.SubtotalAmount = linesNet;
        order.TaxAmount = order.Lines.Where(l => !l.IsVoided).Sum(l => l.TaxAmount);
        order.TotalAmount = Math.Max(0, linesNet + order.TaxAmount - order.DiscountAmount + order.ServiceChargeAmount + order.DeliveryFee + order.TipAmount);

        order.UpdateUser = username;
        order.UpdateDate = DateTime.UtcNow;

        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        return await GetOrderByIdAsync(order.Id, ct);
    }

    public async Task<ApiResponse<SplitOrderResultDto>> SplitOrderAsync(long orderId, SplitPosOrderDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null || dto.LinesToSplit == null || !dto.LinesToSplit.Any())
            return ApiResponse<SplitOrderResultDto>.CreateFailure("يجب تحديد صنف واحد على الأقل لفصله عن الفاتورة.", null, 400);

        var parentOrder = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (parentOrder == null)
            return ApiResponse<SplitOrderResultDto>.CreateFailure("الفاتورة الأصلية غير موجودة.", null, 404);

        if (parentOrder.IsPaid)
            return ApiResponse<SplitOrderResultDto>.CreateFailure("لا يمكن فصل فاتورة تم سدادها بالكامل مسبقاً.", null, 400);

        if (parentOrder.Status == PosOrderStatus.Completed || parentOrder.Status == PosOrderStatus.Voided || parentOrder.Status == PosOrderStatus.Refunded)
            return ApiResponse<SplitOrderResultDto>.CreateFailure("لا يمكن فصل فاتورة مكتملة أو ملغاة أو مسترجعة.", null, 400);

        var activeParentLines = parentOrder.Lines.Where(l => !l.IsVoided).ToList();
        if (!activeParentLines.Any())
            return ApiResponse<SplitOrderResultDto>.CreateFailure("لا توجد أصناف نشطة قابلة للفصل في الفاتورة الأصلية.", null, 400);

        // Validate lines to split
        var splitLinesData = new List<(PosOrderLine ParentLine, decimal QtyToSplit)>();
        foreach (var item in dto.LinesToSplit)
        {
            if (item.Quantity <= 0)
                return ApiResponse<SplitOrderResultDto>.CreateFailure("كمية الصنف المراد فصله يجب أن تكون أكبر من الصفر.", null, 400);

            var parentLine = activeParentLines.FirstOrDefault(l => l.Id == item.LineId);
            if (parentLine == null)
                return ApiResponse<SplitOrderResultDto>.CreateFailure($"السطر رقم ({item.LineId}) غير موجود في الفاتورة أو تم إلغاؤه.", null, 400);

            if (item.Quantity > parentLine.Quantity)
                return ApiResponse<SplitOrderResultDto>.CreateFailure($"الكمية المراد فصلها ({item.Quantity}) أكبر من الكمية المتوفرة في السطر ({parentLine.Quantity}) للصنف: {parentLine.ItemName}.", null, 400);

            splitLinesData.Add((parentLine, item.Quantity));
        }

        // Verify that not ALL active items and their full quantities are being split without leaving anything
        bool allItemsFullySplit = true;
        foreach (var pLine in activeParentLines)
        {
            var match = splitLinesData.FirstOrDefault(x => x.ParentLine.Id == pLine.Id);
            if (match.ParentLine == null || match.QtyToSplit < pLine.Quantity)
            {
                allItemsFullySplit = false;
                break;
            }
        }

        if (allItemsFullySplit)
        {
            return ApiResponse<SplitOrderResultDto>.CreateFailure("لا يمكن فصل كامل محتويات الفاتورة بالكامل؛ إذا كنت ترغب في سداد الفاتورة كاملة يرجى استخدام شاشة الدفع المباشر.", null, 400);
        }

        // Prepare Child (Split) Order DTO for Calculation Engine
        var childCalcDto = new CreatePosOrderDto
        {
            BranchId = parentOrder.BranchId,
            ShiftId = parentOrder.ShiftId,
            TillId = parentOrder.TillId,
            OrderType = (int)parentOrder.OrderType,
            Status = dto.SettleImmediately ? (int)PosOrderStatus.Completed : (int)PosOrderStatus.SentToKitchen,
            CustomerId = parentOrder.CustomerId,
            CustomerName = !string.IsNullOrWhiteSpace(dto.CustomerName) ? dto.CustomerName : parentOrder.CustomerName,
            TableId = parentOrder.TableId,
            Covers = 1,
            Notes = dto.Notes ?? $"[فاتورة مفصولة من الطلب #{parentOrder.OrderNumber}]"
        };

        int childLineIdx = 1;
        foreach (var (pLine, qtyToSplit) in splitLinesData)
        {
            var childLineDto = new CreatePosOrderLineDto
            {
                LineNumber = childLineIdx++,
                ItemId = pLine.ItemId,
                ItemCode = pLine.ItemCode,
                ItemName = pLine.ItemName,
                Quantity = qtyToSplit,
                UomId = pLine.UomId,
                UnitPrice = pLine.UnitPrice,
                DiscountPercent = pLine.DiscountPercent,
                DiscountAmount = pLine.Quantity > 0 ? (pLine.DiscountAmount / pLine.Quantity) * qtyToSplit : 0m,
                PrepStation = pLine.PrepStation,
                IsScaleItem = pLine.IsScaleItem,
                ScaleWeight = pLine.ScaleWeight,
                ScaleBarcode = pLine.ScaleBarcode,
                SalesEmployeeId = pLine.SalesEmployeeId,
                SpecialInstructions = pLine.SpecialInstructions
            };

            foreach (var mod in pLine.Modifiers)
            {
                childLineDto.Modifiers.Add(new CreatePosOrderLineModifierDto
                {
                    ModifierItemId = mod.ModifierItemId,
                    ModifierName = mod.ModifierName,
                    Quantity = mod.Quantity,
                    UnitPrice = mod.UnitPrice,
                    ExtraPrice = mod.ExtraPrice
                });
            }

            childCalcDto.Lines.Add(childLineDto);
        }

        // Run calculation engine for child order
        var childCalcResult = await _calculationEngine.CalculateOrderTotalsAsync(parentOrder.BranchId, childCalcDto, null, ct);

        // Generate numbers for Child Order
        var childOrderNumber = await _orderRepository.GenerateNextShiftOrderNumberAsync(parentOrder.ShiftId, isRefund: false, ct);
        var currentYear = DateTime.UtcNow.Year;
        var childInvoiceNumber = await _orderRepository.GenerateNextInvoiceNumberAsync(parentOrder.BranchId, currentYear, isRefund: false, ct);

        var childOrder = new PosOrderHeader
        {
            BranchId = parentOrder.BranchId,
            ShiftId = parentOrder.ShiftId,
            TillId = parentOrder.TillId,
            OrderNumber = childOrderNumber,
            InvoiceNumber = childInvoiceNumber,
            ClientUuid = Guid.NewGuid().ToString("D"),
            OrderType = parentOrder.OrderType,
            Status = dto.SettleImmediately ? PosOrderStatus.Completed : PosOrderStatus.SentToKitchen,
            CustomerId = parentOrder.CustomerId,
            CustomerName = childCalcDto.CustomerName,
            TableId = parentOrder.TableId,
            Covers = 1,
            PriceListId = parentOrder.PriceListId,

            SubtotalAmount = childCalcResult.SubtotalAmount,
            DiscountAmount = childCalcResult.TotalDiscountAmount,
            TaxAmount = childCalcResult.TaxAmount,
            ServiceChargeAmount = childCalcResult.ServiceChargeAmount,
            DeliveryFee = childCalcResult.DeliveryFee,
            TipAmount = childCalcResult.TipAmount,
            TotalAmount = childCalcResult.TotalAmount,

            Notes = childCalcDto.Notes,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        // Populate child lines
        foreach (var reqLine in childCalcDto.Lines)
        {
            var calculated = childCalcResult.Lines.FirstOrDefault(l => l.LineNumber == reqLine.LineNumber);
            var originalMatching = splitLinesData.FirstOrDefault(x => x.ParentLine.ItemId == reqLine.ItemId).ParentLine;

            var orderLine = new PosOrderLine
            {
                LineNumber = reqLine.LineNumber,
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
                KdsStatus = originalMatching?.KdsStatus ?? PosKdsStatus.Pending,
                KdsSentAt = originalMatching?.KdsSentAt ?? DateTime.UtcNow,
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

            childOrder.Lines.Add(orderLine);
        }

        // Build Child Order Taxes
        var childTaxGroups = childCalcResult.Lines.GroupBy(l => l.TaxPercent);
        foreach (var tg in childTaxGroups)
        {
            var sampleItemId = tg.First().ItemId;
            var taxRateEntity = await _validationService.GetEffectiveTaxRateAsync(sampleItemId, childOrder.BranchId, ct);
            var taxableAmt = tg.Sum(l => l.LineNetBeforeTax);
            var taxAmt = tg.Sum(l => l.TaxAmount);

            childOrder.Taxes.Add(new PosOrderTax
            {
                TaxRateId = taxRateEntity?.Id ?? 1,
                TaxRateCode = taxRateEntity?.TaxRateCode ?? $"VAT_{tg.Key:0.#}",
                TaxPercent = tg.Key,
                TaxableAmount = taxableAmt,
                TaxAmount = taxAmt,
                IsInclusive = false
            });
        }

        // Update Parent Lines (reduce qty or remove)
        foreach (var (pLine, qtyToSplit) in splitLinesData)
        {
            if (qtyToSplit >= pLine.Quantity)
            {
                // Full quantity split - remove line from parent
                parentOrder.Lines.Remove(pLine);
            }
            else
            {
                // Partial quantity split - decrement quantity
                var oldQty = pLine.Quantity;
                pLine.Quantity -= qtyToSplit;
                if (oldQty > 0)
                {
                    pLine.DiscountAmount = (pLine.DiscountAmount / oldQty) * pLine.Quantity;
                    pLine.TaxAmount = (pLine.TaxAmount / oldQty) * pLine.Quantity;
                }
                pLine.LineTotal = (pLine.Quantity * pLine.UnitPrice) - pLine.DiscountAmount;
            }
        }

        // Recalculate Parent Order Totals
        var remainingActive = parentOrder.Lines.Where(l => !l.IsVoided).ToList();
        var parentSubtotal = remainingActive.Sum(l => l.LineTotal);
        var parentTax = remainingActive.Sum(l => l.TaxAmount);
        var parentDiscount = remainingActive.Sum(l => l.DiscountAmount);

        parentOrder.SubtotalAmount = parentSubtotal;
        parentOrder.TaxAmount = parentTax;
        parentOrder.TotalAmount = Math.Max(0, parentSubtotal + parentTax + parentOrder.ServiceChargeAmount + parentOrder.DeliveryFee + parentOrder.TipAmount);

        parentOrder.Notes = string.IsNullOrWhiteSpace(parentOrder.Notes)
            ? $"[تم فصل أصناف إلى الفاتورة #{childOrder.OrderNumber}]"
            : $"{parentOrder.Notes} | [تم فصل أصناف إلى الفاتورة #{childOrder.OrderNumber}]";
        parentOrder.UpdateUser = username;
        parentOrder.UpdateDate = DateTime.UtcNow;

        // Save child order and update parent order
        await _orderRepository.AddOrderAsync(childOrder, ct);
        await _orderRepository.UpdateOrderAsync(parentOrder, ct);
        await _orderRepository.SaveChangesAsync(ct);

        // Handle Immediate Settlement if requested
        if (dto.SettleImmediately)
        {
            var shift = await _shiftRepository.GetShiftByIdAsync(childOrder.ShiftId, ct);
            var paymentsToApply = dto.Payments.Any()
                ? dto.Payments
                : new List<CreatePosOrderPaymentDto>
                {
                    new CreatePosOrderPaymentDto
                    {
                        PaymentMethod = (int)PosPaymentMethod.Cash,
                        Amount = childOrder.TotalAmount,
                        TenderedAmount = childOrder.TotalAmount
                    }
                };

            decimal totalPaid = 0m;
            decimal customerAccountAmount = 0m;
            foreach (var p in paymentsToApply)
            {
                var classification = await ResolvePaymentClassificationAsync(p.PaymentMethod, ct);
                var methodEnum = classification.IsCustomerAccount
                    ? PosPaymentMethod.CustomerAccount
                    : (classification.IsHospitality ? PosPaymentMethod.Hospitality : (PosPaymentMethod)p.PaymentMethod);
                childOrder.Payments.Add(new PosOrderPayment
                {
                    OrderId = childOrder.Id,
                    PaymentMethod = methodEnum,
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
                totalPaid += p.Amount;

                if (shift != null)
                {
                    if (classification.IsCash) shift.TotalCashSales += p.Amount;
                    else if (classification.IsCard) shift.TotalCardSales += p.Amount;
                    else if (classification.IsCustomerAccount)
                    {
                        shift.TotalCustomerAccountSales += p.Amount;
                        customerAccountAmount += p.Amount;
                    }
                    else if (classification.IsHospitality) shift.TotalHospitalitySales += p.Amount;
                    else shift.TotalOtherSales += p.Amount;
                }
            }

            childOrder.PaidAmount = totalPaid;
            childOrder.ChangeAmount = Math.Max(0, totalPaid - childOrder.TotalAmount);
            if (totalPaid >= childOrder.TotalAmount)
            {
                childOrder.IsPaid = true;
                childOrder.Status = PosOrderStatus.Completed;
            }

            await _orderRepository.UpdateOrderAsync(childOrder, ct);
            await _orderRepository.SaveChangesAsync(ct);

            if (shift != null)
            {
                await _shiftRepository.UpdateShiftAsync(shift, ct);
                await _shiftRepository.SaveChangesAsync(ct);
            }

            if (customerAccountAmount > 0)
            {
                await RecordCustomerAccountReceivableAsync(childOrder, customerAccountAmount, username, ct);
            }

            if (childOrder.IsPaid)
            {
                await DeductOrderStockIfRealtimeAsync(childOrder, ct);
            }
        }

        // Return updated summaries
        var updatedParent = await _orderRepository.GetOrderByIdAsync(parentOrder.Id, ct);
        var updatedChild = await _orderRepository.GetOrderByIdAsync(childOrder.Id, ct);

        var result = new SplitOrderResultDto
        {
            ParentOrder = MapToSummary(updatedParent!),
            SplitOrder = MapToSummary(updatedChild!)
        };

        return ApiResponse<SplitOrderResultDto>.CreateSuccess(result, "تم فصل الفاتورة بنجاح.");
    }

    public async Task<ApiResponse<ExchangeOrderResultDto>> ExchangeOrderAsync(ExchangeOrderDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<ExchangeOrderResultDto>.CreateFailure("بيانات فاتورة التبديل غير صالحة.", null, 400);

        if (dto.ReturnLines == null || !dto.ReturnLines.Any())
            return ApiResponse<ExchangeOrderResultDto>.CreateFailure("يجب تحديد صنف واحد على الأقل للإرجاع والاستبدال.", null, 400);

        if (dto.NewLines == null || !dto.NewLines.Any())
            return ApiResponse<ExchangeOrderResultDto>.CreateFailure("يجب تحديد صنف بديل جديد واحد على الأقل للشراء.", null, 400);

        var originalOrder = await _orderRepository.GetOrderByIdAsync(dto.OriginalOrderId, ct);
        if (originalOrder == null)
            return ApiResponse<ExchangeOrderResultDto>.CreateFailure("الفاتورة الأصلية غير موجودة.", null, 404);

        if (!originalOrder.IsPaid)
            return ApiResponse<ExchangeOrderResultDto>.CreateFailure("لا يمكن عمل استبدال لفاتورة غير مدفوعة مسبقاً.", null, 400);

        if (originalOrder.IsRefund)
            return ApiResponse<ExchangeOrderResultDto>.CreateFailure("لا يمكن عمل استبدال من فاتورة إرجاع.", null, 400);

        if (originalOrder.Status == PosOrderStatus.Refunded)
            return ApiResponse<ExchangeOrderResultDto>.CreateFailure("تم إرجاع هذه الفاتورة بالكامل مسبقاً، ولا يمكن عمل استبدال لها.", null, 400);

        // Fetch prior refunds to prevent over-exchanging
        var priorRefunds = await _orderRepository.GetRefundOrdersByOriginalOrderIdAsync(originalOrder.Id, ct);

        var shift = await _shiftRepository.GetShiftByIdAsync(originalOrder.ShiftId, ct);
        if (shift == null)
            return ApiResponse<ExchangeOrderResultDto>.CreateFailure("الوردية التابعة للفاتورة غير موجودة.", null, 400);

        var currentYear = DateTime.UtcNow.Year;

        // ====================================================================
        // 1. إنشاء فاتورة الإرجاع (Refund Order / Credit Note)
        // ====================================================================
        var refundOrderNumber = await _orderRepository.GenerateNextShiftOrderNumberAsync(originalOrder.ShiftId, isRefund: true, ct);
        var refundInvoiceNumber = await _orderRepository.GenerateNextInvoiceNumberAsync(originalOrder.BranchId, currentYear, isRefund: true, ct);

        var refundOrder = new PosOrderHeader
        {
            BranchId = originalOrder.BranchId,
            ShiftId = originalOrder.ShiftId,
            TillId = originalOrder.TillId,
            OrderNumber = refundOrderNumber,
            InvoiceNumber = refundInvoiceNumber,
            ClientUuid = Guid.NewGuid().ToString("D"),
            OrderType = originalOrder.OrderType,
            Status = PosOrderStatus.Refunded,
            IsRefund = true,
            OriginalOrderId = originalOrder.Id,
            RefundReason = !string.IsNullOrWhiteSpace(dto.ExchangeReason) ? dto.ExchangeReason : "استبدال أصناف (Item Exchange)",
            CustomerId = originalOrder.CustomerId,
            CustomerName = originalOrder.CustomerName,
            Notes = $"[إشعار دائن استبدال تابع للفاتورة #{originalOrder.OrderNumber}]" + (!string.IsNullOrWhiteSpace(dto.Notes) ? $" - {dto.Notes}" : ""),
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        decimal returnedSubtotal = 0m;
        decimal returnedTax = 0m;

        foreach (var refLine in dto.ReturnLines)
        {
            var origLine = originalOrder.Lines.FirstOrDefault(l => l.Id == refLine.OrderLineId && !l.IsVoided);
            if (origLine == null)
                return ApiResponse<ExchangeOrderResultDto>.CreateFailure($"السطر رقم ({refLine.OrderLineId}) غير موجود في الفاتورة الأصلية.", null, 400);

            if (refLine.QuantityToRefund <= 0)
                return ApiResponse<ExchangeOrderResultDto>.CreateFailure("كمية الصنف المرتجع يجب أن تكون أكبر من الصفر.", null, 400);

            var alreadyRefunded = priorRefunds.SelectMany(r => r.Lines)
                .Where(rl => rl.LineNumber == origLine.LineNumber && rl.ItemId == origLine.ItemId)
                .Sum(rl => Math.Abs(rl.Quantity));
            var origQtyAbs = Math.Abs(origLine.Quantity);
            var remainingRefundable = Math.Max(0m, origQtyAbs - alreadyRefunded);

            if (remainingRefundable <= 0)
                return ApiResponse<ExchangeOrderResultDto>.CreateFailure($"الصنف ({origLine.ItemName}) تم إرجاعه بالكامل مسبقاً، ولا توجد كميات قابلة للاستبدال.", null, 400);

            if (refLine.QuantityToRefund > remainingRefundable)
                return ApiResponse<ExchangeOrderResultDto>.CreateFailure($"كمية الإرجاع المطلوبة ({refLine.QuantityToRefund:G29}) للصنف ({origLine.ItemName}) أكبر من الكمية المتبقية القابلة للإرجاع ({remainingRefundable:G29}). تم إرجاع {alreadyRefunded:G29} سابقاً من أصل {origQtyAbs:G29}.", null, 400);

            var qty = refLine.QuantityToRefund;
            var origQtySafe = origQtyAbs > 0 ? origQtyAbs : 1m;
            var ratio = qty / origQtySafe;
            var modExtraPerUnit = origLine.Modifiers.Sum(m => m.Quantity * m.ExtraPrice) / origQtySafe;
            var discountPerUnit = origLine.DiscountAmount / origQtySafe;
            var netUnitPrice = origLine.UnitPrice + modExtraPerUnit - discountPerUnit;
            var lineSubtotal = qty * netUnitPrice;
            var lineTax = qty * (origLine.TaxAmount / origQtySafe);
            var lineDiscount = origLine.DiscountAmount * ratio;
            var lineTotal = lineSubtotal + lineTax;

            returnedSubtotal += lineSubtotal;
            returnedTax += lineTax;

            var returnOrderLine = new PosOrderLine
            {
                LineNumber = origLine.LineNumber,
                ItemId = origLine.ItemId,
                ItemCode = origLine.ItemCode,
                ItemName = origLine.ItemName,
                Quantity = -qty, // Negative quantity for credit note
                UomId = origLine.UomId,
                UnitPrice = origLine.UnitPrice,
                DiscountAmount = -lineDiscount,
                DiscountPercent = origLine.DiscountPercent,
                LineTotal = -lineTotal,
                TaxAmount = -lineTax,
                TaxPercent = origLine.TaxPercent
            };

            foreach (var origMod in origLine.Modifiers)
            {
                returnOrderLine.Modifiers.Add(new PosOrderLineModifier
                {
                    ModifierItemId = origMod.ModifierItemId,
                    ModifierName = origMod.ModifierName,
                    Quantity = origMod.Quantity * ratio,
                    UnitPrice = origMod.UnitPrice,
                    ExtraPrice = origMod.ExtraPrice
                });
            }

            refundOrder.Lines.Add(returnOrderLine);
        }

        var totalReturnedAmount = returnedSubtotal + returnedTax;
        refundOrder.SubtotalAmount = -returnedSubtotal;
        refundOrder.TaxAmount = -returnedTax;
        refundOrder.TotalAmount = -totalReturnedAmount;
        refundOrder.PaidAmount = -totalReturnedAmount;
        refundOrder.IsPaid = true;

        await _orderRepository.AddOrderAsync(refundOrder, ct);
        await _orderRepository.SaveChangesAsync(ct);

        if (dto.ReturnToInventory)
        {
            await RestockRefundOrderIfRealtimeAsync(refundOrder, originalOrder, ct);
        }

        // ====================================================================
        // 2. إنشاء فاتورة المبيعات الجديدة (New Sales Order)
        // ====================================================================
        var createNewOrderDto = new CreatePosOrderDto
        {
            BranchId = originalOrder.BranchId,
            ShiftId = originalOrder.ShiftId,
            TillId = originalOrder.TillId,
            OrderType = (int)originalOrder.OrderType,
            Status = (int)PosOrderStatus.Completed,
            CustomerId = originalOrder.CustomerId,
            CustomerName = originalOrder.CustomerName,
            TableId = originalOrder.TableId,
            Notes = $"[فاتورة استبدال أصناف من الفاتورة #{originalOrder.OrderNumber} وإشعار دائن #{refundOrder.OrderNumber}]" + (!string.IsNullOrWhiteSpace(dto.Notes) ? $" - {dto.Notes}" : ""),
            Lines = dto.NewLines
        };

        var newCalcResult = await _calculationEngine.CalculateOrderTotalsAsync(originalOrder.BranchId, createNewOrderDto, null, ct);

        var newSalesOrderNumber = await _orderRepository.GenerateNextShiftOrderNumberAsync(originalOrder.ShiftId, isRefund: false, ct);
        var newSalesInvoiceNumber = await _orderRepository.GenerateNextInvoiceNumberAsync(originalOrder.BranchId, currentYear, isRefund: false, ct);

        var newSalesOrder = new PosOrderHeader
        {
            BranchId = originalOrder.BranchId,
            ShiftId = originalOrder.ShiftId,
            TillId = originalOrder.TillId,
            OrderNumber = newSalesOrderNumber,
            InvoiceNumber = newSalesInvoiceNumber,
            ClientUuid = Guid.NewGuid().ToString("D"),
            OrderType = originalOrder.OrderType,
            Status = PosOrderStatus.Completed,
            CustomerId = originalOrder.CustomerId,
            CustomerName = originalOrder.CustomerName,
            TableId = originalOrder.TableId,
            PriceListId = originalOrder.PriceListId,

            SubtotalAmount = newCalcResult.SubtotalAmount,
            DiscountAmount = newCalcResult.TotalDiscountAmount,
            TaxAmount = newCalcResult.TaxAmount,
            ServiceChargeAmount = newCalcResult.ServiceChargeAmount,
            DeliveryFee = newCalcResult.DeliveryFee,
            TipAmount = newCalcResult.TipAmount,
            TotalAmount = newCalcResult.TotalAmount,

            Notes = createNewOrderDto.Notes,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        // Populate new sales lines
        int salesLineIdx = 1;
        foreach (var reqLine in createNewOrderDto.Lines)
        {
            reqLine.LineNumber = salesLineIdx++;
            var calculated = newCalcResult.Lines.FirstOrDefault(l => l.LineNumber == reqLine.LineNumber);

            var orderLine = new PosOrderLine
            {
                LineNumber = reqLine.LineNumber,
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
                KdsSentAt = DateTime.UtcNow,
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

            newSalesOrder.Lines.Add(orderLine);
        }

        // Build new order taxes dynamically
        var newTaxGroups = newCalcResult.Lines.GroupBy(l => l.TaxPercent);
        foreach (var tg in newTaxGroups)
        {
            var sampleItemId = tg.First().ItemId;
            var taxRateEntity = await _validationService.GetEffectiveTaxRateAsync(sampleItemId, newSalesOrder.BranchId, ct);
            var taxableAmt = tg.Sum(l => l.LineNetBeforeTax);
            var taxAmt = tg.Sum(l => l.TaxAmount);

            newSalesOrder.Taxes.Add(new PosOrderTax
            {
                TaxRateId = taxRateEntity?.Id ?? 1,
                TaxRateCode = taxRateEntity?.TaxRateCode ?? $"VAT_{tg.Key:0.#}",
                TaxPercent = tg.Key,
                TaxableAmount = taxableAmt,
                TaxAmount = taxAmt,
                IsInclusive = false
            });
        }

        // ====================================================================
        // 3. تسوية الدفعات وفارق السعر (Payment & Net Settlement)
        // ====================================================================
        var totalNewAmount = newSalesOrder.TotalAmount;
        var netDifference = totalNewAmount - totalReturnedAmount; // الموجب يدفعه الزبون، السالب يُعاد له

        // إضافة دفعة مقاصة برصيد المرتجع (Exchange Credit Tender)
        var creditApplied = Math.Min(totalReturnedAmount, totalNewAmount);
        newSalesOrder.Payments.Add(new PosOrderPayment
        {
            PaymentMethod = PosPaymentMethod.Split, // مقاصة استبدال / رصيد دائن
            Amount = creditApplied,
            TenderedAmount = creditApplied,
            ChangeAmount = 0m,
            TransactionReference = $"EXCHANGE-CREDIT-#{refundOrder.OrderNumber}",
            PaymentDate = DateTime.UtcNow,
            CreationUser = username
        });

        decimal paidDifference = 0m;
        decimal changeDue = 0m;
        decimal diffCustomerAccountAmount = 0m;

        if (netDifference > 0)
        {
            // الزبون يحتاج لدفع الفارق الإضافي
            var diffPayments = dto.DifferencePayments.Any()
                ? dto.DifferencePayments
                : new List<CreatePosOrderPaymentDto>
                {
                    new CreatePosOrderPaymentDto
                    {
                        PaymentMethod = (int)PosPaymentMethod.Cash,
                        Amount = netDifference,
                        TenderedAmount = netDifference
                    }
                };

            foreach (var p in diffPayments)
            {
                var classification = await ResolvePaymentClassificationAsync(p.PaymentMethod, ct);
                var methodEnum = classification.IsCustomerAccount
                    ? PosPaymentMethod.CustomerAccount
                    : (classification.IsHospitality ? PosPaymentMethod.Hospitality : (PosPaymentMethod)p.PaymentMethod);
                newSalesOrder.Payments.Add(new PosOrderPayment
                {
                    PaymentMethod = methodEnum,
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
                paidDifference += p.Amount;

                // Update shift tenders
                if (classification.IsCash) shift.TotalCashSales += p.Amount;
                else if (classification.IsCard) shift.TotalCardSales += p.Amount;
                else if (classification.IsCustomerAccount)
                {
                    shift.TotalCustomerAccountSales += p.Amount;
                    diffCustomerAccountAmount += p.Amount;
                }
                else shift.TotalOtherSales += p.Amount;
            }
        }
        else if (netDifference < 0)
        {
            // الصنف الجديد أرخص: يُعاد للزبون الفرق
            changeDue = Math.Abs(netDifference);
            // تسجيل استرجاع نقدي للفرق في الوردية
            shift.TotalCashRefunds += changeDue;
        }

        newSalesOrder.PaidAmount = creditApplied + paidDifference;
        newSalesOrder.ChangeAmount = changeDue;
        newSalesOrder.IsPaid = true;

        await _orderRepository.AddOrderAsync(newSalesOrder, ct);
        await _orderRepository.SaveChangesAsync(ct);
        await _shiftRepository.UpdateShiftAsync(shift, ct);
        await _shiftRepository.SaveChangesAsync(ct);

        if (diffCustomerAccountAmount > 0)
        {
            await RecordCustomerAccountReceivableAsync(newSalesOrder, diffCustomerAccountAmount, username, ct);
        }

        // Deduct inventory for new items
        await DeductOrderStockIfRealtimeAsync(newSalesOrder, ct);

        // Check if all lines of the original order are now fully refunded
        bool allLinesFullyRefunded = originalOrder.Lines.Where(l => !l.IsVoided).All(l =>
        {
            var alreadyRefunded = priorRefunds.SelectMany(r => r.Lines)
                .Where(rl => rl.LineNumber == l.LineNumber && rl.ItemId == l.ItemId)
                .Sum(rl => Math.Abs(rl.Quantity));
            var newlyRefunded = refundOrder.Lines
                .Where(rl => rl.LineNumber == l.LineNumber && rl.ItemId == l.ItemId)
                .Sum(rl => Math.Abs(rl.Quantity));
            return (alreadyRefunded + newlyRefunded) >= Math.Abs(l.Quantity);
        });

        if (allLinesFullyRefunded)
        {
            originalOrder.Status = PosOrderStatus.Refunded;
            originalOrder.UpdateUser = username;
            originalOrder.UpdateDate = DateTime.UtcNow;
            await _orderRepository.UpdateOrderAsync(originalOrder, ct);
            await _orderRepository.SaveChangesAsync(ct);
        }

        // Log audit trail
        await _auditService.LogRefundOrderAsync(
            originalOrder.BranchId,
            originalOrder.Id,
            refundOrder.Id,
            totalReturnedAmount,
            $"Item Exchange to Order #{newSalesOrder.OrderNumber}",
            null,
            username,
            ct);

        var refreshedRefund = await _orderRepository.GetOrderByIdAsync(refundOrder.Id, ct);
        var refreshedNewSales = await _orderRepository.GetOrderByIdAsync(newSalesOrder.Id, ct);

        var result = new ExchangeOrderResultDto
        {
            RefundOrder = MapToSummary(refreshedRefund!),
            NewSalesOrder = MapToSummary(refreshedNewSales!),
            ReturnedTotalAmount = totalReturnedAmount,
            NewSalesTotalAmount = totalNewAmount,
            NetDifferenceAmount = netDifference,
            PaidDifferenceAmount = paidDifference,
            ChangeDueToCustomer = changeDue
        };

        return ApiResponse<ExchangeOrderResultDto>.CreateSuccess(
            result, 
            netDifference >= 0 
                ? $"تمت عملية الاستبدال بنجاح. الفارق الإضافي المسدد: {paidDifference:0.00} ريال."
                : $"تمت عملية الاستبدال بنجاح. المبلغ المستحق إرجاعه للزبون: {changeDue:0.00} ريال.");
    }

    private sealed record PaymentClassification(
        bool IsCash,
        bool IsCard,
        bool IsCustomerAccount,
        bool IsHospitality,
        string? GlAccountCode,
        string? MethodCode);

    private async Task<PaymentClassification> ResolvePaymentClassificationAsync(int paymentMethodCode, CancellationToken ct = default)
    {
        if (paymentMethodCode == (int)PosPaymentMethod.Cash)
            return new PaymentClassification(IsCash: true, IsCard: false, IsCustomerAccount: false, IsHospitality: false, GlAccountCode: "111101", MethodCode: "CASH");

        if (paymentMethodCode == (int)PosPaymentMethod.Card || paymentMethodCode == (int)PosPaymentMethod.DigitalWallet)
            return new PaymentClassification(IsCash: false, IsCard: true, IsCustomerAccount: false, IsHospitality: false, GlAccountCode: "111201", MethodCode: "CARD");

        if (paymentMethodCode == (int)PosPaymentMethod.CustomerAccount || paymentMethodCode == 99 || paymentMethodCode == 4 || paymentMethodCode == 43)
            return new PaymentClassification(IsCash: false, IsCard: false, IsCustomerAccount: true, IsHospitality: false, GlAccountCode: "112101", MethodCode: "CUSTOMER_ACCOUNT");

        if (paymentMethodCode == (int)PosPaymentMethod.Hospitality || paymentMethodCode == 98 || paymentMethodCode == 11 || paymentMethodCode == 41)
            return new PaymentClassification(IsCash: false, IsCard: false, IsCustomerAccount: false, IsHospitality: true, GlAccountCode: "632102", MethodCode: "HOSPITALITY");

        if (_paymentMethodRepository != null)
        {
            try
            {
                var pm = await _paymentMethodRepository.GetByIdAsync(paymentMethodCode, ct);
                if (pm != null)
                {
                    var codeUpper = pm.Code?.Trim().ToUpperInvariant() ?? string.Empty;
                    var typeUpper = pm.MethodType?.Trim().ToUpperInvariant() ?? string.Empty;

                    bool isCustAcc = codeUpper == "CUSTOMER_ACCOUNT" ||
                                     typeUpper is "ACCOUNT" or "CUSTOMER_ACCOUNT" or "CREDIT_ACCOUNT" or "CREDIT";

                    bool isCash = !isCustAcc && (codeUpper == "CASH" || typeUpper == "CASH");

                    bool isCard = !isCustAcc && !isCash && (
                        codeUpper is "CARD" or "DIGITAL_WALLET" or "MADA" or "VISA" or "MASTERCARD" ||
                        typeUpper is "CARD" or "DIGITAL_WALLET" or "BANK");

                    bool isHosp = !isCustAcc && !isCash && !isCard && (codeUpper == "HOSPITALITY" || typeUpper == "HOSPITALITY");

                    return new PaymentClassification(
                        IsCash: isCash,
                        IsCard: isCard,
                        IsCustomerAccount: isCustAcc,
                        IsHospitality: isHosp,
                        GlAccountCode: pm.GlAccountCode,
                        MethodCode: pm.Code);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve payment method ID {PaymentMethodId} from repository", paymentMethodCode);
            }
        }

        return new PaymentClassification(IsCash: false, IsCard: false, IsCustomerAccount: false, IsHospitality: false, GlAccountCode: null, MethodCode: null);
    }

    private async Task<(bool Success, string? ErrorMessage, decimal AdvanceDeducted, decimal ReceivableCharged, decimal CurrentBalance, decimal? AvailableCredit)> ValidateAndCalculateCustomerAccountPaymentAsync(
        long customerId,
        decimal totalCustAccountPayment,
        CancellationToken ct)
    {
        if (_customerRepository == null)
            return (true, null, 0m, totalCustAccountPayment, 0m, null);

        var customer = await _customerRepository.GetByIdAsync(customerId, ct);
        if (customer == null || !customer.IsActive)
            return (false, "العميل المحدد غير موجود أو غير مفعّل في النظام.", 0m, 0m, 0m, null);

        decimal currentBalance = 0m;
        if (_arSubledgerRepository != null && !string.IsNullOrWhiteSpace(customer.CustomerCode))
        {
            currentBalance = await _arSubledgerRepository.GetCustomerBalanceAsync(customer.CustomerCode, ct);
        }

        decimal advanceDeducted = 0m;
        decimal receivableCharged = totalCustAccountPayment;

        // إذا كان للعميل رصيد دائن مسبق (currentBalance < 0: له رصيد / دفعات مقدمة)
        if (currentBalance < 0)
        {
            decimal availableAdvance = Math.Abs(currentBalance);
            advanceDeducted = Math.Min(totalCustAccountPayment, availableAdvance);
            receivableCharged = totalCustAccountPayment - advanceDeducted;
        }

        decimal? availableCredit = customer.CreditLimit.HasValue
            ? customer.CreditLimit.Value - currentBalance
            : null;

        // التحقق من السقف الائتماني إذا كان جزء من المبلغ سيُسجل كذمة مدينة جديدة
        if (customer.CreditLimit.HasValue && receivableCharged > 0)
        {
            decimal projectedBalance = currentBalance + totalCustAccountPayment;
            if (projectedBalance > customer.CreditLimit.Value)
            {
                var remainingCredit = Math.Max(0, customer.CreditLimit.Value - currentBalance);
                var errorMsg = $"لا يمكن إتمام عملية البيع على الحساب. رصيد العميل الحالي ({currentBalance:N2}) مع قيمة الفاتورة ({totalCustAccountPayment:N2}) يتجاوز السقف الائتماني المسموح به ({customer.CreditLimit.Value:N2}). المتاح للشراء الآجل: ({remainingCredit:N2}).";
                return (false, errorMsg, advanceDeducted, receivableCharged, currentBalance, availableCredit);
            }
        }

        return (true, null, advanceDeducted, receivableCharged, currentBalance, availableCredit);
    }

    private async Task RecordCustomerAccountReceivableAsync(
        PosOrderHeader order,
        decimal amount,
        string username,
        CancellationToken ct,
        decimal? advanceDeducted = null,
        decimal? receivableCharged = null)
    {
        if (amount <= 0 || !order.CustomerId.HasValue || order.CustomerId.Value <= 0)
            return;

        try
        {
            Customer? customer = null;
            if (_customerRepository != null)
            {
                customer = await _customerRepository.GetByIdAsync(order.CustomerId.Value, ct);
            }

            if (customer == null || string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                _logger.LogWarning("Customer {CustomerId} not found in repository. Skipping AR posting for order {OrderId}.", order.CustomerId.Value, order.Id);
                return;
            }

            // If advanceDeducted / receivableCharged not supplied by caller, resolve them
            if (!advanceDeducted.HasValue || !receivableCharged.HasValue)
            {
                var curBal = _arSubledgerRepository != null
                    ? await _arSubledgerRepository.GetCustomerBalanceAsync(customer.CustomerCode, ct)
                    : 0m;
                if (curBal < 0)
                {
                    var avail = Math.Abs(curBal);
                    advanceDeducted = Math.Min(amount, avail);
                    receivableCharged = amount - advanceDeducted.Value;
                }
                else
                {
                    advanceDeducted = 0m;
                    receivableCharged = amount;
                }
            }

            var arAccount = !string.IsNullOrWhiteSpace(customer.ArControlAccountCode) ? customer.ArControlAccountCode : "112101";
            var advanceAccount = "212101"; // أمانات ودفعات مقدمة من العملاء
            var revAccount = "411101";

            if (_voucherService != null && _fiscalPeriodRepository != null)
            {
                try
                {
                    var period = await _fiscalPeriodRepository.GetPeriodByDateOnlyAsync(DateTime.UtcNow, ct);
                    var fiscalYearId = period?.FiscalYearId ?? 1;

                    var desc = $"فاتورة بيع POS - طلب #{order.OrderNumber} ({order.InvoiceNumber}) - عميل {customer.CustomerCode}";

                    var details = new List<CreateGlVoucherDetailDto>();

                    if (advanceDeducted.Value > 0)
                    {
                        details.Add(new CreateGlVoucherDetailDto
                        {
                            AccountCode = advanceAccount,
                            Debit = advanceDeducted.Value,
                            Credit = 0,
                            Description = $"استهلاك رصيد مسبق / دفعة مقدمة - طلب #{order.OrderNumber} ({order.InvoiceNumber})",
                            PartyType = "CUSTOMER",
                            PartyCode = customer.CustomerCode,
                            CurrencyId = 1,
                            ExchangeRate = 1.0m,
                            BranchId = order.BranchId
                        });
                    }

                    if (receivableCharged.Value > 0)
                    {
                        details.Add(new CreateGlVoucherDetailDto
                        {
                            AccountCode = arAccount,
                            Debit = receivableCharged.Value,
                            Credit = 0,
                            Description = $"فاتورة بيع آجل (ذمم) - طلب #{order.OrderNumber} ({order.InvoiceNumber})",
                            PartyType = "CUSTOMER",
                            PartyCode = customer.CustomerCode,
                            CurrencyId = 1,
                            ExchangeRate = 1.0m,
                            BranchId = order.BranchId
                        });
                    }

                    details.Add(new CreateGlVoucherDetailDto
                    {
                        AccountCode = revAccount,
                        Debit = 0,
                        Credit = amount,
                        Description = desc,
                        CurrencyId = 1,
                        ExchangeRate = 1.0m,
                        BranchId = order.BranchId
                    });

                    var voucherDto = new CreateGlVoucherDto
                    {
                        BranchId = order.BranchId,
                        FiscalYearId = fiscalYearId,
                        VoucherType = 101, // قيد يومية عام
                        VoucherDate = DateTime.UtcNow,
                        Description = desc,
                        Details = details
                    };

                    var voucher = await _voucherService.CreateVoucherAsync(voucherDto, username, ct);
                    await _voucherService.PostVoucherAsync(voucher.Id, username, ct);
                    _logger.LogInformation("Posted GL Voucher #{VoucherNo} and AR Subledger transaction for customer {CustomerCode}, Order #{OrderNumber}, Amount: {Amount} (Adv: {Adv}, Rec: {Rec})",
                        voucher.VoucherNo, customer.CustomerCode, order.OrderNumber, amount, advanceDeducted.Value, receivableCharged.Value);
                    return;
                }
                catch (Exception glEx)
                {
                    _logger.LogWarning(glEx, "GL Voucher posting failed for Order #{OrderId}, falling back to direct AR subledger transaction", order.Id);
                }
            }

            if (_arSubledgerRepository != null)
            {
                await _arSubledgerRepository.AddTransactionAsync(new ArSubledgerTransaction
                {
                    CustomerCode = customer.CustomerCode,
                    JournalLineId = 0,
                    VoucherId = 0,
                    TransactionType = "INVOICE",
                    TransactionDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(customer.PaymentTermsDays > 0 ? customer.PaymentTermsDays : 30),
                    Amount = amount,
                    CurrencyId = 1,
                    ExchangeRate = 1.0m,
                    LocalAmount = amount,
                    OpenAmount = amount,
                    LocalOpenAmount = amount,
                    ReferenceNo = order.InvoiceNumber ?? order.OrderNumber,
                    Description = $"فاتورة بيع آجل POS - طلب #{order.OrderNumber} ({order.InvoiceNumber})",
                    CreationUser = username,
                    CreationDate = DateTime.UtcNow
                }, ct);
                await _arSubledgerRepository.SaveChangesAsync(ct);
                _logger.LogInformation("Direct AR Subledger transaction recorded for customer {CustomerCode}, Order #{OrderNumber}, Amount: {Amount}",
                    customer.CustomerCode, order.OrderNumber, amount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record customer account receivable for Order #{OrderId}, CustomerId: {CustomerId}", order.Id, order.CustomerId);
        }
    }

    private async Task RecordCustomerAccountRefundCreditAsync(
        PosOrderHeader refundOrder,
        PosOrderHeader originalOrder,
        decimal amount,
        string username,
        CancellationToken ct)
    {
        if (amount <= 0 || !originalOrder.CustomerId.HasValue || originalOrder.CustomerId.Value <= 0)
            return;

        try
        {
            Customer? customer = null;
            if (_customerRepository != null)
            {
                customer = await _customerRepository.GetByIdAsync(originalOrder.CustomerId.Value, ct);
            }

            if (customer == null || string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                _logger.LogWarning("Customer {CustomerId} not found in repository. Skipping AR refund posting for order {OrderId}.", originalOrder.CustomerId.Value, refundOrder.Id);
                return;
            }

            var arAccount = !string.IsNullOrWhiteSpace(customer.ArControlAccountCode) ? customer.ArControlAccountCode : "112101";
            var revAccount = "411101";

            if (_voucherService != null && _fiscalPeriodRepository != null)
            {
                try
                {
                    var period = await _fiscalPeriodRepository.GetPeriodByDateOnlyAsync(DateTime.UtcNow, ct);
                    var fiscalYearId = period?.FiscalYearId ?? 1;

                    var desc = $"إرجاع مبيعات آجل POS - مرتجع #{refundOrder.OrderNumber} للأصل #{originalOrder.OrderNumber} - عميل {customer.CustomerCode}";

                    var voucherDto = new CreateGlVoucherDto
                    {
                        BranchId = refundOrder.BranchId,
                        FiscalYearId = fiscalYearId,
                        VoucherType = 101, // قيد يومية عام
                        VoucherDate = DateTime.UtcNow,
                        Description = desc,
                        Details = new List<CreateGlVoucherDetailDto>
                        {
                            // سطر مدين: تخفيض إيراد المبيعات (أو مردودات مبيعات)
                            new()
                            {
                                AccountCode = revAccount,
                                Debit = amount,
                                Credit = 0,
                                Description = desc,
                                CurrencyId = 1,
                                ExchangeRate = 1.0m,
                                BranchId = refundOrder.BranchId
                            },
                            // سطر دائن: تخفيض ذمة العميل (AR Control)
                            new()
                            {
                                AccountCode = arAccount,
                                Debit = 0,
                                Credit = amount,
                                Description = desc,
                                PartyType = "CUSTOMER",
                                PartyCode = customer.CustomerCode,
                                CurrencyId = 1,
                                ExchangeRate = 1.0m,
                                BranchId = refundOrder.BranchId
                            }
                        }
                    };

                    var voucher = await _voucherService.CreateVoucherAsync(voucherDto, username, ct);
                    await _voucherService.PostVoucherAsync(voucher.Id, username, ct);
                    _logger.LogInformation("Posted GL Voucher #{VoucherNo} and AR Credit transaction for customer {CustomerCode}, Refund #{RefundNumber}, Amount: {Amount}",
                        voucher.VoucherNo, customer.CustomerCode, refundOrder.OrderNumber, amount);
                    return;
                }
                catch (Exception glEx)
                {
                    _logger.LogWarning(glEx, "GL Voucher refund posting failed for Refund #{RefundId}, falling back to direct AR subledger transaction", refundOrder.Id);
                }
            }

            if (_arSubledgerRepository != null)
            {
                await _arSubledgerRepository.AddTransactionAsync(new ArSubledgerTransaction
                {
                    CustomerCode = customer.CustomerCode,
                    JournalLineId = 0,
                    VoucherId = 0,
                    TransactionType = "RECEIPT",
                    TransactionDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow,
                    Amount = -amount,
                    CurrencyId = 1,
                    ExchangeRate = 1.0m,
                    LocalAmount = -amount,
                    OpenAmount = -amount,
                    LocalOpenAmount = -amount,
                    ReferenceNo = refundOrder.InvoiceNumber ?? refundOrder.OrderNumber,
                    Description = $"إرجاع مبيعات آجل POS - طلب #{refundOrder.OrderNumber}",
                    CreationUser = username,
                    CreationDate = DateTime.UtcNow
                }, ct);
                await _arSubledgerRepository.SaveChangesAsync(ct);
                _logger.LogInformation("Direct AR Subledger credit transaction recorded for customer {CustomerCode}, Refund #{RefundNumber}, Amount: {Amount}",
                    customer.CustomerCode, refundOrder.OrderNumber, amount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record customer account refund credit for Refund #{RefundId}, Original Order: {OrderId}", refundOrder.Id, originalOrder.Id);
        }
    }
}

