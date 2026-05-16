using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysRoleScreenPermission entity.
/// Maps to SYS_ROLE_SCREEN_PERMISSION table in Oracle database.
/// </summary>
public class RoleScreenPermissionConfiguration : IEntityTypeConfiguration<SysRoleScreenPermission>
{
    public void Configure(EntityTypeBuilder<SysRoleScreenPermission> builder)
    {
        // Table mapping
        builder.ToTable("SYS_ROLE_SCREEN_PERMISSION");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_ROLE_SCREEN_PERMISSION.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Foreign keys
        builder.Property(e => e.RoleId)
            .HasColumnName("ROLE_ID")
            .IsRequired();

        builder.Property(e => e.ScreenId)
            .HasColumnName("SCREEN_ID")
            .IsRequired();

        // Permission flags with Y/N to bool conversion
        builder.Property(e => e.CanView)
            .HasColumnName("CAN_VIEW")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.CanInsert)
            .HasColumnName("CAN_INSERT")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.CanUpdate)
            .HasColumnName("CAN_UPDATE")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.CanDelete)
            .HasColumnName("CAN_DELETE")
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
        builder.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .HasConstraintName("FK_ROLE_SCREEN_PERM_ROLE")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Screen)
            .WithMany()
            .HasForeignKey(e => e.ScreenId)
            .HasConstraintName("FK_ROLE_SCREEN_PERM_SCREEN")
            .OnDelete(DeleteBehavior.Cascade);

        // Composite unique index to prevent duplicate role-screen combinations
        builder.HasIndex(e => new { e.RoleId, e.ScreenId })
            .IsUnique()
            .HasDatabaseName("UK_ROLE_SCREEN");
    }
}
