using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class TrxDocumentLineConfiguration : IEntityTypeConfiguration<TrxDocumentLine>
{
    public void Configure(EntityTypeBuilder<TrxDocumentLine> builder)
    {
        builder.ToTable("TRX_DOCUMENT_LINE");
        // Composite PK: BRANCH_ID, DOC_YEAR, DOC_TYPE, DOC_ID, LINE_NO
        builder.HasKey(l => new { l.BranchId, l.DocYear, l.DocType, l.DocId, l.LineNo });

        builder.Property(l => l.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(l => l.DocYear).HasColumnName("DOC_YEAR");
        builder.Property(l => l.DocType).HasColumnName("DOC_TYPE");
        builder.Property(l => l.DocId).HasColumnName("DOC_ID");
        builder.Property(l => l.LineNo).HasColumnName("LINE_NO");

        builder.Property(l => l.TrxType).HasColumnName("TRX_TYPE").IsRequired();
        builder.Property(l => l.ItemId).HasColumnName("ITEM_ID").IsRequired();
        builder.Property(l => l.ItemDescription).HasColumnName("ITEM_DESCRIPTION").HasMaxLength(500);

        builder.Property(l => l.UomCode).HasColumnName("UOM_CODE").HasColumnType("NUMBER(6)").IsRequired();
        builder.Property(l => l.UomFactor).HasColumnName("UOM_FACTOR").HasColumnType("NUMBER(18,6)").HasDefaultValue(1m);

        builder.Property(l => l.QuantityIn).HasColumnName("QUANTITY_IN").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);
        builder.Property(l => l.QuantityOut).HasColumnName("QUANTITY_OUT").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);
        builder.Property(l => l.BaseQuantityIn).HasColumnName("BASE_QUANTITY_IN").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);
        builder.Property(l => l.BaseQuantityOut).HasColumnName("BASE_QUANTITY_OUT").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);

        builder.Property(l => l.UnitPrice).HasColumnName("UNIT_PRICE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(l => l.UnitCost).HasColumnName("UNIT_COST").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(l => l.DiscountPercent).HasColumnName("DISCOUNT_PERCENT").HasColumnType("NUMBER(7,4)").HasDefaultValue(0m);
        builder.Property(l => l.DiscountAmount).HasColumnName("DISCOUNT_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(l => l.TaxRate).HasColumnName("TAX_RATE").HasColumnType("NUMBER(7,4)").HasDefaultValue(0m);
        builder.Property(l => l.TaxAmount).HasColumnName("TAX_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(l => l.LineTotal).HasColumnName("LINE_TOTAL").HasColumnType("NUMBER(18,4)").IsRequired();

        builder.Property(l => l.FromBinId).HasColumnName("FROM_BIN_ID");
        builder.Property(l => l.ToBinId).HasColumnName("TO_BIN_ID");
        builder.Property(l => l.LotNumber).HasColumnName("LOT_NUMBER").HasMaxLength(50);
        builder.Property(l => l.SerialNumber).HasColumnName("SERIAL_NUMBER").HasMaxLength(100);
        builder.Property(l => l.ExpiryDate).HasColumnName("EXPIRY_DATE");
        builder.Property(l => l.GlAccountCode).HasColumnName("GL_ACCOUNT_CODE").HasMaxLength(50);
        builder.Property(l => l.BaseLineNo).HasColumnName("BASE_LINE_NO");

        builder.HasOne(l => l.Item).WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.TrxTypeConfig).WithMany().HasForeignKey(l => l.TrxType).OnDelete(DeleteBehavior.Restrict);
    }
}
