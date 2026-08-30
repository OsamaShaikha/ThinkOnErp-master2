using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvStockLedgerEntryConfiguration : IEntityTypeConfiguration<InvStockLedgerEntry>
{
    public void Configure(EntityTypeBuilder<InvStockLedgerEntry> builder)
    {
        builder.ToTable("INV_STOCK_LEDGER_ENTRY");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(e => e.ItemId)
            .HasColumnName("ITEM_ID")
            .IsRequired();

        builder.Property(e => e.WarehouseId)
            .HasColumnName("WAREHOUSE_ID")
            .IsRequired();

        builder.Property(e => e.BinId)
            .HasColumnName("BIN_ID");

        builder.Property(e => e.TransactionDate)
            .HasColumnName("TRANSACTION_DATE")
            .IsRequired();

        builder.Property(e => e.TransactionType)
            .HasColumnName("TRANSACTION_TYPE")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Direction)
            .HasColumnName("DIRECTION")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Quantity)
            .HasColumnName("QUANTITY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.UomCode)
            .HasColumnName("UOM_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.UnitCost)
            .HasColumnName("UNIT_COST")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(e => e.RunningBalanceQty)
            .HasColumnName("RUNNING_BALANCE_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.RunningBalanceValue)
            .HasColumnName("RUNNING_BALANCE_VALUE")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(e => e.LotId)
            .HasColumnName("LOT_ID");

        builder.Property(e => e.SerialId)
            .HasColumnName("SERIAL_ID");

        builder.Property(e => e.LpnCode)
            .HasColumnName("LPN_CODE")
            .HasMaxLength(50);

        builder.Property(e => e.CostLayerId)
            .HasColumnName("COST_LAYER_ID");

        builder.Property(e => e.SourceModule)
            .HasColumnName("SOURCE_MODULE")
            .HasMaxLength(100);

        builder.Property(e => e.SourceDocType)
            .HasColumnName("SOURCE_DOC_TYPE")
            .HasMaxLength(100);

        builder.Property(e => e.SourceDocId)
            .HasColumnName("SOURCE_DOC_ID")
            .HasMaxLength(100);

        builder.Property(e => e.SourceLineId)
            .HasColumnName("SOURCE_LINE_ID")
            .HasMaxLength(100);

        builder.Property(e => e.JournalEntryId)
            .HasColumnName("JOURNAL_ENTRY_ID");

        builder.Property(e => e.PostingRuleCode)
            .HasColumnName("POSTING_RULE_CODE")
            .HasMaxLength(50);

        builder.Property(e => e.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(2000);

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

    }
}