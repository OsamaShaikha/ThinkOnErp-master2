using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosExternalOrderConfiguration : IEntityTypeConfiguration<PosExternalOrder>
{
    public void Configure(EntityTypeBuilder<PosExternalOrder> builder)
    {
        builder.ToTable("POS_EXTERNAL_ORDER");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.Provider).HasColumnName("PROVIDER").HasConversion<int>().IsRequired();
        builder.Property(e => e.ExternalOrderId).HasColumnName("EXTERNAL_ORDER_ID").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ExternalOrderDisplayNumber).HasColumnName("EXTERNAL_ORDER_DISPLAY_NO").HasMaxLength(50).IsRequired();
        builder.Property(e => e.RawPayloadJson).HasColumnName("RAW_PAYLOAD_JSON").HasColumnType("CLOB");

        builder.Property(e => e.CustomerName).HasColumnName("CUSTOMER_NAME").HasMaxLength(150).IsRequired();
        builder.Property(e => e.CustomerPhone).HasColumnName("CUSTOMER_PHONE").HasMaxLength(50);
        builder.Property(e => e.DeliveryAddress).HasColumnName("DELIVERY_ADDRESS").HasMaxLength(500);

        builder.Property(e => e.SubtotalAmount).HasColumnName("SUBTOTAL_AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(e => e.CommissionRate).HasColumnName("COMMISSION_RATE").HasColumnType("NUMBER(8,4)").HasDefaultValue(0m);
        builder.Property(e => e.CommissionAmount).HasColumnName("COMMISSION_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(e => e.NetPayableAmount).HasColumnName("NET_PAYABLE_AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();

        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(50).HasDefaultValue("Received");
        builder.Property(e => e.CreatedPosOrderId).HasColumnName("CREATED_POS_ORDER_ID");
        builder.Property(e => e.ReceivedAt).HasColumnName("RECEIVED_AT").HasColumnType("TIMESTAMP");

        builder.HasIndex(e => new { e.BranchId, e.Provider, e.ExternalOrderId }).IsUnique();
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PosOrder).WithMany().HasForeignKey(e => e.CreatedPosOrderId).OnDelete(DeleteBehavior.SetNull);
    }
}
