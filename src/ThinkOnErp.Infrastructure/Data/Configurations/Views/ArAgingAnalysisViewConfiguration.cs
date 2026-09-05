using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Views;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Views;

public sealed class ArAgingAnalysisViewConfiguration : IEntityTypeConfiguration<ArAgingAnalysisView>
{
    public void Configure(EntityTypeBuilder<ArAgingAnalysisView> builder)
    {
        builder.ToView("VW_AR_AGING_ANALYSIS");
        builder.HasNoKey();

        builder.Property(e => e.TransactionId).HasColumnName("TRANSACTION_ID");
        builder.Property(e => e.CustomerCode).HasColumnName("CUSTOMER_CODE").HasMaxLength(50);
        builder.Property(e => e.CustomerNameLocal).HasColumnName("CUSTOMER_NAME_LOCAL").HasMaxLength(200);
        builder.Property(e => e.CustomerNameEn).HasColumnName("CUSTOMER_NAME_EN").HasMaxLength(200);
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(e => e.TransactionType).HasColumnName("TRANSACTION_TYPE").HasMaxLength(50);
        builder.Property(e => e.TransactionDate).HasColumnName("TRANSACTION_DATE");
        builder.Property(e => e.DueDate).HasColumnName("DUE_DATE");
        builder.Property(e => e.VoucherId).HasColumnName("VOUCHER_ID");
        builder.Property(e => e.ReferenceNo).HasColumnName("REFERENCE_NO").HasMaxLength(100);
        builder.Property(e => e.Amount).HasColumnName("AMOUNT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.LocalAmount).HasColumnName("LOCAL_AMOUNT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.OpenAmount).HasColumnName("OPEN_AMOUNT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.LocalOpenAmount).HasColumnName("LOCAL_OPEN_AMOUNT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.DaysOverdue).HasColumnName("DAYS_OVERDUE");
        builder.Property(e => e.AgingBucket).HasColumnName("AGING_BUCKET").HasMaxLength(20);
        builder.Property(e => e.Bucket0To30).HasColumnName("BUCKET_0_30").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.Bucket31To60).HasColumnName("BUCKET_31_60").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.Bucket61To90).HasColumnName("BUCKET_61_90").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.Bucket91To120).HasColumnName("BUCKET_91_120").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.BucketOver120).HasColumnName("BUCKET_OVER_120").HasColumnType("NUMBER(18,4)");
    }
}
