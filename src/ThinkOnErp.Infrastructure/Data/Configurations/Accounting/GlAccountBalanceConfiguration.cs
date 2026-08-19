using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlAccountBalanceConfiguration : IEntityTypeConfiguration<GlAccountBalance>
{
    public void Configure(EntityTypeBuilder<GlAccountBalance> builder)
    {
        builder.ToTable("GL_ACCOUNT_BALANCE");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.AccountCode)
            .HasColumnName("ACCOUNT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(e => e.FiscalYearId)
            .HasColumnName("FISCAL_YEAR_ID")
            .IsRequired();

        builder.Property(e => e.FiscalPeriodId)
            .HasColumnName("FISCAL_PERIOD_ID")
            .IsRequired();

        builder.Property(e => e.CurrencyId)
            .HasColumnName("CURRENCY_ID")
            .IsRequired();

        // Foreign Currency Amounts
        builder.Property(e => e.OpeningDebit)
            .HasColumnName("OPENING_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.OpeningCredit)
            .HasColumnName("OPENING_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.PeriodDebit)
            .HasColumnName("PERIOD_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.PeriodCredit)
            .HasColumnName("PERIOD_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.ClosingDebit)
            .HasColumnName("CLOSING_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.ClosingCredit)
            .HasColumnName("CLOSING_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        // Local / Base Currency Amounts
        builder.Property(e => e.LocalOpeningDebit)
            .HasColumnName("LOCAL_OPENING_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.LocalOpeningCredit)
            .HasColumnName("LOCAL_OPENING_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.LocalPeriodDebit)
            .HasColumnName("LOCAL_PERIOD_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.LocalPeriodCredit)
            .HasColumnName("LOCAL_PERIOD_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.LocalClosingDebit)
            .HasColumnName("LOCAL_CLOSING_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(e => e.LocalClosingCredit)
            .HasColumnName("LOCAL_CLOSING_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        // Audit Fields
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

        // Unique Constraint across Dimensions
        builder.HasIndex(e => new { e.AccountCode, e.BranchId, e.FiscalYearId, e.FiscalPeriodId, e.CurrencyId })
            .IsUnique()
            .HasDatabaseName("UX_GL_ACC_BAL_DIM");

        // Additional Query Indexes
        builder.HasIndex(e => new { e.FiscalYearId, e.FiscalPeriodId, e.BranchId })
            .HasDatabaseName("IX_GL_ACC_BAL_PERIOD");

        // Relationships
        builder.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.AccountCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FiscalYear)
            .WithMany()
            .HasForeignKey(e => e.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FiscalPeriod)
            .WithMany()
            .HasForeignKey(e => e.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
