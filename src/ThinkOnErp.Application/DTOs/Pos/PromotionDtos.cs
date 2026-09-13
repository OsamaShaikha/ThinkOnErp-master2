using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

public class CreatePromotionDto
{
    public long BranchId { get; set; }
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionNameLocal { get; set; } = string.Empty;
    public string? PromotionNameEn { get; set; }
    public PosPromotionType PromotionType { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? DaysOfWeekMask { get; set; }

    public int Priority { get; set; }
    public bool CanCombineWithOtherDiscounts { get; set; }
    public decimal? MinimumCartAmount { get; set; }

    public List<CreatePromotionRuleDto> Rules { get; set; } = new();
}

public class UpdatePromotionDto
{
    public string PromotionNameLocal { get; set; } = string.Empty;
    public string? PromotionNameEn { get; set; }
    public PosPromotionType PromotionType { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? DaysOfWeekMask { get; set; }

    public int Priority { get; set; }
    public bool CanCombineWithOtherDiscounts { get; set; }
    public decimal? MinimumCartAmount { get; set; }
    public bool IsActive { get; set; } = true;

    public List<CreatePromotionRuleDto> Rules { get; set; } = new();
}

public class CreatePromotionRuleDto
{
    public long? RequiredGroupId { get; set; }
    public long? RequiredItemId { get; set; }
    public decimal RequiredQuantity { get; set; } = 1;

    public long? RewardItemId { get; set; }
    public long? RewardGroupId { get; set; }
    public decimal RewardQuantity { get; set; } = 1;
    public decimal DiscountPercent { get; set; }
    public decimal FixedBundlePrice { get; set; }
}

public class PromotionRuleDto
{
    public long Id { get; set; }
    public long PromotionId { get; set; }
    public long? RequiredGroupId { get; set; }
    public long? RequiredItemId { get; set; }
    public decimal RequiredQuantity { get; set; }
    public long? RewardItemId { get; set; }
    public long? RewardGroupId { get; set; }
    public decimal RewardQuantity { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal FixedBundlePrice { get; set; }
}

public class PromotionSummaryDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionNameLocal { get; set; } = string.Empty;
    public string? PromotionNameEn { get; set; }
    public PosPromotionType PromotionType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public int RuleCount { get; set; }
}

public class PromotionDetailsDto : PromotionSummaryDto
{
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? DaysOfWeekMask { get; set; }
    public bool CanCombineWithOtherDiscounts { get; set; }
    public decimal? MinimumCartAmount { get; set; }
    public List<PromotionRuleDto> Rules { get; set; } = new();
}

public class CartPromotionEvaluationResult
{
    public decimal TotalPromotionDiscount { get; set; }
    public List<AppliedPromotionInfo> AppliedPromotions { get; set; } = new();
}

public class AppliedPromotionInfo
{
    public long PromotionId { get; set; }
    public string PromotionName { get; set; } = string.Empty;
    public PosPromotionType PromotionType { get; set; }
    public decimal DiscountAmount { get; set; }
    public string Description { get; set; } = string.Empty;
}
