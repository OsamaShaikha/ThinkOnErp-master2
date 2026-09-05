using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvZoneConfiguration : IEntityTypeConfiguration<InvZone>
{
    public void Configure(EntityTypeBuilder<InvZone> builder)
    {
        builder.ToTable("INV_ZONE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.WarehouseId)
            .HasColumnName("WAREHOUSE_ID")
            .IsRequired();

        builder.Property(e => e.ZoneCode)
            .HasColumnName("ZONE_CODE")
            .HasColumnType("NUMBER(6)")
            .IsRequired();

        builder.Property(e => e.ZoneName)
            .HasColumnName("ZONE_NAME")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ZoneType)
            .HasColumnName("ZONE_TYPE")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

    }
}