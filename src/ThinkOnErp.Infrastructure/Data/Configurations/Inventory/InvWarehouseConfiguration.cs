using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvWarehouseConfiguration : IEntityTypeConfiguration<InvWarehouse>
{
    public void Configure(EntityTypeBuilder<InvWarehouse> builder)
    {
        builder.ToTable("INV_WAREHOUSE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(e => e.WarehouseCode)
            .HasColumnName("WAREHOUSE_CODE")
            .HasColumnType("NUMBER(6)")
            .IsRequired();

        builder.Property(e => e.WarehouseNameLocal)
            .HasColumnName("WAREHOUSE_NAME_LOCAL")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.WarehouseNameEn)
            .HasColumnName("WAREHOUSE_NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.WarehouseType)
            .HasColumnName("WAREHOUSE_TYPE")
            .HasConversion(
                v => FormatWarehouseType(v),
                v => ParseWarehouseType(v))
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Address)
            .HasColumnName("ADDRESS")
            .HasMaxLength(100);

        builder.Property(e => e.EnableBinTracking)
            .HasColumnName("ENABLE_BIN_TRACKING")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

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

    private static string FormatWarehouseType(WarehouseType v) => v switch
    {
        WarehouseType.Main => "MAIN",
        WarehouseType.Branch => "BRANCH",
        WarehouseType.Transit => "TRANSIT",
        WarehouseType.Virtual => "VIRTUAL",
        _ => "MAIN"
    };

    private static WarehouseType ParseWarehouseType(string? v) => v?.ToUpperInvariant() switch
    {
        "MAIN" or "1" => WarehouseType.Main,
        "BRANCH" or "2" => WarehouseType.Branch,
        "TRANSIT" or "3" => WarehouseType.Transit,
        "VIRTUAL" or "4" => WarehouseType.Virtual,
        _ => Enum.TryParse<WarehouseType>(v, true, out var res) ? res : WarehouseType.Main
    };
}
