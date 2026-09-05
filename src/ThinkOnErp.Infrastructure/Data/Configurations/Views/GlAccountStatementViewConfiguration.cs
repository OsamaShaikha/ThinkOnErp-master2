using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Views;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Views;

public sealed class GlAccountStatementViewConfiguration : IEntityTypeConfiguration<GlAccountStatementView>
{
    public void Configure(EntityTypeBuilder<GlAccountStatementView> builder)
    {
        builder.ToView("VW_GL_ACCOUNT_STATEMENT");
        builder.HasNoKey();

        builder.Property(e => e.DetailId).HasColumnName("DETAIL_ID");
        builder.Property(e => e.VoucherId).HasColumnName("VOUCHER_ID");
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(e => e.FiscalYearId).HasColumnName("FISCAL_YEAR_ID");
        builder.Property(e => e.VoucherYear).HasColumnName("VOUCHER_YEAR");
        builder.Property(e => e.VoucherMonth).HasColumnName("VOUCHER_MONTH");
        builder.Property(e => e.VoucherType).HasColumnName("VOUCHER_TYPE");
        builder.Property(e => e.VoucherNo).HasColumnName("VOUCHER_NO");
        builder.Property(e => e.VoucherDate).HasColumnName("VOUCHER_DATE");
        builder.Property(e => e.VoucherStatus).HasColumnName("VOUCHER_STATUS");
        builder.Property(e => e.AccountCode).HasColumnName("ACCOUNT_CODE").HasMaxLength(50);
        builder.Property(e => e.AccountNameLocal).HasColumnName("ACCOUNT_NAME_LOCAL").HasMaxLength(200);
        builder.Property(e => e.AccountNameEn).HasColumnName("ACCOUNT_NAME_EN").HasMaxLength(200);
        builder.Property(e => e.AccountType).HasColumnName("ACCOUNT_TYPE").HasMaxLength(50);
        builder.Property(e => e.NormalBalance).HasColumnName("NORMAL_BALANCE").HasMaxLength(20);
        builder.Property(e => e.LineSer).HasColumnName("LINE_SER");
        builder.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
        builder.Property(e => e.CurrencyId).HasColumnName("CURRENCY_ID");
        builder.Property(e => e.ExchangeRate).HasColumnName("EXCHANGE_RATE").HasColumnType("NUMBER(18,6)");
        builder.Property(e => e.Debit).HasColumnName("DEBIT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.Credit).HasColumnName("CREDIT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.LocalDebit).HasColumnName("LOCAL_DEBIT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.LocalCredit).HasColumnName("LOCAL_CREDIT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.NetLocalAmount).HasColumnName("NET_LOCAL_AMOUNT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.CostCenterCode).HasColumnName("COST_CENTER_CODE").HasMaxLength(50);
        builder.Property(e => e.CostCenterNameLocal).HasColumnName("COST_CENTER_NAME_LOCAL").HasMaxLength(200);
        builder.Property(e => e.PartyType).HasColumnName("PARTY_TYPE").HasMaxLength(50);
        builder.Property(e => e.PartyCode).HasColumnName("PARTY_CODE").HasMaxLength(50);
        builder.Property(e => e.RunningBalance).HasColumnName("RUNNING_BALANCE").HasColumnType("NUMBER(18,4)");
    }
}
