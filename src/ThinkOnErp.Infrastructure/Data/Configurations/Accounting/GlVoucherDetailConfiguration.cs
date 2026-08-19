using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlVoucherDetailConfiguration : IEntityTypeConfiguration<GlVoucherDetail>
{
    public void Configure(EntityTypeBuilder<GlVoucherDetail> builder)
    {
        builder.ToTable("GL_VOUCHER_DETAIL");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(d => d.VoucherId)
            .HasColumnName("VOUCHER_ID")
            .IsRequired();

        builder.Property(d => d.LineSer)
            .HasColumnName("LINE_SER")
            .IsRequired();

        builder.Property(d => d.AccountCode)
            .HasColumnName("ACCOUNT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Debit)
            .HasColumnName("DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.Credit)
            .HasColumnName("CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.LocalDebit)
            .HasColumnName("LOCAL_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.LocalCredit)
            .HasColumnName("LOCAL_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.BaseDebit)
            .HasColumnName("BASE_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.BaseCredit)
            .HasColumnName("BASE_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(1000);

        builder.Property(d => d.CurrencyId)
            .HasColumnName("CURRENCY_ID")
            .HasDefaultValue(1L);

        builder.Property(d => d.ExchangeRate)
            .HasColumnName("EXCHANGE_RATE")
            .HasColumnType("NUMBER(14,6)")
            .HasDefaultValue(1.0m);

        builder.Property(d => d.CostCenterCode)
            .HasColumnName("COST_CENTER_CODE")
            .HasMaxLength(50);

        builder.Property(d => d.CostCenterMgrCode)
            .HasColumnName("COST_CENTER_MGR_CODE")
            .HasMaxLength(50);

        builder.Property(d => d.CostCenterMnrCode)
            .HasColumnName("COST_CENTER_MNR_CODE")
            .HasMaxLength(50);

        builder.Property(d => d.IsSettlement)
            .HasColumnName("IS_SETTLEMENT")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false);

        builder.Property(d => d.PartyType)
            .HasColumnName("PARTY_TYPE")
            .HasMaxLength(20);

        builder.Property(d => d.PartyCode)
            .HasColumnName("PARTY_CODE")
            .HasMaxLength(50);

        builder.Property(d => d.BranchId)
            .HasColumnName("BRANCH_ID");

        builder.HasOne(d => d.Account)
            .WithMany()
            .HasForeignKey(d => d.AccountCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.CostCenter)
            .WithMany()
            .HasForeignKey(d => d.CostCenterCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Branch)
            .WithMany()
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
