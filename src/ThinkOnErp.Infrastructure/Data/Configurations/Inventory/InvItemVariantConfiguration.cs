using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvItemVariantConfiguration : IEntityTypeConfiguration<InvItemVariant>
{
    public void Configure(EntityTypeBuilder<InvItemVariant> builder)
    {
        builder.ToTable("INV_ITEM_VARIANT");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ItemId)
            .HasColumnName("ITEM_ID")
            .IsRequired();

        builder.Property(e => e.Sku)
            .HasColumnName("SKU")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.VariantNameLocal)
            .HasColumnName("VARIANT_NAME_LOCAL")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.VariantNameEn)
            .HasColumnName("VARIANT_NAME_EN")
            .HasMaxLength(255);

        builder.Property(e => e.Barcode)
            .HasColumnName("BARCODE")
            .HasMaxLength(100);

        builder.Property(e => e.AdditionalPrice)
            .HasColumnName("ADDITIONAL_PRICE")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m);

        builder.Property(e => e.CostPrice)
            .HasColumnName("COST_PRICE")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m);

        builder.Property(e => e.ImageBase64)
            .HasColumnName("IMAGE_BASE64")
            .HasColumnType("CLOB");

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .HasColumnType("TIMESTAMP");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE")
            .HasColumnType("TIMESTAMP");

        builder.HasIndex(e => e.Sku);
        builder.HasIndex(e => e.Barcode);

        builder.HasMany(e => e.AttributeValues)
            .WithOne(v => v.Variant)
            .HasForeignKey(v => v.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
