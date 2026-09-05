using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Views;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Views;

public sealed class SalesInvoiceProfitabilityViewConfiguration : IEntityTypeConfiguration<SalesInvoiceProfitabilityView>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceProfitabilityView> builder)
    {
        builder.ToView("VW_SALES_INVOICE_PROFITABILITY");
        builder.HasNoKey();

        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(e => e.DocYear).HasColumnName("DOC_YEAR");
        builder.Property(e => e.DocType).HasColumnName("DOC_TYPE");
        builder.Property(e => e.DocId).HasColumnName("DOC_ID");
        builder.Property(e => e.DocNo).HasColumnName("DOC_NO").HasMaxLength(50);
        builder.Property(e => e.DocDate).HasColumnName("DOC_DATE");
        builder.Property(e => e.DocStatusCode).HasColumnName("DOC_STATUS_CODE");
        builder.Property(e => e.IsPostedGl).HasColumnName("IS_POSTED_GL").HasColumnType("NUMBER(1)");
        builder.Property(e => e.IsPostedStock).HasColumnName("IS_POSTED_STOCK").HasColumnType("NUMBER(1)");
        builder.Property(e => e.CustomerId).HasColumnName("CUSTOMER_ID");
        builder.Property(e => e.CustomerCode).HasColumnName("CUSTOMER_CODE").HasMaxLength(50);
        builder.Property(e => e.CustomerName).HasColumnName("CUSTOMER_NAME").HasMaxLength(200);
        builder.Property(e => e.FromWarehouseId).HasColumnName("FROM_WAREHOUSE_ID");
        builder.Property(e => e.HeaderTotalGross).HasColumnName("HEADER_TOTAL_GROSS").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.HeaderTotalNet).HasColumnName("HEADER_TOTAL_NET").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.HeaderTotalCost).HasColumnName("HEADER_TOTAL_COST").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.HeaderTotalProfit).HasColumnName("HEADER_TOTAL_PROFIT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.HeaderProfitMargin).HasColumnName("HEADER_PROFIT_MARGIN").HasColumnType("NUMBER(9,4)");
        builder.Property(e => e.LineNo).HasColumnName("LINE_NO");
        builder.Property(e => e.ItemId).HasColumnName("ITEM_ID");
        builder.Property(e => e.ItemCode).HasColumnName("ITEM_CODE").HasMaxLength(50);
        builder.Property(e => e.ItemName).HasColumnName("ITEM_NAME").HasMaxLength(200);
        builder.Property(e => e.Quantity).HasColumnName("QUANTITY").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.BaseQuantity).HasColumnName("BASE_QUANTITY").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.UnitPrice).HasColumnName("UNIT_PRICE").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.UnitCost).HasColumnName("UNIT_COST").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.LineTotal).HasColumnName("LINE_TOTAL").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.LineCost).HasColumnName("LINE_COST").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.LineProfit).HasColumnName("LINE_PROFIT").HasColumnType("NUMBER(18,4)");
        builder.Property(e => e.LineProfitMargin).HasColumnName("LINE_PROFIT_MARGIN").HasColumnType("NUMBER(9,4)");
    }
}
