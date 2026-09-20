using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvItemAttributeValueConfiguration : IEntityTypeConfiguration<InvItemAttributeValue>
{
    public void Configure(EntityTypeBuilder<InvItemAttributeValue> builder)
    {
        builder.ToTable("INV_ITEM_ATTRIBUTE_VALUE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.AttributeId)
            .HasColumnName("ATTRIBUTE_ID")
            .IsRequired();

        builder.Property(e => e.ValueCode)
            .HasColumnName("VALUE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ValueLocal)
            .HasColumnName("VALUE_LOCAL")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ValueEn)
            .HasColumnName("VALUE_EN")
            .HasMaxLength(100);

        builder.Property(e => e.ColorHex)
            .HasColumnName("COLOR_HEX")
            .HasMaxLength(20);

        builder.Property(e => e.SortOrder)
            .HasColumnName("SORT_ORDER")
            .HasColumnType("NUMBER(6)")
            .HasDefaultValue(0);

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);
    }
}
