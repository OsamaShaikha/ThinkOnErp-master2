using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlOpeningBalanceHeaderConfiguration : IEntityTypeConfiguration<GlOpeningBalanceHeader>
{
    public void Configure(EntityTypeBuilder<GlOpeningBalanceHeader> builder)
    {
        builder.ToTable("GL_OPENING_BALANCE_HEADER", tableBuilder =>
        {
            tableBuilder.ExcludeFromMigrations();
        });

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(h => h.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(h => h.FiscalYearId)
            .HasColumnName("FISCAL_YEAR_ID")
            .IsRequired();

        builder.Property(h => h.AsOfDate)
            .HasColumnName("AS_OF_DATE")
            .IsRequired();

        builder.Property(h => h.Description)
            .HasColumnName("DESCRIPTION")
            .HasColumnType("NVARCHAR2(500)")
            .HasMaxLength(500);

        builder.Property(h => h.Status)
            .HasColumnName("STATUS")
            .HasColumnType("NUMBER(3)")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(h => h.TotalDebit)
            .HasColumnName("TOTAL_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(h => h.TotalCredit)
            .HasColumnName("TOTAL_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(h => h.ObVoucherId)
            .HasColumnName("OB_VOUCHER_ID");

        builder.Property(h => h.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasColumnType("NVARCHAR2(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(h => h.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(h => h.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasColumnType("NVARCHAR2(100)")
            .HasMaxLength(100);

        builder.Property(h => h.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // Computed properties — ignored by EF
        builder.Ignore(h => h.IsDraft);
        builder.Ignore(h => h.IsConfirmed);

        // One OB per branch per fiscal year
        builder.HasIndex(h => new { h.BranchId, h.FiscalYearId })
            .IsUnique()
            .HasDatabaseName("UX_GL_OB_BRANCH_YEAR");

        // Navigation: one header → many details
        builder.HasMany(h => h.Details)
            .WithOne(d => d.Header)
            .HasForeignKey(d => d.HeaderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
