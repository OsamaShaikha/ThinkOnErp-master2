using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysBranch entity.
/// Maps to SYS_BRANCH table in Oracle database.
/// </summary>
public class BranchConfiguration : IEntityTypeConfiguration<SysBranch>
{
    public void Configure(EntityTypeBuilder<SysBranch> builder)
    {
        // Table mapping
        builder.ToTable("SYS_BRANCH");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_BRANCH.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Foreign key to parent company
        builder.Property(e => e.ParRowId)
            .HasColumnName("PAR_ROW_ID");

        // Required properties
        builder.Property(e => e.RowDesc)
            .HasColumnName("ROW_DESC")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.RowDescE)
            .HasColumnName("ROW_DESC_E")
            .HasMaxLength(200)
            .IsRequired();

        // Optional contact properties
        builder.Property(e => e.Phone)
            .HasColumnName("BRANCH_PHONE")
            .HasMaxLength(50);

        builder.Property(e => e.Mobile)
            .HasColumnName("BRANCH_MOBILE")
            .HasMaxLength(50);

        builder.Property(e => e.Fax)
            .HasColumnName("BRANCH_FAX")
            .HasMaxLength(50);

        builder.Property(e => e.Email)
            .HasColumnName("BRANCH_EMAIL")
            .HasMaxLength(100);

        // Branch settings
        builder.Property(e => e.IsHeadBranch)
            .HasColumnName("IS_HEAD_BRANCH")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.DefaultLang)
            .HasColumnName("DEFAULT_LANG")
            .HasMaxLength(10);

        builder.Property(e => e.BaseCurrencyId)
            .HasColumnName("BASE_CURRENCY_ID");

        builder.Property(e => e.RoundingRules)
            .HasColumnName("ROUNDING_RULES");

        // BLOB property
        builder.Property(e => e.BranchLogo)
            .HasColumnName("BRANCH_LOGO")
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
        builder.HasOne(e => e.BaseCurrency)
            .WithMany()
            .HasForeignKey(e => e.BaseCurrencyId)
            .HasConstraintName("FK_BRANCH_CURRENCY")
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Ignore computed properties
        builder.Ignore(e => e.HasLogo);
    }
}
