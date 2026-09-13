using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Pos;

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
}

public interface IPosCalculationEngine
{
    Task<PosOrderCalculationResult> CalculateOrderTotalsAsync(
        long branchId,
        CreatePosOrderDto orderDto,
        decimal defaultTaxRatePercent = 15m,
        CancellationToken ct = default);
}

public class PosCalculationEngine : IPosCalculationEngine
{
    private readonly IPosPromotionEngine _promotionEngine;

    public PosCalculationEngine(IPosPromotionEngine promotionEngine)
    {
        _promotionEngine = promotionEngine;
    }

    public async Task<PosOrderCalculationResult> CalculateOrderTotalsAsync(
        long branchId,
        CreatePosOrderDto orderDto,
        decimal defaultTaxRatePercent = 15m,
        CancellationToken ct = default)
    {
        var result = new PosOrderCalculationResult();
        decimal rawSubtotal = 0m;
        decimal totalLineDiscounts = 0m;

        // Step 1: Calculate Line Gross, Modifiers, and Line-Level Discounts
        foreach (var line in orderDto.Lines)
        {
            var lineGross = line.Quantity * line.UnitPrice;
            var modExtra = line.Modifiers.Sum(m => m.Quantity * m.ExtraPrice);
            var baseLineTotal = lineGross + modExtra;

            decimal lineDiscount = line.DiscountAmount;
            if (line.DiscountPercent > 0)
            {
                lineDiscount += baseLineTotal * (line.DiscountPercent / 100m);
            }

            lineDiscount = Math.Min(lineDiscount, baseLineTotal); // Cannot exceed line price
            var lineNet = baseLineTotal - lineDiscount;

            rawSubtotal += baseLineTotal;
            totalLineDiscounts += lineDiscount;

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
                TaxPercent = defaultTaxRatePercent
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
        decimal orderDiscount = orderDto.ManualDiscountAmount;
        if (orderDto.ManualDiscountPercent > 0)
        {
            orderDiscount += netAfterPromo * (orderDto.ManualDiscountPercent / 100m);
        }
        result.OrderDiscountAmount = Math.Min(orderDiscount, netAfterPromo);

        result.TotalDiscountAmount = result.LineDiscountsAmount + result.PromotionDiscountsAmount + result.OrderDiscountAmount;
        result.NetTaxableAmount = netAfterPromo - result.OrderDiscountAmount;

        // Step 4: Calculate Taxes (Standard 15% or item specified)
        // Distribute net taxable amount proportionally across lines to compute exact line tax
        decimal totalTax = 0m;
        foreach (var lineCalc in result.Lines)
        {
            if (netAfterLineDiscounts > 0)
            {
                var proportion = lineCalc.LineNetBeforeTax / netAfterLineDiscounts;
                var effectiveLineTaxable = proportion * result.NetTaxableAmount;
                var lineTax = Math.Round(effectiveLineTaxable * (lineCalc.TaxPercent / 100m), 4);
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
        result.TaxAmount = totalTax;

        // Step 5: Additional Charges (Service charge, delivery fees, tips)
        result.ServiceChargeAmount = orderDto.ServiceChargeAmount;
        result.DeliveryFee = orderDto.DeliveryFee;
        result.TipAmount = orderDto.TipAmount;

        // Final Total
        result.TotalAmount = Math.Round(
            result.NetTaxableAmount + result.TaxAmount + result.ServiceChargeAmount + result.DeliveryFee + result.TipAmount,
            4);

        return result;
    }
}
