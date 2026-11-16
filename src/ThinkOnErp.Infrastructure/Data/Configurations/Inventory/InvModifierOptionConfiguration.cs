using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvModifierOptionConfiguration : IEntityTypeConfiguration<InvModifierOption>
{
    public void Configure(EntityTypeBuilder<InvModifierOption> builder)
    {
        builder.ToTable("INV_MODIFIER_OPTION");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(o => o.ModifierGroupId).HasColumnName("MODIFIER_GROUP_ID").IsRequired();
        builder.Property(o => o.OptionNameLocal).HasColumnName("OPTION_NAME_LOCAL").HasMaxLength(150).IsRequired();
        builder.Property(o => o.OptionNameEn).HasColumnName("OPTION_NAME_EN").HasMaxLength(150);
        builder.Property(o => o.PriceAdjustment).HasColumnName("PRICE_ADJUSTMENT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.RelatedItemId).HasColumnName("RELATED_ITEM_ID");
        builder.Property(o => o.RelatedItemQty).HasColumnName("RELATED_ITEM_QTY").HasColumnType("NUMBER(18,4)").HasDefaultValue(1m);
        builder.Property(o => o.RelatedUomCode).HasColumnName("RELATED_UOM_CODE").HasMaxLength(20);
        builder.Property(o => o.UomFactor).HasColumnName("UOM_FACTOR").HasColumnType("NUMBER(18,6)").HasDefaultValue(1m);
        builder.Property(o => o.OptionType).HasColumnName("OPTION_TYPE").HasConversion<int>().HasDefaultValue(OptionType.ADD);
        builder.Property(o => o.ReplacesItemId).HasColumnName("REPLACES_ITEM_ID");
        builder.Property(o => o.IsDefault).HasColumnName("IS_DEFAULT").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(o => o.SortOrder).HasColumnName("SORT_ORDER").HasColumnType("NUMBER(10)").HasDefaultValue(0);
        builder.Property(o => o.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.HasIndex(o => o.ModifierGroupId);
        builder.HasOne(o => o.ModifierGroup).WithMany(g => g.Options).HasForeignKey(o => o.ModifierGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(o => o.RelatedItem).WithMany().HasForeignKey(o => o.RelatedItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(o => o.ReplacesItem).WithMany().HasForeignKey(o => o.ReplacesItemId).OnDelete(DeleteBehavior.SetNull);
    }
}
