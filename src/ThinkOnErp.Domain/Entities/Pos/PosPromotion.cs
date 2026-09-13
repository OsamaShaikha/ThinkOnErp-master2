using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosPromotion
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionNameLocal { get; set; } = string.Empty;
    public string? PromotionNameEn { get; set; }
    public PosPromotionType PromotionType { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Happy Hour & Time triggers
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? DaysOfWeekMask { get; set; } // e.g. "1,2,3,4,5" (Monday-Friday)

    public int Priority { get; set; } = 0;
    public bool CanCombineWithOtherDiscounts { get; set; }
    public decimal? MinimumCartAmount { get; set; }

    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation
    public SysBranch? Branch { get; set; }
    public ICollection<PosPromotionRule> Rules { get; set; } = new List<PosPromotionRule>();
}

public class PosPromotionRule
{
    public long Id { get; set; }
    public long PromotionId { get; set; }

    // Condition
    public long? RequiredGroupId { get; set; }
    public long? RequiredItemId { get; set; }
    public decimal RequiredQuantity { get; set; } = 1;

    // Reward / Action
    public long? RewardItemId { get; set; }
    public long? RewardGroupId { get; set; }
    public decimal RewardQuantity { get; set; } = 1;
    public decimal DiscountPercent { get; set; }
    public decimal FixedBundlePrice { get; set; }

    // Navigation
    public PosPromotion? Promotion { get; set; }
}
