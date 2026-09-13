using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosOrderLineModifierConfiguration : IEntityTypeConfiguration<PosOrderLineModifier>
{
    public void Configure(EntityTypeBuilder<PosOrderLineModifier> builder)
    {
        builder.ToTable("POS_ORDER_LINE_MODIFIER");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(m => m.OrderLineId).HasColumnName("ORDER_LINE_ID").IsRequired();
        builder.Property(m => m.ModifierItemId).HasColumnName("MODIFIER_ITEM_ID").IsRequired();
        builder.Property(m => m.ModifierName).HasColumnName("MODIFIER_NAME").HasMaxLength(150).IsRequired();
        builder.Property(m => m.Quantity).HasColumnName("QUANTITY").HasColumnType("NUMBER(14,4)").HasDefaultValue(1m);
        builder.Property(m => m.UnitPrice).HasColumnName("UNIT_PRICE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(m => m.ExtraPrice).HasColumnName("EXTRA_PRICE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.HasOne(m => m.OrderLine).WithMany(l => l.Modifiers).HasForeignKey(m => m.OrderLineId).OnDelete(DeleteBehavior.Cascade);
    }
}
