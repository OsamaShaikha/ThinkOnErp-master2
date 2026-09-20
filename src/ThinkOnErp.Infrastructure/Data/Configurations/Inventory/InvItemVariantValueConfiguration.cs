using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvItemVariantValueConfiguration : IEntityTypeConfiguration<InvItemVariantValue>
{
    public void Configure(EntityTypeBuilder<InvItemVariantValue> builder)
    {
        builder.ToTable("INV_ITEM_VARIANT_VALUE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.VariantId)
            .HasColumnName("VARIANT_ID")
            .IsRequired();

        builder.Property(e => e.AttributeId)
            .HasColumnName("ATTRIBUTE_ID")
            .IsRequired();

        builder.Property(e => e.AttributeValueId)
            .HasColumnName("ATTRIBUTE_VALUE_ID")
            .IsRequired();

        builder.HasOne(e => e.Attribute)
            .WithMany()
            .HasForeignKey(e => e.AttributeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AttributeValue)
            .WithMany()
            .HasForeignKey(e => e.AttributeValueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
