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
        bool orderInclusive = isTaxInclusive ?? (orderDto.IsTaxInclusive ?? false);
        result.IsTaxInclusive = orderInclusive;

        decimal rawSubtotal = 0m;
        decimal totalLineDiscounts = 0m;

        // Step 1: Calculate Line Gross, Modifiers, Line Discounts and Dynamic Item Tax
        foreach (var line in orderDto.Lines)
        {
            bool lineInclusive = line.IsTaxInclusive ?? orderInclusive;
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

            // Resolve dynamic tax rate per item from database or explicit overrides (Exempt, Zero, Custom rate)
            decimal itemTaxPercent;
            if (orderDto.IsTaxExempt == true || line.IsTaxExempt == true)
            {
                itemTaxPercent = 0m;
            }
            else if (line.TaxPercentOverride.HasValue)
            {
                itemTaxPercent = line.TaxPercentOverride.Value;
            }
            else if (orderDto.TaxRateOverride.HasValue)
            {
                itemTaxPercent = orderDto.TaxRateOverride.Value;
            }
            else
            {
                itemTaxPercent = defaultTaxRatePercent ?? await _validationService.GetEffectiveTaxRatePercentAsync(line.ItemId, branchId, ct);
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
                LineNetBeforeTax = lineNet,
                TaxPercent = itemTaxPercent,
                IsInclusive = lineInclusive
            });
        }

        result.SubtotalAmount = rawSubtotal;
        result.LineDiscountsAmount = totalLineDiscounts;

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

        result.TotalDiscountAmount = result.LineDiscountsAmount + result.PromotionDiscountsAmount + result.OrderDiscountAmount;
        result.NetTaxableAmount = netAfterPromo - result.OrderDiscountAmount;

        // Step 4: Calculate Taxes (Standard 15% or item specified; Inclusive or Exclusive)
        decimal totalTax = 0m;
        foreach (var lineCalc in result.Lines)
        {
            if (lineCalc.IsInclusive)
            {
                // In Tax-Inclusive mode, price already contains tax
                decimal effectiveLineInclusive = netAfterLineDiscounts > 0
                    ? Math.Round((lineCalc.LineNetBeforeTax / netAfterLineDiscounts) * result.NetTaxableAmount, 4)
                    : 0m;

                decimal lineNetBeforeTax;
                decimal lineTax;

                if (lineCalc.TaxPercent > 0)
                {
                    lineNetBeforeTax = Math.Round(effectiveLineInclusive / (1.0m + (lineCalc.TaxPercent / 100.0m)), 4);
                    lineTax = Math.Round(effectiveLineInclusive - lineNetBeforeTax, 4);
                }
                else
                {
                    lineNetBeforeTax = effectiveLineInclusive;
                    lineTax = 0m;
                }

                lineCalc.TaxAmount = lineTax;
                lineCalc.LineNetBeforeTax = lineNetBeforeTax;
                lineCalc.LineTotal = effectiveLineInclusive;
                totalTax += lineTax;
            }
            else
            {
                // In Tax-Exclusive mode, tax is added on top
                if (netAfterLineDiscounts > 0)
                {
                    var proportion = lineCalc.LineNetBeforeTax / netAfterLineDiscounts;
                    var effectiveLineTaxable = proportion * result.NetTaxableAmount;
                    var lineTax = lineCalc.TaxPercent > 0
                        ? Math.Round(effectiveLineTaxable * (lineCalc.TaxPercent / 100m), 4)
                        : 0m;
                    lineCalc.TaxAmount = lineTax;
                    lineCalc.LineTotal = Math.Round(effectiveLineTaxable + lineTax, 4);
                    totalTax += lineTax;
                }
                else
                {
                    lineCalc.TaxAmount = 0m;
                    lineCalc.LineTotal = 0m;
                }
            }
        }
        result.TaxAmount = totalTax;

        // Step 5: Additional Charges (Service charge, delivery fees, tips)
        result.ServiceChargeAmount = orderDto.ServiceChargeAmount;
        result.DeliveryFee = orderDto.DeliveryFee;
        result.TipAmount = orderDto.TipAmount;

        // Final Total
        if (orderInclusive)
        {
            result.NetTaxableAmount = Math.Round(result.Lines.Sum(l => l.LineNetBeforeTax), 4);
            result.TotalAmount = Math.Round(
                result.Lines.Sum(l => l.LineTotal) + result.ServiceChargeAmount + result.DeliveryFee + result.TipAmount,
                4);
        }
        else
        {
            result.TotalAmount = Math.Round(
                result.NetTaxableAmount + result.TaxAmount + result.ServiceChargeAmount + result.DeliveryFee + result.TipAmount,
                4);
        }

        return result;
    }
}
