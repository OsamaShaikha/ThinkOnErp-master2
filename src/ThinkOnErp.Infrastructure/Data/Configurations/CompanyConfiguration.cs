using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysCompany entity.
/// Maps to SYS_COMPANY table in Oracle database.
/// </summary>
public class CompanyConfiguration : IEntityTypeConfiguration<SysCompany>
{
    public void Configure(EntityTypeBuilder<SysCompany> builder)
    {
        // Table mapping
        builder.ToTable("SYS_COMPANY");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_COMPANY.NEXTVAL")
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

        // Optional properties
        builder.Property(e => e.LegalName)
            .HasColumnName("LEGAL_NAME")
            .HasMaxLength(300);

        builder.Property(e => e.LegalNameE)
            .HasColumnName("LEGAL_NAME_E")
            .HasMaxLength(300);

        builder.Property(e => e.CompanyCode)
            .HasColumnName("COMPANY_CODE")
            .HasMaxLength(50);

        builder.Property(e => e.TaxNumber)
            .HasColumnName("TAX_NUMBER")
            .HasMaxLength(50);

        // Foreign keys
        builder.Property(e => e.CountryId)
            .HasColumnName("COUNTRY_ID");

        builder.Property(e => e.CurrId)
            .HasColumnName("CURR_ID");

        builder.Property(e => e.DefaultBranchId)
            .HasColumnName("DEFAULT_BRANCH_ID");

        // BLOB property
        builder.Property(e => e.CompanyLogo)
            .HasColumnName("COMPANY_LOGO")
            .HasColumnType("BLOB");

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
        builder.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrId)
            .HasConstraintName("FK_COMPANY_CURRENCY")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.DefaultBranch)
            .WithMany()
            .HasForeignKey(e => e.DefaultBranchId)
            .HasConstraintName("FK_COMPANY_DEFAULT_BRANCH")
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes
        builder.HasIndex(e => e.CompanyCode)
            .IsUnique()
            .HasDatabaseName("UK_COMPANY_CODE");

        // Ignore computed properties
        builder.Ignore(e => e.HasLogo);
    }
}
