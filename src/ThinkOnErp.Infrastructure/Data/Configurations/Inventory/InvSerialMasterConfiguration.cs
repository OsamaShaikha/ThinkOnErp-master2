using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvSerialMasterConfiguration : IEntityTypeConfiguration<InvSerialMaster>
{
    public void Configure(EntityTypeBuilder<InvSerialMaster> builder)
    {
        builder.ToTable("INV_SERIAL_MASTER");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ItemId)
            .HasColumnName("ITEM_ID")
            .IsRequired();

        builder.Property(e => e.SerialNumber)
            .HasColumnName("SERIAL_NUMBER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.LotId)
            .HasColumnName("LOT_ID");

        builder.Property(e => e.Status)
            .HasColumnName("STATUS")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CurrentWarehouseId)
            .HasColumnName("CURRENT_WAREHOUSE_ID");

        builder.Property(e => e.CurrentBinId)
            .HasColumnName("CURRENT_BIN_ID");

    }
}