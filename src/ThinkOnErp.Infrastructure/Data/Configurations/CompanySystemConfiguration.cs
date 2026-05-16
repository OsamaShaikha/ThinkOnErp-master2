using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysCompanySystem entity.
/// Maps to SYS_COMPANY_SYSTEM table in Oracle database.
/// </summary>
public class CompanySystemConfiguration : IEntityTypeConfiguration<SysCompanySystem>
{
    public void Configure(EntityTypeBuilder<SysCompanySystem> builder)
    {
        // Table mapping
        builder.ToTable("SYS_COMPANY_SYSTEM");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_COMPANY_SYSTEM.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Foreign keys
        builder.Property(e => e.CompanyId)
            .HasColumnName("COMPANY_ID")
            .IsRequired();

        builder.Property(e => e.SystemId)
            .HasColumnName("SYSTEM_ID")
            .IsRequired();

        // IS_ALLOWED with Y/N to bool conversion
        builder.Property(e => e.IsAllowed)
            .HasColumnName("IS_ALLOWED")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        // Optional properties
        builder.Property(e => e.GrantedBy)
            .HasColumnName("GRANTED_BY");

        builder.Property(e => e.GrantedDate)
            .HasColumnName("GRANTED_DATE");

        builder.Property(e => e.RevokedDate)
            .HasColumnName("REVOKED_DATE");

        builder.Property(e => e.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(500);

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

        // Composite unique index to prevent duplicate company-system combinations
        builder.HasIndex(e => new { e.CompanyId, e.SystemId })
            .IsUnique()
            .HasDatabaseName("UK_COMPANY_SYSTEM");

        builder.HasIndex(e => e.CompanyId)
            .HasDatabaseName("IDX_COMPANY_SYSTEM_COMPANY");

        builder.HasIndex(e => e.SystemId)
            .HasDatabaseName("IDX_COMPANY_SYSTEM_SYSTEM");
    }
}
