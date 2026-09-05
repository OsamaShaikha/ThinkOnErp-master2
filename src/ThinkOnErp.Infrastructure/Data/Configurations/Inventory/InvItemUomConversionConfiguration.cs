using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvItemUomConversionConfiguration : IEntityTypeConfiguration<InvItemUomConversion>
{
    public void Configure(EntityTypeBuilder<InvItemUomConversion> builder)
    {
        builder.ToTable("INV_ITEM_UOM");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ItemId)
            .HasColumnName("ITEM_ID")
            .IsRequired();

        builder.Property(e => e.UomCode)
            .HasColumnName("UOM_CODE")
            .HasColumnType("NUMBER(6)")
            .IsRequired();

        builder.Property(e => e.ConversionFactor)
            .HasColumnName("CONVERSION_FACTOR")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(e => e.IsDefaultPurchase)
            .HasColumnName("IS_DEFAULT_PURCHASE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.IsDefaultSales)
            .HasColumnName("IS_DEFAULT_SALES")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

    }
}