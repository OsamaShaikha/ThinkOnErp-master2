using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BANK_ACCOUNT");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.AccountNumber).HasColumnName("ACCOUNT_NUMBER").HasMaxLength(50).IsRequired();
        builder.Property(e => e.AccountNameLocal).HasColumnName("ACCOUNT_NAME_LOCAL").HasMaxLength(200).IsRequired();
        builder.Property(e => e.AccountNameEn).HasColumnName("ACCOUNT_NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.BankName).HasColumnName("BANK_NAME").HasMaxLength(150).IsRequired();
        builder.Property(e => e.BankBranchName).HasColumnName("BANK_BRANCH_NAME").HasMaxLength(150);
        builder.Property(e => e.Iban).HasColumnName("IBAN").HasMaxLength(50);
        builder.Property(e => e.SwiftCode).HasColumnName("SWIFT_CODE").HasMaxLength(30);

        builder.Property(e => e.CurrencyId).HasColumnName("CURRENCY_ID").IsRequired();
        builder.Property(e => e.GlAccountCode).HasColumnName("GL_ACCOUNT_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.OverdraftLimit).HasColumnName("OVERDRAFT_LIMIT").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.OpeningBalance).HasColumnName("OPENING_BALANCE").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.CurrentBalance).HasColumnName("CURRENT_BALANCE").HasPrecision(18, 3).IsRequired();

        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(50);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(e => e.AccountNumber).IsUnique().HasDatabaseName("UX_BANK_ACC_NUM");

        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Currency).WithMany().HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.GlAccount).WithMany().HasForeignKey(e => e.GlAccountCode).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
{
    public void Configure(EntityTypeBuilder<CashRegister> builder)
    {
        builder.ToTable("CASH_REGISTER");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.Code).HasColumnName("CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();

        builder.Property(e => e.RegisterType).HasColumnName("REGISTER_TYPE").HasMaxLength(30).IsRequired();
        builder.Property(e => e.CustodianName).HasColumnName("CUSTODIAN_NAME").HasMaxLength(150);
        builder.Property(e => e.GlAccountCode).HasColumnName("GL_ACCOUNT_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CurrencyId).HasColumnName("CURRENCY_ID").IsRequired();

        builder.Property(e => e.MinLimit).HasColumnName("MIN_LIMIT").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.MaxLimit).HasColumnName("MAX_LIMIT").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.OpeningBalance).HasColumnName("OPENING_BALANCE").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.CurrentBalance).HasColumnName("CURRENT_BALANCE").HasPrecision(18, 3).IsRequired();

        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(50);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_CASH_REG_CODE");

        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Currency).WithMany().HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.GlAccount).WithMany().HasForeignKey(e => e.GlAccountCode).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> builder)
    {
        builder.ToTable("BANK_RECONCILIATION");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.BankAccountId).HasColumnName("BANK_ACCOUNT_ID").IsRequired();
        builder.Property(e => e.FiscalYearId).HasColumnName("FISCAL_YEAR_ID").IsRequired();
        builder.Property(e => e.FiscalPeriodId).HasColumnName("FISCAL_PERIOD_ID").IsRequired();

        builder.Property(e => e.StatementDate).HasColumnName("STATEMENT_DATE").IsRequired();
        builder.Property(e => e.StatementEndingBalance).HasColumnName("STATEMENT_ENDING_BALANCE").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.BookEndingBalance).HasColumnName("BOOK_ENDING_BALANCE").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.TotalReconciledAmount).HasColumnName("TOTAL_RECONCILED_AMOUNT").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.UnreconciledDifference).HasColumnName("UNRECONCILED_DIFFERENCE").HasPrecision(18, 3).IsRequired();

        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(30).IsRequired();
        builder.Property(e => e.Notes).HasColumnName("NOTES").HasMaxLength(500);

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(50);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.BankAccount).WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.FiscalYear).WithMany().HasForeignKey(e => e.FiscalYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.FiscalPeriod).WithMany().HasForeignKey(e => e.FiscalPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.StatementLines).WithOne(l => l.Reconciliation).HasForeignKey(l => l.ReconciliationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> builder)
    {
        builder.ToTable("BANK_STATEMENT_LINE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.ReconciliationId).HasColumnName("RECONCILIATION_ID").IsRequired();
        builder.Property(e => e.TransactionDate).HasColumnName("TRANSACTION_DATE").IsRequired();
        builder.Property(e => e.ValueDate).HasColumnName("VALUE_DATE");
        builder.Property(e => e.ReferenceNo).HasColumnName("REFERENCE_NO").HasMaxLength(100);
        builder.Property(e => e.ChequeNo).HasColumnName("CHEQUE_NO").HasMaxLength(50);
        builder.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(500).IsRequired();

        builder.Property(e => e.Debit).HasColumnName("DEBIT").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.Credit).HasColumnName("CREDIT").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.Balance).HasColumnName("BALANCE").HasPrecision(18, 3).IsRequired();

        builder.Property(e => e.IsReconciled).HasColumnName("IS_RECONCILED").IsRequired();
        builder.Property(e => e.ReconciledDate).HasColumnName("RECONCILED_DATE");
        builder.Property(e => e.MatchedVoucherDetailId).HasColumnName("MATCHED_VOUCHER_DETAIL_ID");

        builder.HasOne(e => e.MatchedVoucherDetail).WithMany().HasForeignKey(e => e.MatchedVoucherDetailId).OnDelete(DeleteBehavior.SetNull);
    }
}
