using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosPromotionConfiguration : IEntityTypeConfiguration<PosPromotion>
{
    public void Configure(EntityTypeBuilder<PosPromotion> builder)
    {
        builder.ToTable("POS_PROMOTION");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(p => p.PromotionCode).HasColumnName("PROMOTION_CODE").HasMaxLength(50).IsRequired();
        builder.Property(p => p.PromotionNameLocal).HasColumnName("PROMOTION_NAME_LOCAL").HasMaxLength(200).IsRequired();
        builder.Property(p => p.PromotionNameEn).HasColumnName("PROMOTION_NAME_EN").HasMaxLength(200);
        builder.Property(p => p.PromotionType).HasColumnName("PROMOTION_TYPE").HasConversion<int>().IsRequired();

        builder.Property(p => p.StartDate).HasColumnName("START_DATE").HasColumnType("TIMESTAMP").IsRequired();
        builder.Property(p => p.EndDate).HasColumnName("END_DATE").HasColumnType("TIMESTAMP").IsRequired();
        builder.Property(p => p.StartTime).HasColumnName("START_TIME");
        builder.Property(p => p.EndTime).HasColumnName("END_TIME");
        builder.Property(p => p.DaysOfWeekMask).HasColumnName("DAYS_OF_WEEK_MASK").HasMaxLength(50);

        builder.Property(p => p.Priority).HasColumnName("PRIORITY").HasDefaultValue(0);
        builder.Property(p => p.CanCombineWithOtherDiscounts).HasColumnName("CAN_COMBINE_DISCOUNTS").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(p => p.MinimumCartAmount).HasColumnName("MINIMUM_CART_AMOUNT").HasColumnType("NUMBER(18,4)");

        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(p => new { p.BranchId, p.PromotionCode }).IsUnique();
        builder.HasOne(p => p.Branch).WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PosPromotionRuleConfiguration : IEntityTypeConfiguration<PosPromotionRule>
{
    public void Configure(EntityTypeBuilder<PosPromotionRule> builder)
    {
        builder.ToTable("POS_PROMOTION_RULE");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.PromotionId).HasColumnName("PROMOTION_ID").IsRequired();

        builder.Property(r => r.RequiredGroupId).HasColumnName("REQUIRED_GROUP_ID");
        builder.Property(r => r.RequiredItemId).HasColumnName("REQUIRED_ITEM_ID");
        builder.Property(r => r.RequiredQuantity).HasColumnName("REQUIRED_QUANTITY").HasColumnType("NUMBER(14,4)").HasDefaultValue(1m);

        builder.Property(r => r.RewardItemId).HasColumnName("REWARD_ITEM_ID");
        builder.Property(r => r.RewardGroupId).HasColumnName("REWARD_GROUP_ID");
        builder.Property(r => r.RewardQuantity).HasColumnName("REWARD_QUANTITY").HasColumnType("NUMBER(14,4)").HasDefaultValue(1m);
        builder.Property(r => r.DiscountPercent).HasColumnName("DISCOUNT_PERCENT").HasColumnType("NUMBER(8,4)").HasDefaultValue(0m);
        builder.Property(r => r.FixedBundlePrice).HasColumnName("FIXED_BUNDLE_PRICE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.HasOne(r => r.Promotion).WithMany(p => p.Rules).HasForeignKey(r => r.PromotionId).OnDelete(DeleteBehavior.Cascade);
    }
}
