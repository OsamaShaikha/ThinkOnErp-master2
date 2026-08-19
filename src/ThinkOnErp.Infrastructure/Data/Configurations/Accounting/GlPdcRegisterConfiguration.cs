using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlPdcRegisterConfiguration : IEntityTypeConfiguration<GlPdcRegister>
{
    public void Configure(EntityTypeBuilder<GlPdcRegister> builder)
    {
        builder.ToTable("GL_PDC_REGISTER");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.FiscalYearId).HasColumnName("FISCAL_YEAR_ID").IsRequired();

        builder.Property(e => e.ChequeType).HasColumnName("CHEQUE_TYPE").HasMaxLength(20).IsRequired();
        builder.Property(e => e.ChequeNumber).HasColumnName("CHEQUE_NO").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ChequeDate).HasColumnName("CHEQUE_DATE").IsRequired();
        builder.Property(e => e.DueDate).HasColumnName("DUE_DATE").IsRequired();

        builder.Property(e => e.Amount).HasColumnName("AMOUNT").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.LocalAmount).HasColumnName("LOCAL_AMOUNT").HasPrecision(18, 3).IsRequired();
        builder.Property(e => e.CurrencyId).HasColumnName("CURRENCY_ID").IsRequired();
        builder.Property(e => e.ExchangeRate).HasColumnName("EXCHANGE_RATE").HasPrecision(18, 6).IsRequired();

        builder.Property(e => e.DrawerBankName).HasColumnName("DRAWER_BANK_NAME").HasMaxLength(150).IsRequired();
        builder.Property(e => e.DrawerBankAccountNo).HasColumnName("DRAWER_BANK_ACC_NO").HasMaxLength(50);
        builder.Property(e => e.BeneficiaryName).HasColumnName("BENEFICIARY_NAME").HasMaxLength(150);

        builder.Property(e => e.PartyType).HasColumnName("PARTY_TYPE").HasMaxLength(20);
        builder.Property(e => e.PartyCode).HasColumnName("PARTY_CODE").HasMaxLength(50);

        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();

        builder.Property(e => e.IntermediateAccountCode).HasColumnName("INTERMEDIATE_ACCOUNT_CODE").HasMaxLength(50);
        builder.Property(e => e.DepositBankAccountCode).HasColumnName("DEPOSIT_BANK_ACCOUNT_CODE").HasMaxLength(50);
        builder.Property(e => e.DepositDate).HasColumnName("DEPOSIT_DATE");
        builder.Property(e => e.ClearedDate).HasColumnName("CLEARED_DATE");
        builder.Property(e => e.BouncedDate).HasColumnName("BOUNCED_DATE");
        builder.Property(e => e.BounceReason).HasColumnName("BOUNCE_REASON").HasMaxLength(250);

        builder.Property(e => e.OriginatingVoucherId).HasColumnName("ORIGINATING_VOUCHER_ID");
        builder.Property(e => e.ClearingVoucherId).HasColumnName("CLEARING_VOUCHER_ID");
        builder.Property(e => e.BounceVoucherId).HasColumnName("BOUNCE_VOUCHER_ID");

        builder.Property(e => e.Notes).HasColumnName("NOTES").HasMaxLength(500);

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(50);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        // Indexes
        builder.HasIndex(e => new { e.BranchId, e.ChequeType, e.ChequeNumber }).HasDatabaseName("IX_PDC_BR_TYPE_NO");
        builder.HasIndex(e => new { e.DueDate, e.Status }).HasDatabaseName("IX_PDC_DUE_STATUS");
        builder.HasIndex(e => new { e.PartyType, e.PartyCode }).HasDatabaseName("IX_PDC_PARTY");

        // Relationships
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.FiscalYear).WithMany().HasForeignKey(e => e.FiscalYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.OriginatingVoucher).WithMany().HasForeignKey(e => e.OriginatingVoucherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ClearingVoucher).WithMany().HasForeignKey(e => e.ClearingVoucherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.BounceVoucher).WithMany().HasForeignKey(e => e.BounceVoucherId).OnDelete(DeleteBehavior.Restrict);
    }
}
