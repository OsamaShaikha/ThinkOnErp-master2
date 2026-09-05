using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Views;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Views;

public sealed class InventoryValuationViewConfiguration : IEntityTypeConfiguration<InventoryValuationView>
{
    public void Configure(EntityTypeBuilder<InventoryValuationView> builder)
    {
        builder.ToView("VW_INVENTORY_VALUATION");
        builder.HasNoKey();

        builder.Property(e => e.BalanceId).HasColumnName("BALANCE_ID");
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(e => e.WarehouseId).HasColumnName("WAREHOUSE_ID");
        builder.Property(e => e.WarehouseCode).HasColumnName("WAREHOUSE_CODE");
        builder.Property(e => e.WarehouseNameLocal).HasColumnName("WAREHOUSE_NAME_LOCAL").HasMaxLength(200);
        builder.Property(e => e.ItemId).HasColumnName("ITEM_ID");
        builder.Property(e => e.ItemCode).HasColumnName("ITEM_CODE").HasMaxLength(50);
        builder.Property(e => e.ItemNameLocal).HasColumnName("ITEM_NAME_LOCAL").HasMaxLength(200);
        builder.Property(e => e.ItemNameEn).HasColumnName("ITEM_NAME_EN").HasMaxLength(200);
        builder.Property(e => e.ItemType).HasColumnName("ITEM_TYPE").HasMaxLength(50);
        builder.Property(e => e.CostingMethod).HasColumnName("COSTING_METHOD").HasMaxLength(50);
        builder.Property(e => e.UomBase).HasColumnName("UOM_BASE");
        builder.Property(e => e.OnHandQty).HasColumnName("ON_HAND_QTY").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.ReservedQty).HasColumnName("RESERVED_QTY").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.AvailableQty).HasColumnName("AVAILABLE_QTY").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.OnOrderQty).HasColumnName("ON_ORDER_QTY").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.AvgCost).HasColumnName("AVG_COST").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.TotalValuation).HasColumnName("TOTAL_VALUATION").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.GlControlAccount).HasColumnName("GL_CONTROL_ACCOUNT").HasMaxLength(50);
        builder.Property(e => e.GlCogsAccount).HasColumnName("GL_COGS_ACCOUNT").HasMaxLength(50);
        builder.Property(e => e.LastReceiptDate).HasColumnName("LAST_RECEIPT_DATE");
        builder.Property(e => e.LastIssueDate).HasColumnName("LAST_ISSUE_DATE");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)");
    }
}
