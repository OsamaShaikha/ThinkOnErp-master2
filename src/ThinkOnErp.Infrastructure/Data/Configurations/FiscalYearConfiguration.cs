using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysFiscalYear entity.
/// Maps to SYS_FISCAL_YEAR table in Oracle database.
/// </summary>
public class FiscalYearConfiguration : IEntityTypeConfiguration<SysFiscalYear>
{
    public void Configure(EntityTypeBuilder<SysFiscalYear> builder)
    {
        // Table mapping
        builder.ToTable("SYS_FISCAL_YEAR");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_FISCAL_YEAR.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.CompanyId)
            .HasColumnName("COMPANY_ID")
            .IsRequired();

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(e => e.FiscalYearCode)
            .HasColumnName("FISCAL_YEAR_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.StartDate)
            .HasColumnName("START_DATE")
            .IsRequired();

        builder.Property(e => e.EndDate)
            .HasColumnName("END_DATE")
            .IsRequired();

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        // Optional properties
        builder.Property(e => e.RowDesc)
            .HasColumnName("ROW_DESC")
            .HasMaxLength(200);

        builder.Property(e => e.RowDescE)
            .HasColumnName("ROW_DESC_E")
            .HasMaxLength(200);

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // Boolean conversions for Y/N fields
        builder.Property(e => e.IsClosed)
            .HasColumnName("IS_CLOSED")
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

        // Foreign key relationships
        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .HasConstraintName("FK_FISCAL_YEAR_COMPANY")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .HasConstraintName("FK_FISCAL_YEAR_BRANCH")
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes
        builder.HasIndex(e => new { e.BranchId, e.FiscalYearCode })
            .IsUnique()
            .HasDatabaseName("UK_FISCAL_YEAR_CODE");
    }
}
