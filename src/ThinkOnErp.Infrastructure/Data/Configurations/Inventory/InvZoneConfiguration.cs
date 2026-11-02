using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

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
            .HasConversion(
                v => FormatZoneType(v),
                v => ParseZoneType(v))
            .HasMaxLength(50)
            .IsRequired();
    }

    private static string FormatZoneType(ZoneType v) => v switch
    {
        ZoneType.Storage => "STORAGE",
        ZoneType.Receiving => "RECEIVING",
        ZoneType.Staging => "STAGING",
        ZoneType.Quarantine => "QUARANTINE",
        ZoneType.Damaged => "DAMAGED",
        ZoneType.Returns => "RETURNS",
        _ => "STORAGE"
    };

    private static ZoneType ParseZoneType(string? v) => v?.ToUpperInvariant() switch
    {
        "STORAGE" or "1" => ZoneType.Storage,
        "RECEIVING" or "2" => ZoneType.Receiving,
        "STAGING" or "3" => ZoneType.Staging,
        "QUARANTINE" or "4" => ZoneType.Quarantine,
        "DAMAGED" or "5" => ZoneType.Damaged,
        "RETURNS" or "6" => ZoneType.Returns,
        _ => Enum.TryParse<ZoneType>(v, true, out var res) ? res : ZoneType.Storage
    };
}
