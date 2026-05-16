using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysSuperAdmin entity.
/// Maps to SYS_SUPER_ADMIN table in Oracle database.
/// Configures super admin accounts with full platform access and 2FA support.
/// </summary>
public class SuperAdminConfiguration : IEntityTypeConfiguration<SysSuperAdmin>
{
    public void Configure(EntityTypeBuilder<SysSuperAdmin> builder)
    {
        // Table mapping
        builder.ToTable("SYS_SUPER_ADMIN");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_SUPER_ADMIN.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.RowDesc)
            .HasColumnName("ROW_DESC")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.RowDescE)
            .HasColumnName("ROW_DESC_E")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.UserName)
            .HasColumnName("USER_NAME")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Password)
            .HasColumnName("PASSWORD")
            .HasMaxLength(500)
            .IsRequired();

        // Optional properties
        builder.Property(e => e.Email)
            .HasColumnName("EMAIL")
            .HasMaxLength(200);

        builder.Property(e => e.Phone)
            .HasColumnName("PHONE")
            .HasMaxLength(50);

        builder.Property(e => e.TwoFaSecret)
            .HasColumnName("TWO_FA_SECRET")
            .HasMaxLength(500);

        // Boolean properties with Y/N conversion
        builder.Property(e => e.TwoFaEnabled)
            .HasColumnName("TWO_FA_ENABLED")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        // Date properties
        builder.Property(e => e.LastLoginDate)
            .HasColumnName("LAST_LOGIN_DATE");

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

        // Indexes
        builder.HasIndex(e => e.UserName)
            .IsUnique()
            .HasDatabaseName("UK_SUPER_ADMIN_USERNAME");

        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("UK_SUPER_ADMIN_EMAIL");

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);
    }
}
