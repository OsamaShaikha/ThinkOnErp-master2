using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class TrxDocumentHeaderConfiguration : IEntityTypeConfiguration<TrxDocumentHeader>
{
    public void Configure(EntityTypeBuilder<TrxDocumentHeader> builder)
    {
        builder.ToTable("TRX_DOCUMENT_HEADER");
        // Composite PK: BRANCH_ID, DOC_YEAR, DOC_TYPE, ID
        builder.HasKey(h => new { h.BranchId, h.DocYear, h.DocType, h.Id });

        builder.Property(h => h.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(h => h.DocYear).HasColumnName("DOC_YEAR");
        builder.Property(h => h.DocType).HasColumnName("DOC_TYPE");
        builder.Property(h => h.Id).HasColumnName("ID");

        builder.Property(h => h.TrxType).HasColumnName("TRX_TYPE").IsRequired();
        builder.Property(h => h.DocNo).HasColumnName("DOC_NO").HasMaxLength(50).IsRequired();
        builder.Property(h => h.DocDate).HasColumnName("DOC_DATE").HasColumnType("TIMESTAMP").IsRequired();
        builder.Property(h => h.DueDate).HasColumnName("DUE_DATE");

        builder.Property(h => h.PartyTypeCode).HasColumnName("PARTY_TYPE_CODE").HasDefaultValue(0);
        builder.Property(h => h.PartyId).HasColumnName("PARTY_ID");
        builder.Property(h => h.PartyName).HasColumnName("PARTY_NAME").HasMaxLength(200);

        builder.Property(h => h.FromWarehouseId).HasColumnName("FROM_WAREHOUSE_ID");
        builder.Property(h => h.ToWarehouseId).HasColumnName("TO_WAREHOUSE_ID");

        builder.Property(h => h.CurrencyCode).HasColumnName("CURRENCY_CODE").HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(h => h.ExchangeRate).HasColumnName("EXCHANGE_RATE").HasColumnType("NUMBER(14,6)").HasDefaultValue(1m);
        builder.Property(h => h.PaymentMethodCode).HasColumnName("PAYMENT_METHOD_CODE").HasDefaultValue(1);

        builder.Property(h => h.TotalGross).HasColumnName("TOTAL_GROSS").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(h => h.DiscountAmount).HasColumnName("DISCOUNT_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(h => h.TotalNetBeforeTax).HasColumnName("TOTAL_NET_BEFORE_TAX").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(h => h.TaxAmount).HasColumnName("TAX_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(h => h.TotalNet).HasColumnName("TOTAL_NET").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(h => h.PaidAmount).HasColumnName("PAID_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(h => h.RemainingAmount).HasColumnName("REMAINING_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.Property(h => h.BaseBranchId).HasColumnName("BASE_BRANCH_ID");
        builder.Property(h => h.BaseDocYear).HasColumnName("BASE_DOC_YEAR");
        builder.Property(h => h.BaseDocType).HasColumnName("BASE_DOC_TYPE");
        builder.Property(h => h.BaseDocId).HasColumnName("BASE_DOC_ID");

        builder.Property(h => h.DocStatusCode).HasColumnName("DOC_STATUS_CODE").HasDefaultValue(1);
        builder.Property(h => h.IsPostedGl).HasColumnName("IS_POSTED_GL").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(h => h.IsPostedStock).HasColumnName("IS_POSTED_STOCK").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(h => h.JournalEntryId).HasColumnName("JOURNAL_ENTRY_ID");
        builder.Property(h => h.Notes).HasColumnName("NOTES").HasMaxLength(1000);

        builder.Property(h => h.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(h => h.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(h => h.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(h => h.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");
        builder.Property(h => h.PostedBy).HasColumnName("POSTED_BY").HasMaxLength(100);
        builder.Property(h => h.PostedDate).HasColumnName("POSTED_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(h => new { h.BranchId, h.DocYear, h.DocType, h.DocNo }).IsUnique();
        builder.HasOne(h => h.DocTypeConfig).WithMany().HasForeignKey(h => h.DocType).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.TrxTypeConfig).WithMany().HasForeignKey(h => h.TrxType).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.FromWarehouse).WithMany().HasForeignKey(h => h.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.ToWarehouse).WithMany().HasForeignKey(h => h.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(h => h.Lines).WithOne(l => l.DocumentHeader)
            .HasForeignKey(l => new { l.BranchId, l.DocYear, l.DocType, l.DocId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
