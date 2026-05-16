using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysRole entity.
/// Maps to SYS_ROLE table in Oracle database.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<SysRole>
{
    public void Configure(EntityTypeBuilder<SysRole> builder)
    {
        // Table mapping
        builder.ToTable("SYS_ROLE");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_ROLE.NEXTVAL")
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

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        // Optional properties
        builder.Property(e => e.Note)
            .HasColumnName("NOTE")
            .HasMaxLength(500);

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // IS_ACTIVE with Y/N to bool conversion
        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);
    }
}
