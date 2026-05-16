using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysUserRole entity.
/// Maps to SYS_USER_ROLE table in Oracle database.
/// </summary>
public class UserRoleConfiguration : IEntityTypeConfiguration<SysUserRole>
{
    public void Configure(EntityTypeBuilder<SysUserRole> builder)
    {
        // Table mapping
        builder.ToTable("SYS_USER_ROLE");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_USER_ROLE.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Foreign keys
        builder.Property(e => e.UserId)
            .HasColumnName("USER_ID")
            .IsRequired();

        builder.Property(e => e.RoleId)
            .HasColumnName("ROLE_ID")
            .IsRequired();

        // Optional properties
        builder.Property(e => e.AssignedBy)
            .HasColumnName("ASSIGNED_BY");

        builder.Property(e => e.AssignedDate)
            .HasColumnName("ASSIGNED_DATE");

        // Audit properties
        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        // Foreign key relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .HasConstraintName("FK_USER_ROLE_USER")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .HasConstraintName("FK_USER_ROLE_ROLE")
            .OnDelete(DeleteBehavior.Cascade);

        // Composite unique index to prevent duplicate user-role assignments
        builder.HasIndex(e => new { e.UserId, e.RoleId })
            .IsUnique()
            .HasDatabaseName("UK_USER_ROLE");
    }
}
