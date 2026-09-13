using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosModifierOptionConfiguration : IEntityTypeConfiguration<PosModifierOption>
{
    public void Configure(EntityTypeBuilder<PosModifierOption> builder)
    {
        builder.ToTable("POS_MODIFIER_OPTION");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(o => o.ModifierGroupId).HasColumnName("MODIFIER_GROUP_ID").IsRequired();
        builder.Property(o => o.OptionNameLocal).HasColumnName("OPTION_NAME_LOCAL").HasMaxLength(150).IsRequired();
        builder.Property(o => o.OptionNameEn).HasColumnName("OPTION_NAME_EN").HasMaxLength(150);
        builder.Property(o => o.PriceAdjustment).HasColumnName("PRICE_ADJUSTMENT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.RelatedItemId).HasColumnName("RELATED_ITEM_ID");
        builder.Property(o => o.IsDefault).HasColumnName("IS_DEFAULT").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(o => o.SortOrder).HasColumnName("SORT_ORDER").HasColumnType("NUMBER(10)").HasDefaultValue(0);
        builder.Property(o => o.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.HasIndex(o => o.ModifierGroupId);
        builder.HasOne(o => o.ModifierGroup).WithMany(g => g.Options).HasForeignKey(o => o.ModifierGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(o => o.RelatedItem).WithMany().HasForeignKey(o => o.RelatedItemId).OnDelete(DeleteBehavior.SetNull);
    }
}
