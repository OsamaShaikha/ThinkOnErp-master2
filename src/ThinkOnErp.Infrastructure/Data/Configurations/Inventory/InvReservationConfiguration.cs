using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvReservationConfiguration : IEntityTypeConfiguration<InvReservation>
{
    public void Configure(EntityTypeBuilder<InvReservation> builder)
    {
        builder.ToTable("INV_RESERVATION");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ItemId)
            .HasColumnName("ITEM_ID")
            .IsRequired();

        builder.Property(e => e.WarehouseId)
            .HasColumnName("WAREHOUSE_ID")
            .IsRequired();

        builder.Property(e => e.LotId)
            .HasColumnName("LOT_ID");

        builder.Property(e => e.SerialId)
            .HasColumnName("SERIAL_ID");

        builder.Property(e => e.ReservedQty)
            .HasColumnName("RESERVED_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.SourceDocType)
            .HasColumnName("SOURCE_DOC_TYPE")
            .HasMaxLength(100);

        builder.Property(e => e.SourceDocId)
            .HasColumnName("SOURCE_DOC_ID")
            .HasMaxLength(100);

        builder.Property(e => e.SourceLineId)
            .HasColumnName("SOURCE_LINE_ID")
            .HasMaxLength(100);

        builder.Property(e => e.Status)
            .HasColumnName("STATUS")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ExpiryDate)
            .HasColumnName("EXPIRY_DATE");

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

    }
}