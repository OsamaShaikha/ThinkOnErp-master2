using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysUser entity.
/// Maps to SYS_USERS table in Oracle database.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<SysUser>
{
    public void Configure(EntityTypeBuilder<SysUser> builder)
    {
        // Table mapping
        builder.ToTable("SYS_USERS");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_USERS.NEXTVAL")
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

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        // Optional properties
        builder.Property(e => e.Phone)
            .HasColumnName("PHONE")
            .HasMaxLength(50);

        builder.Property(e => e.Phone2)
            .HasColumnName("PHONE2")
            .HasMaxLength(50);

        builder.Property(e => e.Email)
            .HasColumnName("EMAIL")
            .HasMaxLength(200);

        builder.Property(e => e.RefreshToken)
            .HasColumnName("REFRESH_TOKEN")
            .HasMaxLength(500);

        builder.Property(e => e.RefreshTokenExpiry)
            .HasColumnName("REFRESH_TOKEN_EXPIRY");

        builder.Property(e => e.ForceLogoutDate)
            .HasColumnName("FORCE_LOGOUT_DATE");

        builder.Property(e => e.LastLoginDate)
            .HasColumnName("LAST_LOGIN_DATE");

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // Foreign keys
        builder.Property(e => e.Role)
            .HasColumnName("ROLE");

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID");

        // Boolean conversions for Y/N fields
        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.IsAdmin)
            .HasColumnName("IS_ADMIN")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes
        builder.HasIndex(e => e.UserName)
            .IsUnique()
            .HasDatabaseName("UK_USER_NAME");

        builder.HasIndex(e => e.RefreshToken)
            .HasDatabaseName("IDX_USER_REFRESH_TOKEN");
    }
}
