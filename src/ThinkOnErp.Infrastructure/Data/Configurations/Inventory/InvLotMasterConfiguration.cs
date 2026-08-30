using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvLotMasterConfiguration : IEntityTypeConfiguration<InvLotMaster>
{
    public void Configure(EntityTypeBuilder<InvLotMaster> builder)
    {
        builder.ToTable("INV_LOT_MASTER");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ItemId)
            .HasColumnName("ITEM_ID")
            .IsRequired();

        builder.Property(e => e.LotNumber)
            .HasColumnName("LOT_NUMBER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.SupplierLot)
            .HasColumnName("SUPPLIER_LOT")
            .HasMaxLength(100);

        builder.Property(e => e.ManufacturingDate)
            .HasColumnName("MANUFACTURING_DATE");

        builder.Property(e => e.ExpiryDate)
            .HasColumnName("EXPIRY_DATE");

        builder.Property(e => e.Status)
            .HasColumnName("STATUS")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

    }
}