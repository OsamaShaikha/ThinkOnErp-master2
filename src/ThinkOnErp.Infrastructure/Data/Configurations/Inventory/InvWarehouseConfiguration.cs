using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

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
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.WarehouseNameAr)
            .HasColumnName("WAREHOUSE_NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.WarehouseNameEn)
            .HasColumnName("WAREHOUSE_NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.WarehouseType)
            .HasColumnName("WAREHOUSE_TYPE")
            .HasConversion<string>()
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
}