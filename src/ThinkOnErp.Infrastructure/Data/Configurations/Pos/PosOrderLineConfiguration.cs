using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosOrderLineConfiguration : IEntityTypeConfiguration<PosOrderLine>
{
    public void Configure(EntityTypeBuilder<PosOrderLine> builder)
    {
        builder.ToTable("POS_ORDER_LINE");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.OrderId).HasColumnName("ORDER_ID").IsRequired();
        builder.Property(l => l.LineNumber).HasColumnName("LINE_NUMBER").IsRequired();
        builder.Property(l => l.ItemId).HasColumnName("ITEM_ID").IsRequired();
        builder.Property(l => l.ItemCode).HasColumnName("ITEM_CODE").HasMaxLength(50).IsRequired();
        builder.Property(l => l.ItemName).HasColumnName("ITEM_NAME").HasMaxLength(200).IsRequired();

        builder.Property(l => l.Quantity).HasColumnName("QUANTITY").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(l => l.UomId).HasColumnName("UOM_ID").IsRequired();
        builder.Property(l => l.UnitPrice).HasColumnName("UNIT_PRICE").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(l => l.CostPrice).HasColumnName("COST_PRICE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.Property(l => l.DiscountAmount).HasColumnName("DISCOUNT_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(l => l.DiscountPercent).HasColumnName("DISCOUNT_PERCENT").HasColumnType("NUMBER(8,4)").HasDefaultValue(0m);
        builder.Property(l => l.DiscountReason).HasColumnName("DISCOUNT_REASON").HasMaxLength(300);

        builder.Property(l => l.TaxAmount).HasColumnName("TAX_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(l => l.TaxPercent).HasColumnName("TAX_PERCENT").HasColumnType("NUMBER(8,4)").HasDefaultValue(0m);
        builder.Property(l => l.LineTotal).HasColumnName("LINE_TOTAL").HasColumnType("NUMBER(18,4)").IsRequired();

        builder.Property(l => l.KdsStatus).HasColumnName("KDS_STATUS").HasConversion<int>().IsRequired();
        builder.Property(l => l.PrepStation).HasColumnName("PREP_STATION").HasMaxLength(50);
        builder.Property(l => l.KdsSentAt).HasColumnName("KDS_SENT_AT").HasColumnType("TIMESTAMP");
        builder.Property(l => l.KdsReadyAt).HasColumnName("KDS_READY_AT").HasColumnType("TIMESTAMP");

        builder.Property(l => l.IsScaleItem).HasColumnName("IS_SCALE_ITEM").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(l => l.ScaleWeight).HasColumnName("SCALE_WEIGHT").HasColumnType("NUMBER(14,4)");
        builder.Property(l => l.ScaleBarcode).HasColumnName("SCALE_BARCODE").HasMaxLength(50);

        builder.Property(l => l.IsVoided).HasColumnName("IS_VOIDED").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(l => l.VoidReason).HasColumnName("VOID_REASON").HasMaxLength(300);
        builder.Property(l => l.VoidApprovedBy).HasColumnName("VOID_APPROVED_BY").HasMaxLength(100);

        builder.Property(l => l.SalesEmployeeId).HasColumnName("SALES_EMPLOYEE_ID");
        builder.Property(l => l.CommissionAmount).HasColumnName("COMMISSION_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(l => l.SpecialInstructions).HasColumnName("SPECIAL_INSTRUCTIONS").HasMaxLength(500);

        builder.HasOne(l => l.Order).WithMany(o => o.Lines).HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.Item).WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
