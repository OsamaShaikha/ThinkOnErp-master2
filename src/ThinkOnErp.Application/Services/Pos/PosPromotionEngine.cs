using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosPromotionEngine
{
    Task<CartPromotionEvaluationResult> EvaluatePromotionsAsync(
        long branchId,
        IReadOnlyList<CreatePosOrderLineDto> lines,
        decimal currentSubtotal,
        CancellationToken ct = default);
}

public class PosPromotionEngine : IPosPromotionEngine
{
    private readonly IPosPromotionRepository _promotionRepository;

    public PosPromotionEngine(IPosPromotionRepository promotionRepository)
    {
        _promotionRepository = promotionRepository;
    }

    public async Task<CartPromotionEvaluationResult> EvaluatePromotionsAsync(
        long branchId,
        IReadOnlyList<CreatePosOrderLineDto> lines,
        decimal currentSubtotal,
        CancellationToken ct = default)
    {
        var result = new CartPromotionEvaluationResult();
        var now = DateTime.UtcNow;
        var currentTime = now.TimeOfDay;
        var currentDayOfWeek = ((int)now.DayOfWeek).ToString();

        var activePromotions = await _promotionRepository.GetActivePromotionsForBranchAsync(branchId, now, ct);

        foreach (var promo in activePromotions)
        {
            // 1. Check time-of-day / happy hour constraints
            if (promo.StartTime.HasValue && promo.EndTime.HasValue)
            {
                if (currentTime < promo.StartTime.Value || currentTime > promo.EndTime.Value)
                    continue;
            }

            // 2. Check day-of-week constraint
            if (!string.IsNullOrWhiteSpace(promo.DaysOfWeekMask))
            {
                var days = promo.DaysOfWeekMask.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!days.Contains(currentDayOfWeek))
                    continue;
            }

            // 3. Check minimum cart amount
            if (promo.MinimumCartAmount.HasValue && currentSubtotal < promo.MinimumCartAmount.Value)
                continue;

            decimal promoDiscount = 0;

            switch (promo.PromotionType)
            {
                case PosPromotionType.Bogo:
                    promoDiscount = EvaluateBogo(promo, lines);
                    break;
                case PosPromotionType.MixAndMatch:
                    promoDiscount = EvaluateMixAndMatch(promo, lines);
                    break;
                case PosPromotionType.HappyHour:
                    promoDiscount = EvaluateHappyHour(promo, lines);
                    break;
                case PosPromotionType.TieredCartDiscount:
                    promoDiscount = EvaluateTieredCart(promo, currentSubtotal);
                    break;
            }

            if (promoDiscount > 0)
            {
                result.TotalPromotionDiscount += promoDiscount;
                result.AppliedPromotions.Add(new AppliedPromotionInfo
                {
                    PromotionId = promo.Id,
                    PromotionName = promo.PromotionNameLocal,
                    PromotionType = promo.PromotionType,
                    DiscountAmount = promoDiscount,
                    Description = $"Applied {promo.PromotionNameLocal} (-{promoDiscount:F2})"
                });

                if (!promo.CanCombineWithOtherDiscounts)
                    break; // Stop evaluating further promotions if non-combinable
            }
        }

        return result;
    }

    private static decimal EvaluateBogo(PosPromotion promo, IReadOnlyList<CreatePosOrderLineDto> lines)
    {
        decimal totalDiscount = 0;
        foreach (var rule in promo.Rules)
        {
            var matchingBuyLines = lines.Where(l => (!rule.RequiredItemId.HasValue || l.ItemId == rule.RequiredItemId.Value)).ToList();
            var totalBuyQty = matchingBuyLines.Sum(l => l.Quantity);

            if (totalBuyQty >= rule.RequiredQuantity && rule.RequiredQuantity > 0)
            {
                var timesApplicable = (int)(totalBuyQty / rule.RequiredQuantity);
                var matchingRewardLines = lines.Where(l => (!rule.RewardItemId.HasValue || l.ItemId == rule.RewardItemId.Value)).ToList();

                decimal rewardQtyToDiscount = timesApplicable * rule.RewardQuantity;
                foreach (var rewardLine in matchingRewardLines)
                {
                    if (rewardQtyToDiscount <= 0) break;
                    var qtyDiscounted = Math.Min(rewardLine.Quantity, rewardQtyToDiscount);
                    var discountRate = rule.DiscountPercent > 0 ? rule.DiscountPercent / 100m : 1.0m;
                    totalDiscount += qtyDiscounted * rewardLine.UnitPrice * discountRate;
                    rewardQtyToDiscount -= qtyDiscounted;
                }
            }
        }
        return totalDiscount;
    }

    private static decimal EvaluateMixAndMatch(PosPromotion promo, IReadOnlyList<CreatePosOrderLineDto> lines)
    {
        decimal totalDiscount = 0;
        foreach (var rule in promo.Rules)
        {
            var matchingLines = lines.Where(l => (!rule.RequiredGroupId.HasValue)).ToList();
            var totalQty = matchingLines.Sum(l => l.Quantity);
            if (totalQty >= rule.RequiredQuantity && rule.RequiredQuantity > 0 && rule.FixedBundlePrice > 0)
            {
                var bundles = (int)(totalQty / rule.RequiredQuantity);
                var bundleItems = matchingLines.Take((int)(bundles * rule.RequiredQuantity)).ToList();
                var originalPrice = bundleItems.Sum(i => i.Quantity * i.UnitPrice);
                var bundleTotalPrice = bundles * rule.FixedBundlePrice;
                if (originalPrice > bundleTotalPrice)
                {
                    totalDiscount += (originalPrice - bundleTotalPrice);
                }
            }
        }
        return totalDiscount;
    }

    private static decimal EvaluateHappyHour(PosPromotion promo, IReadOnlyList<CreatePosOrderLineDto> lines)
    {
        decimal totalDiscount = 0;
        foreach (var rule in promo.Rules)
        {
            if (rule.DiscountPercent > 0)
            {
                var matchingLines = lines.Where(l => !rule.RequiredItemId.HasValue || l.ItemId == rule.RequiredItemId.Value);
                foreach (var line in matchingLines)
                {
                    totalDiscount += line.Quantity * line.UnitPrice * (rule.DiscountPercent / 100m);
                }
            }
        }
        return totalDiscount;
    }

    private static decimal EvaluateTieredCart(PosPromotion promo, decimal currentSubtotal)
    {
        var rule = promo.Rules.FirstOrDefault();
        if (rule == null) return 0;

        if (rule.DiscountPercent > 0)
            return currentSubtotal * (rule.DiscountPercent / 100m);

        if (rule.FixedBundlePrice > 0)
            return Math.Min(rule.FixedBundlePrice, currentSubtotal);

        return 0;
    }
}
