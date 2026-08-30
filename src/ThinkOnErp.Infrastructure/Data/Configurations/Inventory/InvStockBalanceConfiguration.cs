using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvStockBalanceConfiguration : IEntityTypeConfiguration<InvStockBalance>
{
    public void Configure(EntityTypeBuilder<InvStockBalance> builder)
    {
        builder.ToTable("INV_STOCK_BALANCE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ItemId)
            .HasColumnName("ITEM_ID")
            .IsRequired();

        builder.Property(e => e.WarehouseId)
            .HasColumnName("WAREHOUSE_ID")
            .IsRequired();

        builder.Property(e => e.BinId)
            .HasColumnName("BIN_ID");

        builder.Property(e => e.OnHandQty)
            .HasColumnName("ON_HAND_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.ReservedQty)
            .HasColumnName("RESERVED_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.OnOrderQty)
            .HasColumnName("ON_ORDER_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.AvgCost)
            .HasColumnName("AVG_COST")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(e => e.LastReceiptDate)
            .HasColumnName("LAST_RECEIPT_DATE");

        builder.Property(e => e.LastIssueDate)
            .HasColumnName("LAST_ISSUE_DATE");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("UPDATED_AT")
            .IsRequired();

    }
}