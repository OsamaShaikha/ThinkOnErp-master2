using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysSystem entity.
/// Maps to SYS_SYSTEM table in Oracle database.
/// </summary>
public class SystemConfiguration : IEntityTypeConfiguration<SysSystem>
{
    public void Configure(EntityTypeBuilder<SysSystem> builder)
    {
        // Table mapping
        builder.ToTable("SYS_SYSTEM");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_SYSTEM.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.SystemCode)
            .HasColumnName("SYSTEM_CODE")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.SystemName)
            .HasColumnName("SYSTEM_NAME")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.SystemNameE)
            .HasColumnName("SYSTEM_NAME_E")
            .HasMaxLength(200)
            .IsRequired();

        // Optional properties
        builder.Property(e => e.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(500);

        builder.Property(e => e.DescriptionE)
            .HasColumnName("DESCRIPTION_E")
            .HasMaxLength(500);

        builder.Property(e => e.Icon)
            .HasColumnName("ICON")
            .HasMaxLength(100);

        builder.Property(e => e.DisplayOrder)
            .HasColumnName("DISPLAY_ORDER")
            .IsRequired();

        // IS_ACTIVE with Y/N to bool conversion
        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        // Audit properties
        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes
        builder.HasIndex(e => e.SystemCode)
            .IsUnique()
            .HasDatabaseName("UK_SYSTEM_CODE");

        builder.HasIndex(e => e.DisplayOrder)
            .HasDatabaseName("IDX_SYSTEM_DISPLAY_ORDER");
    }
}
