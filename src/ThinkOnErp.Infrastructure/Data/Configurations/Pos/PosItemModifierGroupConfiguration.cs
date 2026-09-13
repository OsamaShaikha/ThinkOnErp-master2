using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosItemModifierGroupConfiguration : IEntityTypeConfiguration<PosItemModifierGroup>
{
    public void Configure(EntityTypeBuilder<PosItemModifierGroup> builder)
    {
        builder.ToTable("POS_ITEM_MODIFIER_GROUP");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(m => m.ItemId).HasColumnName("ITEM_ID").IsRequired();
        builder.Property(m => m.ModifierGroupId).HasColumnName("MODIFIER_GROUP_ID").IsRequired();
        builder.Property(m => m.SortOrder).HasColumnName("SORT_ORDER").HasColumnType("NUMBER(10)").HasDefaultValue(0);

        builder.HasIndex(m => new { m.ItemId, m.ModifierGroupId }).IsUnique();
        builder.HasOne(m => m.Item).WithMany().HasForeignKey(m => m.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.ModifierGroup).WithMany(g => g.ItemLinks).HasForeignKey(m => m.ModifierGroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
