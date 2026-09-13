using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosOrderHeaderConfiguration : IEntityTypeConfiguration<PosOrderHeader>
{
    public void Configure(EntityTypeBuilder<PosOrderHeader> builder)
    {
        builder.ToTable("POS_ORDER_HEADER");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(o => o.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(o => o.ShiftId).HasColumnName("SHIFT_ID").IsRequired();
        builder.Property(o => o.TillId).HasColumnName("TILL_ID").IsRequired();
        builder.Property(o => o.OrderNumber).HasColumnName("ORDER_NUMBER").HasMaxLength(50).IsRequired();
        builder.Property(o => o.InvoiceNumber).HasColumnName("INVOICE_NUMBER").HasMaxLength(50);
        builder.Property(o => o.ClientUuid).HasColumnName("CLIENT_UUID").HasMaxLength(100).IsRequired();

        builder.Property(o => o.OrderType).HasColumnName("ORDER_TYPE").HasConversion<int>().IsRequired();
        builder.Property(o => o.Status).HasColumnName("STATUS").HasConversion<int>().IsRequired();

        builder.Property(o => o.CustomerId).HasColumnName("CUSTOMER_ID");
        builder.Property(o => o.CustomerName).HasColumnName("CUSTOMER_NAME").HasMaxLength(200);
        builder.Property(o => o.TableId).HasColumnName("TABLE_ID");
        builder.Property(o => o.Covers).HasColumnName("COVERS").HasDefaultValue(1);
        builder.Property(o => o.PriceListId).HasColumnName("PRICE_LIST_ID");

        builder.Property(o => o.SubtotalAmount).HasColumnName("SUBTOTAL_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.DiscountAmount).HasColumnName("DISCOUNT_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.DiscountPercent).HasColumnName("DISCOUNT_PERCENT").HasColumnType("NUMBER(8,4)").HasDefaultValue(0m);
        builder.Property(o => o.DiscountReason).HasColumnName("DISCOUNT_REASON").HasMaxLength(300);
        builder.Property(o => o.TaxAmount).HasColumnName("TAX_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.ServiceChargeAmount).HasColumnName("SERVICE_CHARGE_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.DeliveryFee).HasColumnName("DELIVERY_FEE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.TipAmount).HasColumnName("TIP_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.TotalAmount).HasColumnName("TOTAL_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.PaidAmount).HasColumnName("PAID_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.ChangeAmount).HasColumnName("CHANGE_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.IsPaid).HasColumnName("IS_PAID").HasColumnType("NUMBER(1)").HasDefaultValue(false);

        builder.Property(o => o.IsRefund).HasColumnName("IS_REFUND").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(o => o.OriginalOrderId).HasColumnName("ORIGINAL_ORDER_ID");
        builder.Property(o => o.RefundReason).HasColumnName("REFUND_REASON").HasMaxLength(500);

        builder.Property(o => o.ZReportId).HasColumnName("Z_REPORT_ID");
        builder.Property(o => o.GlVoucherId).HasColumnName("GL_VOUCHER_ID");

        builder.Property(o => o.EInvoiceQrCode).HasColumnName("E_INVOICE_QR_CODE").HasColumnType("CLOB");
        builder.Property(o => o.EInvoiceHash).HasColumnName("E_INVOICE_HASH").HasMaxLength(128);
        builder.Property(o => o.InvoiceCounterValue).HasColumnName("INVOICE_COUNTER_VALUE");

        builder.Property(o => o.Notes).HasColumnName("NOTES").HasMaxLength(1000);
        builder.Property(o => o.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.Property(o => o.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(o => o.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(o => o.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(o => o.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(o => new { o.BranchId, o.OrderNumber }).IsUnique();
        builder.HasIndex(o => new { o.BranchId, o.ClientUuid }).IsUnique();

        builder.HasOne(o => o.Branch).WithMany().HasForeignKey(o => o.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Shift).WithMany(s => s.Orders).HasForeignKey(o => o.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Till).WithMany().HasForeignKey(o => o.TillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Table).WithMany().HasForeignKey(o => o.TableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.GlVoucher).WithMany().HasForeignKey(o => o.GlVoucherId).OnDelete(DeleteBehavior.Restrict);
    }
}
