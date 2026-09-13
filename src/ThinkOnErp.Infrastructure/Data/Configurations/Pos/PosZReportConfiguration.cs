using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosZReportConfiguration : IEntityTypeConfiguration<PosZReport>
{
    public void Configure(EntityTypeBuilder<PosZReport> builder)
    {
        builder.ToTable("POS_Z_REPORT");
        builder.HasKey(z => z.Id);

        builder.Property(z => z.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(z => z.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(z => z.TillId).HasColumnName("TILL_ID");
        builder.Property(z => z.ShiftId).HasColumnName("SHIFT_ID");
        builder.Property(z => z.ZSequenceNumber).HasColumnName("Z_SEQUENCE_NUMBER").IsRequired();
        builder.Property(z => z.ReportDate).HasColumnName("REPORT_DATE").HasColumnType("TIMESTAMP").IsRequired();

        builder.Property(z => z.FirstInvoiceNumber).HasColumnName("FIRST_INVOICE_NUMBER").HasMaxLength(50).IsRequired();
        builder.Property(z => z.LastInvoiceNumber).HasColumnName("LAST_INVOICE_NUMBER").HasMaxLength(50).IsRequired();
        builder.Property(z => z.TotalInvoiceCount).HasColumnName("TOTAL_INVOICE_COUNT").HasDefaultValue(0);
        builder.Property(z => z.TotalReturnCount).HasColumnName("TOTAL_RETURN_COUNT").HasDefaultValue(0);

        builder.Property(z => z.GrossSalesAmount).HasColumnName("GROSS_SALES_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.TotalDiscountAmount).HasColumnName("TOTAL_DISCOUNT_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.NetSalesAmount).HasColumnName("NET_SALES_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.TotalTaxAmount).HasColumnName("TOTAL_TAX_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.TotalServiceCharge).HasColumnName("TOTAL_SERVICE_CHARGE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.TotalRefundAmount).HasColumnName("TOTAL_REFUND_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.FinalTotalAmount).HasColumnName("FINAL_TOTAL_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.Property(z => z.CashPaymentsTotal).HasColumnName("CASH_PAYMENTS_TOTAL").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.CardPaymentsTotal).HasColumnName("CARD_PAYMENTS_TOTAL").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.CustomerAccountPaymentsTotal).HasColumnName("CUSTOMER_ACCOUNT_TOTAL").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.OtherPaymentsTotal).HasColumnName("OTHER_PAYMENTS_TOTAL").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.Property(z => z.OpeningFloat).HasColumnName("OPENING_FLOAT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.CashDropTotal).HasColumnName("CASH_DROP_TOTAL").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.PayOutTotal).HasColumnName("PAY_OUT_TOTAL").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.ExpectedDrawerCash).HasColumnName("EXPECTED_DRAWER_CASH").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.ActualCountedCash).HasColumnName("ACTUAL_COUNTED_CASH").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(z => z.CashVariance).HasColumnName("CASH_VARIANCE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.Property(z => z.IsPostedToGl).HasColumnName("IS_POSTED_TO_GL").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(z => z.GlVoucherId).HasColumnName("GL_VOUCHER_ID");

        builder.Property(z => z.GeneratedBy).HasColumnName("GENERATED_BY").HasMaxLength(100).IsRequired();
        builder.Property(z => z.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(z => new { z.BranchId, z.ZSequenceNumber }).IsUnique();
        builder.HasOne(z => z.Branch).WithMany().HasForeignKey(z => z.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(z => z.Till).WithMany().HasForeignKey(z => z.TillId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(z => z.Shift).WithMany().HasForeignKey(z => z.ShiftId).OnDelete(DeleteBehavior.SetNull);
    }
}
