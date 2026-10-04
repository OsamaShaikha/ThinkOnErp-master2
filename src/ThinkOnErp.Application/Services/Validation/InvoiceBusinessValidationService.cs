using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Validation;

public class InvoiceBusinessValidationService : IInvoiceBusinessValidationService
{
    private readonly IGlFiscalPeriodRepository _fiscalPeriodRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IArSubledgerRepository _arSubledgerRepository;
    private readonly ITrxTypeRepository _trxTypeRepository;
    private readonly IInvItemRepository _itemRepository;
    private readonly IInvStockBalanceRepository _stockBalanceRepository;
    private readonly IInvPriceListRepository _priceListRepository;
    private readonly ITaxRepository _taxRepository;
    private readonly IPosShiftRepository _shiftRepository;
    private readonly ILogger<InvoiceBusinessValidationService> _logger;

    public InvoiceBusinessValidationService(
        IGlFiscalPeriodRepository fiscalPeriodRepository,
        ICustomerRepository customerRepository,
        IArSubledgerRepository arSubledgerRepository,
        ITrxTypeRepository trxTypeRepository,
        IInvItemRepository itemRepository,
        IInvStockBalanceRepository stockBalanceRepository,
        IInvPriceListRepository priceListRepository,
        ITaxRepository taxRepository,
        IPosShiftRepository shiftRepository,
        ILogger<InvoiceBusinessValidationService> logger)
    {
        _fiscalPeriodRepository = fiscalPeriodRepository;
        _customerRepository = customerRepository;
        _arSubledgerRepository = arSubledgerRepository;
        _trxTypeRepository = trxTypeRepository;
        _itemRepository = itemRepository;
        _stockBalanceRepository = stockBalanceRepository;
        _priceListRepository = priceListRepository;
        _taxRepository = taxRepository;
        _shiftRepository = shiftRepository;
        _logger = logger;
    }

    public async Task<TaxRate?> GetEffectiveTaxRateAsync(long itemId, long branchId, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, ct);
        if (item == null || item.IsTaxExempt)
            return null;

        // 1. Direct TaxRate assigned to item
        if (item.TaxRateId.HasValue)
        {
            var directRate = await _taxRepository.GetTaxRateByIdAsync(item.TaxRateId.Value, ct);
            if (directRate != null && directRate.IsActive)
                return directRate;
        }

        // 2. Active Tax Rates from master tax repository
        var activeRates = await _taxRepository.GetTaxRatesAsync(false, ct);
        return activeRates.FirstOrDefault(r => r.IsActive);
    }

    public async Task<decimal> GetEffectiveTaxRatePercentAsync(long itemId, long branchId, CancellationToken ct = default)
    {
        var rate = await GetEffectiveTaxRateAsync(itemId, branchId, ct);
        return rate?.RatePercent ?? 0m;
    }

    public async Task<BusinessValidationResult> ValidateWarehouseDocumentDtoAsync(CreateTrxDocumentDto dto, CancellationToken ct = default)
    {
        var result = new BusinessValidationResult();

        if (dto.Lines == null || dto.Lines.Count == 0)
        {
            result.AddError("Lines", "يجب أن يحتوي المستند على سطر صنف واحد على الأقل.", ErrorCodes.FieldRequired);
            return result;
        }

        // 1. Fiscal Period Validation
        await ValidateFiscalPeriodAsync(dto.DocDate, result, ct);

        // 2. Document & Transaction Type Validation
        var docType = await _trxTypeRepository.GetDocTypeAsync(dto.DocType, ct);
        var trxType = await _trxTypeRepository.GetTrxTypeAsync(dto.TrxType, ct);

        if (docType == null || !docType.IsActive)
        {
            result.AddError("DocType", $"نوع المستند ({dto.DocType}) غير صالح أو غير نشط.", ErrorCodes.DocTypeRequired);
            return result;
        }

        if (trxType == null || !trxType.IsActive)
        {
            result.AddError("TrxType", $"نوع المعاملة ({dto.TrxType}) غير صالح أو غير نشط.", ErrorCodes.TrxTypeRequired);
            return result;
        }

        // 2.5 Auto-determine Stock Direction for lines if unified Quantity is supplied
        if (trxType.AffectsStock)
        {
            foreach (var l in dto.Lines)
            {
                if (l.Quantity.HasValue && l.Quantity.Value > 0)
                {
                    if (trxType.StockDirection < 0)
                    {
                        l.QuantityOut = l.Quantity.Value;
                        l.QuantityIn = 0;
                    }
                    else if (trxType.StockDirection > 0)
                    {
                        l.QuantityIn = l.Quantity.Value;
                        l.QuantityOut = 0;
                    }
                    else if (dto.DocType == 350) // Internal Warehouse Transfer
                    {
                        l.QuantityOut = l.Quantity.Value;
                        l.QuantityIn = l.Quantity.Value;
                    }
                }
            }
        }

        // 3. Party & Credit Limit Validation
        if (trxType.RequiresParty && (!dto.PartyId.HasValue || dto.PartyId.Value <= 0))
        {
            result.AddError("PartyId", "تحديد الطرف التجاري (العميل أو المورد) إلزامي لهذا النوع من المعاملات.", ErrorCodes.FieldRequired);
        }

        decimal documentNetTotal = dto.Lines.Sum(l =>
        {
            var qty = l.QuantityIn > 0 ? l.QuantityIn : l.QuantityOut;
            return (qty * l.UnitPrice) - l.DiscountAmount;
        }) - dto.DiscountAmount;

        if (trxType.AffectsPartyBalance && dto.PartyId.HasValue)
        {
            await ValidateCustomerCreditLimitAsync(dto.PartyId.Value, documentNetTotal, result, ct);
        }

        // 4. Warehouse & Stock & Pricing Checks for each line
        long warehouseId = trxType.StockDirection > 0
            ? (dto.ToWarehouseId ?? 1)
            : (dto.FromWarehouseId ?? 1);

        int lineIdx = 0;
        foreach (var line in dto.Lines)
        {
            lineIdx++;
            await ValidateWarehouseLineAsync(line, lineIdx, trxType, warehouseId, result, ct);
        }

        return result;
    }

    public async Task<BusinessValidationResult> ValidateWarehouseDocumentAsync(TrxDocumentHeader doc, CancellationToken ct = default)
    {
        var result = new BusinessValidationResult();

        if (doc.Lines == null || doc.Lines.Count == 0)
        {
            result.AddError("Lines", "المستند لا يحتوي على أي بنود صالحة للترحيل.", ErrorCodes.FieldRequired);
            return result;
        }

        // 1. Fiscal Period Validation
        await ValidateFiscalPeriodAsync(doc.DocDate, result, ct);

        // 2. Transaction Type Validation
        var trxType = await _trxTypeRepository.GetTrxTypeAsync(doc.TrxType, ct);
        if (trxType == null || !trxType.IsActive)
        {
            result.AddError("TrxType", $"نوع المعاملة ({doc.TrxType}) غير نشط أو غير موجود.", ErrorCodes.TrxTypeRequired);
            return result;
        }

        // 3. Party Credit Limit Check on Posting
        if (trxType.AffectsPartyBalance && doc.PartyId.HasValue)
        {
            await ValidateCustomerCreditLimitAsync(doc.PartyId.Value, doc.TotalNet, result, ct);
        }

        // 4. Stock validation on lines
        long warehouseId = trxType.StockDirection > 0
            ? (doc.ToWarehouseId ?? 1)
            : (doc.FromWarehouseId ?? 1);

        int lineIdx = 0;
        foreach (var line in doc.Lines)
        {
            lineIdx++;
            var item = await _itemRepository.GetByIdAsync(line.ItemId, ct);
            if (item == null || !item.IsActive)
            {
                result.AddError($"Lines[{lineIdx}].ItemId", $"الصنف رقم #{line.ItemId} غير موجود أو تم تعطيله.", ErrorCodes.ItemNotFound);
                continue;
            }

            // Stock Direction Consistency Check on Posting
            if (trxType.AffectsStock)
            {
                if (trxType.StockDirection < 0)
                {
                    if (line.QuantityIn > 0)
                    {
                        result.AddError($"Lines[{lineIdx}].QuantityIn",
                            $"نوع المعاملة '{trxType.TrxNameLocal}' هي حركة صرف/إخراج مبيعات ولا يمكن ترحيلها مع كمية إدخال (QuantityIn={line.QuantityIn}).",
                            ErrorCodes.ValidationError);
                    }
                    if (line.QuantityOut <= 0)
                    {
                        result.AddError($"Lines[{lineIdx}].QuantityOut",
                            $"نوع المعاملة '{trxType.TrxNameLocal}' هي حركة صرف/إخراج مبيعات ويجب أن تكون كمية الإخراج (QuantityOut) أكبر من الصفر للترحيل.",
                            ErrorCodes.PositiveNumberRequired);
                    }
                }
                else if (trxType.StockDirection > 0)
                {
                    if (line.QuantityOut > 0)
                    {
                        result.AddError($"Lines[{lineIdx}].QuantityOut",
                            $"نوع المعاملة '{trxType.TrxNameLocal}' هي حركة توريد/إدخال مشتريات ولا يمكن ترحيلها مع كمية إخراج (QuantityOut={line.QuantityOut}).",
                            ErrorCodes.ValidationError);
                    }
                    if (line.QuantityIn <= 0)
                    {
                        result.AddError($"Lines[{lineIdx}].QuantityIn",
                            $"نوع المعاملة '{trxType.TrxNameLocal}' هي حركة توريد/إدخال مشتريات ويجب أن تكون كمية الإدخال (QuantityIn) أكبر من الصفر للترحيل.",
                            ErrorCodes.PositiveNumberRequired);
                    }
                }
            }

            // Negative stock validation
            if (trxType.AffectsStock && trxType.StockDirection < 0 && line.QuantityOut > 0)
            {
                if (!item.AllowNegativeStock)
                {
                    var balance = await _stockBalanceRepository.GetAsync(line.ItemId, warehouseId, null, ct);
                    var available = balance != null ? (balance.OnHandQty - balance.ReservedQty) : 0m;
                    if (available < line.QuantityOut)
                    {
                        result.AddError($"Lines[{lineIdx}].QuantityOut",
                            $"الرصيد المتاح للصنف '{item.ItemNameLocal}' بالمستودع ({available}) لا يكفي للكمية المطلوبة ({line.QuantityOut}). البيع بالسالب ممنوع.",
                            ErrorCodes.InsufficientStock);
                    }
                }
            }

            // Lot and Expiry validation
            if (item.LotTracking && trxType.AffectsStock && trxType.StockDirection < 0)
            {
                if (string.IsNullOrWhiteSpace(line.LotNumber))
                {
                    result.AddError($"Lines[{lineIdx}].LotNumber", $"الصنف '{item.ItemNameLocal}' يخضع للتتبع بالتشغيلة ويجب إدخال رقم التشغيلة.", ErrorCodes.LotNumberRequired);
                }
                if (line.ExpiryDate.HasValue && line.ExpiryDate.Value <= DateTime.UtcNow)
                {
                    result.AddError($"Lines[{lineIdx}].ExpiryDate", $"التشغيلة '{line.LotNumber}' منتهية الصلاحية بتاريخ ({line.ExpiryDate.Value:yyyy-MM-dd}).", ErrorCodes.InvalidDate);
                }
            }

            // Serial tracking
            if (item.SerialTracking && trxType.AffectsStock && trxType.StockDirection < 0)
            {
                if (string.IsNullOrWhiteSpace(line.SerialNumber))
                {
                    result.AddError($"Lines[{lineIdx}].SerialNumber", $"الصنف '{item.ItemNameLocal}' يخضع للأرقام التسلسلية ويجب تحديد رقم السيريال.", ErrorCodes.SerialNumberRequired);
                }
            }
        }

        return result;
    }

    public async Task<BusinessValidationResult> ValidatePosOrderDtoAsync(CreatePosOrderDto dto, CancellationToken ct = default)
    {
        var result = new BusinessValidationResult();

        if (dto.Lines == null || dto.Lines.Count == 0)
        {
            result.AddError("Lines", "يجب أن يحتوي طلب نقطة البيع على صنف واحد على الأقل.", ErrorCodes.FieldRequired);
            return result;
        }

        // 1. Shift & Till Validation
        var shift = await _shiftRepository.GetShiftByIdAsync(dto.ShiftId, ct);
        if (shift == null || shift.Status != PosShiftStatus.Open)
        {
            result.AddError("ShiftId", "لا يمكن إنشاء أو تسجيل طلب نقطة بيع بدون وردية مفتوحة ونشطة.", ErrorCodes.UnauthorizedAction);
        }
        else if (shift.TillId != dto.TillId)
        {
            result.AddError("TillId", "نقطة البيع (Till) المحددة لا تتطابق مع الوردية المفتوحة الحالية.", ErrorCodes.UnauthorizedAction);
        }

        // 2. Lines Validation (Negative stock & Item active)
        int lineIdx = 0;
        foreach (var line in dto.Lines)
        {
            lineIdx++;
            var item = await _itemRepository.GetByIdAsync(line.ItemId, ct);
            if (item == null || !item.IsActive)
            {
                result.AddError($"Lines[{lineIdx}].ItemId", $"الصنف '{line.ItemName}' غير موجود أو غير نشط في النظام.", ErrorCodes.ItemNotFound);
                continue;
            }

            if (!item.ShowInPos)
            {
                result.AddError($"Lines[{lineIdx}].ItemId", $"الصنف '{item.ItemNameLocal}' غير مخصص للبيع في شاشات نقاط البيع.", ErrorCodes.UnauthorizedAction);
            }

            if (line.Quantity <= 0)
            {
                result.AddError($"Lines[{lineIdx}].Quantity", $"كمية الصنف '{item.ItemNameLocal}' يجب أن تكون أكبر من الصفر.", ErrorCodes.PositiveNumberRequired);
            }

            // Dynamic Negative Stock Check in POS
            if (!item.AllowNegativeStock)
            {
                var balances = await _stockBalanceRepository.GetByItemAsync(line.ItemId, ct);
                var totalAvailable = balances != null ? balances.Sum(b => b.OnHandQty - b.ReservedQty) : 0m;
                if (totalAvailable < line.Quantity)
                {
                    result.AddError($"Lines[{lineIdx}].Quantity",
                        $"الرصيد المتاح للصنف '{item.ItemNameLocal}' ({totalAvailable}) لا يكفي للكمية المطلوبة ({line.Quantity}).",
                        ErrorCodes.InsufficientStock);
                }
            }

            // Price List MinPrice Validation
            if (dto.PriceListId.HasValue)
            {
                var priceItem = await _priceListRepository.GetPriceListItemByIdAsync(dto.PriceListId.Value, line.ItemId, ct);
                if (priceItem != null && priceItem.MinPrice.HasValue && priceItem.MinPrice.Value > 0)
                {
                    if (line.UnitPrice < priceItem.MinPrice.Value)
                    {
                        result.AddError($"Lines[{lineIdx}].UnitPrice",
                            $"سعر بيع '{item.ItemNameLocal}' ({line.UnitPrice:N2}) أقل من الحد الأدنى المسموح ({priceItem.MinPrice.Value:N2}).",
                            ErrorCodes.OutOfRange);
                    }
                }
            }
        }

        // 3. Discount Amount Validation
        if (dto.ManualDiscountPercent > 100 || dto.ManualDiscountPercent < 0)
        {
            result.AddError("ManualDiscountPercent", "نسبة الخصم يجب أن تكون بين 0 و 100%.", ErrorCodes.OutOfRange);
        }

        // 4. Payment Settlement Check
        if (dto.Payments != null && dto.Payments.Count > 0)
        {
            foreach (var p in dto.Payments)
            {
                if (p.Amount <= 0)
                {
                    result.AddError("Payments", "مبلغ الدفعة يجب أن يكون أكبر من الصفر.", ErrorCodes.PositiveNumberRequired);
                }
                if (p.PaymentMethod == PosPaymentMethod.Card && string.IsNullOrWhiteSpace(p.TransactionReference) && string.IsNullOrWhiteSpace(p.AuthCode))
                {
                    result.AddWarning("يُفضل إدخال رقم العملية المرجعي للبطاقة لضمان مطابقة الكاشير البنكية.");
                }
            }
        }

        return result;
    }

    public async Task<BusinessValidationResult> ValidatePosOrderAsync(PosOrderHeader order, CancellationToken ct = default)
    {
        var result = new BusinessValidationResult();

        if (order.Lines == null || !order.Lines.Any(l => !l.IsVoided))
        {
            result.AddError("Lines", "الطلب لا يحتوي على أي بنود نشطة (كافة البنود ملغاة).", ErrorCodes.FieldRequired);
        }

        var shift = await _shiftRepository.GetShiftByIdAsync(order.ShiftId, ct);
        if (shift == null || shift.Status != PosShiftStatus.Open)
        {
            result.AddError("ShiftId", "الوردية التابع لها الطلب تم إغلاقها مسبقاً.", ErrorCodes.UnauthorizedAction);
        }

        return result;
    }

    private async Task ValidateFiscalPeriodAsync(DateTime docDate, BusinessValidationResult result, CancellationToken ct)
    {
        var period = await _fiscalPeriodRepository.GetPeriodByDateOnlyAsync(docDate, ct);
        if (period == null || !string.Equals(period.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            result.AddError("DocDate",
                $"تاريخ المستند ({docDate:yyyy-MM-dd}) يقع في فترة محاسبية مغلقة أو غير معرفة في النظام.",
                ErrorCodes.FiscalPeriodClosed);
        }
    }

    private async Task ValidateCustomerCreditLimitAsync(long customerId, decimal newDocumentAmount, BusinessValidationResult result, CancellationToken ct)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId, ct);
        if (customer == null) return;

        if (!customer.IsActive)
        {
            result.AddError("PartyId", $"حساب العميل '{customer.NameLocal}' مجمّد أو غير نشط.", ErrorCodes.CustomerNotFound);
            return;
        }

        if (customer.CreditLimit.HasValue && customer.CreditLimit.Value > 0)
        {
            var openInvoices = await _arSubledgerRepository.GetOpenInvoicesAsync(customer.CustomerCode, ct);
            var currentOpenBalance = openInvoices.Sum(i => i.LocalOpenAmount);

            if ((currentOpenBalance + newDocumentAmount) > customer.CreditLimit.Value)
            {
                result.AddError("PartyId",
                    $"تجاوز العميل سقف الائتمان المسموح به. سقف الائتمان: {customer.CreditLimit.Value:N2}، الرصيد المستحق الحالي: {currentOpenBalance:N2}، قيمة الفاتورة: {newDocumentAmount:N2}.",
                    ErrorCodes.CreditLimitExceeded);
            }

            var overdueCount = openInvoices.Count(i => i.DueDate.HasValue && i.DueDate.Value < DateTime.UtcNow && i.LocalOpenAmount > 0);
            if (overdueCount > 0)
            {
                result.AddWarning($"العميل لديه {overdueCount} فواتير متأخرة السداد تجاوزت تاريخ استحقاقها.");
            }
        }
    }

    private async Task ValidateWarehouseLineAsync(
        CreateTrxDocumentLineDto line,
        int lineIdx,
        TrxTransactionType trxType,
        long warehouseId,
        BusinessValidationResult result,
        CancellationToken ct)
    {
        var item = await _itemRepository.GetByIdAsync(line.ItemId, ct);
        if (item == null || !item.IsActive)
        {
            result.AddError($"Lines[{lineIdx}].ItemId", $"الصنف رقم #{line.ItemId} غير موجود أو غير نشط.", ErrorCodes.ItemNotFound);
            return;
        }

        var activeQty = line.QuantityOut > 0 ? line.QuantityOut : line.QuantityIn;
        if (activeQty <= 0)
        {
            result.AddError($"Lines[{lineIdx}].Quantity", $"كمية الصنف '{item.ItemNameLocal}' يجب أن تكون أكبر من الصفر.", ErrorCodes.PositiveNumberRequired);
        }

        // Stock Direction Consistency Check
        if (trxType.AffectsStock)
        {
            if (trxType.StockDirection < 0)
            {
                if (line.QuantityIn > 0)
                {
                    result.AddError($"Lines[{lineIdx}].QuantityIn",
                        $"نوع المعاملة '{trxType.TrxNameLocal}' هي حركة صرف/إخراج مبيعات ولا يمكن أن تحتوي على كمية إدخال (QuantityIn={line.QuantityIn}). يجب إدخال كمية الصرف في QuantityOut أو استخدام حقل Quantity الموحد.",
                        ErrorCodes.ValidationError);
                }
                if (line.QuantityOut <= 0)
                {
                    result.AddError($"Lines[{lineIdx}].QuantityOut",
                        $"نوع المعاملة '{trxType.TrxNameLocal}' هي حركة صرف/إخراج مبيعات ويجب أن تكون كمية الإخراج (QuantityOut) أكبر من الصفر.",
                        ErrorCodes.PositiveNumberRequired);
                }
            }
            else if (trxType.StockDirection > 0)
            {
                if (line.QuantityOut > 0)
                {
                    result.AddError($"Lines[{lineIdx}].QuantityOut",
                        $"نوع المعاملة '{trxType.TrxNameLocal}' هي حركة توريد/إدخال مشتريات ولا يمكن أن تحتوي على كمية إخراج (QuantityOut={line.QuantityOut}). يجب إدخال كمية التوريد في QuantityIn أو استخدام حقل Quantity الموحد.",
                        ErrorCodes.ValidationError);
                }
                if (line.QuantityIn <= 0)
                {
                    result.AddError($"Lines[{lineIdx}].QuantityIn",
                        $"نوع المعاملة '{trxType.TrxNameLocal}' هي حركة توريد/إدخال مشتريات ويجب أن تكون كمية الإدخال (QuantityIn) أكبر من الصفر.",
                        ErrorCodes.PositiveNumberRequired);
                }
            }
        }

        // Negative Stock Check
        if (trxType.AffectsStock && trxType.StockDirection < 0 && line.QuantityOut > 0)
        {
            if (!item.AllowNegativeStock)
            {
                var balance = await _stockBalanceRepository.GetAsync(line.ItemId, warehouseId, null, ct);
                var available = balance != null ? (balance.OnHandQty - balance.ReservedQty) : 0m;
                if (available < line.QuantityOut)
                {
                    result.AddError($"Lines[{lineIdx}].QuantityOut",
                        $"الرصيد المتاح للصنف '{item.ItemNameLocal}' بالمستودع ({available}) لا يكفي للكمية المطلوبة ({line.QuantityOut}). البيع بالسالب غير مسموح.",
                        ErrorCodes.InsufficientStock);
                }
            }
        }

        // Lot and Expiry Tracking Check
        if (item.LotTracking && trxType.AffectsStock && trxType.StockDirection < 0)
        {
            if (string.IsNullOrWhiteSpace(line.LotNumber))
            {
                result.AddError($"Lines[{lineIdx}].LotNumber", $"الصنف '{item.ItemNameLocal}' يخضع لإلزامية رقم التشغيلة (Lot Tracking).", ErrorCodes.LotNumberRequired);
            }
            if (line.ExpiryDate.HasValue && line.ExpiryDate.Value <= DateTime.UtcNow)
            {
                result.AddError($"Lines[{lineIdx}].ExpiryDate", $"التشغيلة '{line.LotNumber}' منتهية الصلاحية بتاريخ ({line.ExpiryDate.Value:yyyy-MM-dd}).", ErrorCodes.InvalidDate);
            }
        }

        // Serial Tracking Check
        if (item.SerialTracking && trxType.AffectsStock && trxType.StockDirection < 0)
        {
            if (string.IsNullOrWhiteSpace(line.SerialNumber))
            {
                result.AddError($"Lines[{lineIdx}].SerialNumber", $"الصنف '{item.ItemNameLocal}' يخضع للتتبع بالسيريال ويجب إدخال الرقم التسلسلي.", ErrorCodes.SerialNumberRequired);
            }
        }

        // Pricing and Discount Validation
        if (trxType.RequiresPrice && line.UnitPrice <= 0)
        {
            result.AddError($"Lines[{lineIdx}].UnitPrice", $"سعر الوحدة للصنف '{item.ItemNameLocal}' يجب أن يكون أكبر من الصفر.", ErrorCodes.PositiveNumberRequired);
        }

        decimal lineGross = activeQty * line.UnitPrice;
        if (line.DiscountAmount > lineGross)
        {
            result.AddError($"Lines[{lineIdx}].DiscountAmount", $"قيمة الخصم ({line.DiscountAmount:N2}) لا يمكن أن تتجاوز إجمالي السطر ({lineGross:N2}).", ErrorCodes.OutOfRange);
        }

        if (line.UnitPrice > 0 && item.StandardCost > 0 && line.UnitPrice < item.StandardCost && line.QuantityOut > 0)
        {
            result.AddWarning($"سعر بيع الصنف '{item.ItemNameLocal}' ({line.UnitPrice:N2}) أقل من سعر التكلفة ({item.StandardCost:N2}).");
        }
    }
}
