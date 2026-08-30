using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvOpeningBatchConfiguration : IEntityTypeConfiguration<InvOpeningBatch>
{
    public void Configure(EntityTypeBuilder<InvOpeningBatch> builder)
    {
        builder.ToTable("INV_OPENING_BATCH");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(b => b.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(b => b.BatchNo).HasColumnName("BATCH_NO").HasMaxLength(30).IsRequired();
        builder.Property(b => b.BatchDate).HasColumnName("BATCH_DATE").HasColumnType("TIMESTAMP").IsRequired();
        builder.Property(b => b.FiscalYearId).HasColumnName("FISCAL_YEAR_ID").IsRequired();
        builder.Property(b => b.Description).HasColumnName("DESCRIPTION").HasMaxLength(300);
        builder.Property(b => b.TotalQuantity).HasColumnName("TOTAL_QUANTITY").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);
        builder.Property(b => b.TotalValuationAmount).HasColumnName("TOTAL_VALUATION_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(b => b.StatusCode).HasColumnName("STATUS_CODE").HasDefaultValue(1);
        builder.Property(b => b.JournalEntryId).HasColumnName("JOURNAL_ENTRY_ID");
        builder.Property(b => b.PostedAt).HasColumnName("POSTED_AT").HasColumnType("TIMESTAMP");
        builder.Property(b => b.PostedBy).HasColumnName("POSTED_BY").HasMaxLength(100);

        builder.Property(b => b.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(b => b.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(b => b.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(b => b.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(b => b.BatchNo).IsUnique();
        builder.HasMany(b => b.Lines).WithOne(l => l.Batch).HasForeignKey(l => l.BatchId).OnDelete(DeleteBehavior.Cascade);
    }
}
