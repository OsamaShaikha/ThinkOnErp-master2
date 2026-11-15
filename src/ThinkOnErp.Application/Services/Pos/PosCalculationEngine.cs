using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Validation;

namespace ThinkOnErp.Application.Services.Pos;

public class PosOrderCalculationResult
{
    public decimal SubtotalAmount { get; set; }
    public decimal LineDiscountsAmount { get; set; }
    public decimal PromotionDiscountsAmount { get; set; }
    public decimal OrderDiscountAmount { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal NetTaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsTaxInclusive { get; set; }

    public List<CalculatedLineResult> Lines { get; set; } = new();
    public List<AppliedPromotionInfo> AppliedPromotions { get; set; } = new();
}

public class CalculatedLineResult
{
    public int LineNumber { get; set; }
    public long ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineGross { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal ModifiersTotal { get; set; }
    public decimal LineNetBeforeTax { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsInclusive { get; set; }
}

public interface IPosCalculationEngine
{
    Task<PosOrderCalculationResult> CalculateOrderTotalsAsync(
        long branchId,
        CreatePosOrderDto orderDto,
        decimal? defaultTaxRatePercent = null,
        CancellationToken ct = default);

    Task<PosOrderCalculationResult> CalculateOrderTotalsAsync(
        long branchId,
        CreatePosOrderDto orderDto,
        decimal? defaultTaxRatePercent,
        bool? isTaxInclusive,
        CancellationToken ct = default);
}

public class PosCalculationEngine : IPosCalculationEngine
{
    private readonly IPosPromotionEngine _promotionEngine;
    private readonly IInvoiceBusinessValidationService _validationService;

    public PosCalculationEngine(
        IPosPromotionEngine promotionEngine,
        IInvoiceBusinessValidationService validationService)
    {
        _promotionEngine = promotionEngine;
        _validationService = validationService;
    }

    public Task<PosOrderCalculationResult> CalculateOrderTotalsAsync(
        long branchId,
        CreatePosOrderDto orderDto,
        decimal? defaultTaxRatePercent = null,
        CancellationToken ct = default)
    {
        return CalculateOrderTotalsAsync(branchId, orderDto, defaultTaxRatePercent, null, ct);
    }

    public async Task<PosOrderCalculationResult> CalculateOrderTotalsAsync(
        long branchId,
        CreatePosOrderDto orderDto,
        decimal? defaultTaxRatePercent,
        bool? isTaxInclusive,
        CancellationToken ct = default)
    {
        var result = new PosOrderCalculationResult();
        bool? orderInclusive = isTaxInclusive;
        result.IsTaxInclusive = orderInclusive ?? false;

        decimal totalLineDiscounts = 0m;
        decimal rawSubtotal = 0m;

        // Step 1: Calculate Line Gross, Modifiers, Line Discounts and Dynamic Item Tax
        foreach (var line in orderDto.Lines)
        {
            // Determine if line price includes tax:
            // 1. Explicit line override / branch mode
            // 2. Item-level setting in DB (TaxType == 1: Inclusive, 2: Exclusive)
            // 3. Fall back to false (Exclusive)
            bool lineInclusive;
            if (orderInclusive.HasValue)
            {
                lineInclusive = orderInclusive.Value;
            }
            else
            {
                bool? itemInclusive = null;
                if (line.ItemId > 0)
                {
                    itemInclusive = await _validationService.GetItemPriceIncludesTaxAsync(line.ItemId, ct);
                }
                lineInclusive = itemInclusive ?? false;
            }

            var lineGross = line.Quantity * line.UnitPrice;
            var modExtra = line.Modifiers.Sum(m => m.Quantity * m.ExtraPrice);
            var baseLineTotal = lineGross + modExtra;

            decimal lineDiscount = 0m;
            if (line.DiscountAmount > 0)
            {
                lineDiscount = line.DiscountAmount;
            }
            else if (line.DiscountPercent > 0)
            {
                lineDiscount = baseLineTotal * (line.DiscountPercent / 100m);
            }

            lineDiscount = Math.Min(lineDiscount, baseLineTotal); // Cannot exceed line price
            var lineNet = baseLineTotal - lineDiscount;

            rawSubtotal += baseLineTotal;
            totalLineDiscounts += lineDiscount;

            // Resolve dynamic tax rate per item from database (only apply what is configured)
            decimal itemTaxPercent = 0m;
            var itemRate = await _validationService.GetEffectiveTaxRateAsync(line.ItemId, branchId, ct);
            if (itemRate != null && !itemRate.IsZeroRated && !itemRate.IsExempt)
            {
                itemTaxPercent = itemRate.RatePercent;
            }

            result.Lines.Add(new CalculatedLineResult
            {
                LineNumber = line.LineNumber,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineGross = lineGross,
                ModifiersTotal = modExtra,
                LineDiscount = lineDiscount,
                LineNetBeforeTax = lineNet, // temporary storage of lineNet for Step 4 proportion
                TaxPercent = itemTaxPercent,
                IsInclusive = lineInclusive
            });
        }

        var netAfterLineDiscounts = rawSubtotal - totalLineDiscounts;

        // Step 2: Apply Automated Promotion Engine (BOGO, Mix & Match, Happy Hour, Tiered Cart)
        var promoResult = await _promotionEngine.EvaluatePromotionsAsync(branchId, orderDto.Lines, netAfterLineDiscounts, ct);
        result.PromotionDiscountsAmount = Math.Min(promoResult.TotalPromotionDiscount, netAfterLineDiscounts);
        result.AppliedPromotions = promoResult.AppliedPromotions;

        var netAfterPromo = netAfterLineDiscounts - result.PromotionDiscountsAmount;

        // Step 3: Apply Manual Invoice-Level Discount
        decimal orderDiscount = 0m;
        if (orderDto.ManualDiscountAmount > 0)
        {
            orderDiscount = orderDto.ManualDiscountAmount;
        }
        else if (orderDto.ManualDiscountPercent > 0)
        {
            orderDiscount = netAfterPromo * (orderDto.ManualDiscountPercent / 100m);
        }
        result.OrderDiscountAmount = Math.Min(orderDiscount, netAfterPromo);

        decimal totalCartDiscount = result.PromotionDiscountsAmount + result.OrderDiscountAmount;

        // Step 4: Line Taxes and Net Before Tax Calculation
        decimal totalTax = 0m;
        decimal totalGrossBeforeTax = 0m;

        foreach (var lineCalc in result.Lines)
        {
            decimal lineBase = lineCalc.LineNetBeforeTax; // stored lineNet
            decimal allocatedOrderDiscount = 0m;
            if (netAfterLineDiscounts > 0 && totalCartDiscount > 0)
            {
                allocatedOrderDiscount = Math.Round((lineBase / netAfterLineDiscounts) * totalCartDiscount, 4);
            }
            decimal effectiveLineAmount = Math.Max(0m, lineBase - allocatedOrderDiscount);

            decimal lineNetBeforeTax;
            decimal lineTax;
            decimal lineTotal;
            decimal lineGrossBeforeTax;

            if (lineCalc.TaxPercent <= 0m)
            {
                lineNetBeforeTax = effectiveLineAmount;
                lineTax = 0m;
                lineTotal = effectiveLineAmount;
                lineGrossBeforeTax = lineCalc.LineGross + lineCalc.ModifiersTotal;
            }
            else if (lineCalc.IsInclusive)
            {
                // In Tax-Inclusive: price already contains tax
                lineNetBeforeTax = Math.Round(effectiveLineAmount / (1.0m + (lineCalc.TaxPercent / 100.0m)), 4);
                lineTax = Math.Round(effectiveLineAmount - lineNetBeforeTax, 4);
                lineTotal = effectiveLineAmount;
                lineGrossBeforeTax = Math.Round((lineCalc.LineGross + lineCalc.ModifiersTotal) / (1.0m + (lineCalc.TaxPercent / 100.0m)), 4);
            }
            else
            {
                // In Tax-Exclusive: tax is added on top of base amount
                lineNetBeforeTax = effectiveLineAmount;
                lineTax = Math.Round(effectiveLineAmount * (lineCalc.TaxPercent / 100.0m), 4);
                lineTotal = Math.Round(effectiveLineAmount + lineTax, 4);
                lineGrossBeforeTax = lineCalc.LineGross + lineCalc.ModifiersTotal;
            }

            lineCalc.LineNetBeforeTax = lineNetBeforeTax;
            lineCalc.TaxAmount = lineTax;
            lineCalc.LineTotal = lineTotal;

            totalTax += lineTax;
            totalGrossBeforeTax += lineGrossBeforeTax;
        }

        result.TaxAmount = Math.Round(totalTax, 4);
        result.NetTaxableAmount = Math.Round(result.Lines.Sum(l => l.LineNetBeforeTax), 4);
        result.SubtotalAmount = Math.Round(totalGrossBeforeTax, 4);
        result.TotalDiscountAmount = Math.Round(result.SubtotalAmount - result.NetTaxableAmount, 4);
        result.LineDiscountsAmount = totalLineDiscounts;
        result.IsTaxInclusive = orderInclusive ?? (result.Lines.Count > 0 && result.Lines.All(l => l.IsInclusive));

        // Step 5: Additional Charges (Service charge, delivery fees, tips)
        result.ServiceChargeAmount = orderDto.ServiceChargeAmount;
        result.DeliveryFee = orderDto.DeliveryFee;
        result.TipAmount = orderDto.TipAmount;
        decimal additionalCharges = result.ServiceChargeAmount + result.DeliveryFee + result.TipAmount;

        // Step 6: Grand Total
        result.TotalAmount = Math.Round(result.Lines.Sum(l => l.LineTotal) + additionalCharges, 4);

        return result;
    }
}
