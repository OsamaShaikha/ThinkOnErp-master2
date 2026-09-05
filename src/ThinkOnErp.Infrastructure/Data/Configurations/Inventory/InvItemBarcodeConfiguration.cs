using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvItemBarcodeConfiguration : IEntityTypeConfiguration<InvItemBarcode>
{
    public void Configure(EntityTypeBuilder<InvItemBarcode> builder)
    {
        builder.ToTable("INV_ITEM_BARCODE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ItemId)
            .HasColumnName("ITEM_ID")
            .IsRequired();

        builder.Property(e => e.Barcode)
            .HasColumnName("BARCODE")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.BarcodeType)
            .HasColumnName("BARCODE_TYPE")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.UomCode)
            .HasColumnName("UOM_CODE")
            .HasColumnType("NUMBER(6)")
            .IsRequired();

    }
}