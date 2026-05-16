using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysScreen entity.
/// Maps to SYS_SCREEN table in Oracle database.
/// </summary>
public class ScreenConfiguration : IEntityTypeConfiguration<SysScreen>
{
    public void Configure(EntityTypeBuilder<SysScreen> builder)
    {
        // Table mapping
        builder.ToTable("SYS_SCREEN");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_SCREEN.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Foreign keys
        builder.Property(e => e.SystemId)
            .HasColumnName("SYSTEM_ID")
            .IsRequired();

        builder.Property(e => e.ParentScreenId)
            .HasColumnName("PARENT_SCREEN_ID");

        // Required properties
        builder.Property(e => e.ScreenCode)
            .HasColumnName("SCREEN_CODE")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ScreenName)
            .HasColumnName("SCREEN_NAME")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ScreenNameE)
            .HasColumnName("SCREEN_NAME_E")
            .HasMaxLength(200)
            .IsRequired();

        // Optional properties
        builder.Property(e => e.Route)
            .HasColumnName("ROUTE")
            .HasMaxLength(500);

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

        // Foreign key relationships
        builder.HasOne(e => e.System)
            .WithMany()
            .HasForeignKey(e => e.SystemId)
            .HasConstraintName("FK_SCREEN_SYSTEM")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ParentScreen)
            .WithMany()
            .HasForeignKey(e => e.ParentScreenId)
            .HasConstraintName("FK_SCREEN_PARENT")
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes
        builder.HasIndex(e => e.ScreenCode)
            .IsUnique()
            .HasDatabaseName("UK_SCREEN_CODE");

        builder.HasIndex(e => e.SystemId)
            .HasDatabaseName("IDX_SCREEN_SYSTEM");

        builder.HasIndex(e => e.DisplayOrder)
            .HasDatabaseName("IDX_SCREEN_DISPLAY_ORDER");
    }
}
