using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosBatchPrepConfiguration : IEntityTypeConfiguration<PosBatchPrep>
{
    public void Configure(EntityTypeBuilder<PosBatchPrep> builder)
    {
        builder.ToTable("POS_BATCH_PREP");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(b => b.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(b => b.BatchNumber).HasColumnName("BATCH_NUMBER").HasMaxLength(50).IsRequired();
        builder.Property(b => b.PrepDate).HasColumnName("PREP_DATE").HasColumnType("TIMESTAMP").IsRequired();

        builder.Property(b => b.OutputItemId).HasColumnName("OUTPUT_ITEM_ID").IsRequired();
        builder.Property(b => b.PlannedOutputQty).HasColumnName("PLANNED_OUTPUT_QTY").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(b => b.ActualOutputQty).HasColumnName("ACTUAL_OUTPUT_QTY").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(b => b.OutputUomId).HasColumnName("OUTPUT_UOM_ID").IsRequired();

        builder.Property(b => b.YieldFactor).HasColumnName("YIELD_FACTOR").HasColumnType("NUMBER(8,4)").HasDefaultValue(1.0m);
        builder.Property(b => b.TotalRawCost).HasColumnName("TOTAL_RAW_COST").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(b => b.UnitCostProduced).HasColumnName("UNIT_COST_PRODUCED").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.Property(b => b.PrepNotes).HasColumnName("PREP_NOTES").HasMaxLength(500);
        builder.Property(b => b.IsPostedToInventory).HasColumnName("IS_POSTED_TO_INVENTORY").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(b => b.PostedDate).HasColumnName("POSTED_DATE").HasColumnType("TIMESTAMP");

        builder.Property(b => b.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(b => b.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(b => new { b.BranchId, b.BatchNumber }).IsUnique();
        builder.HasOne(b => b.Branch).WithMany().HasForeignKey(b => b.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.OutputItem).WithMany().HasForeignKey(b => b.OutputItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PosBatchPrepLineConfiguration : IEntityTypeConfiguration<PosBatchPrepLine>
{
    public void Configure(EntityTypeBuilder<PosBatchPrepLine> builder)
    {
        builder.ToTable("POS_BATCH_PREP_LINE");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.BatchPrepId).HasColumnName("BATCH_PREP_ID").IsRequired();
        builder.Property(l => l.RawItemId).HasColumnName("RAW_ITEM_ID").IsRequired();
        builder.Property(l => l.QuantityUsed).HasColumnName("QUANTITY_USED").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(l => l.UomId).HasColumnName("UOM_ID").IsRequired();
        builder.Property(l => l.UnitCost).HasColumnName("UNIT_COST").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(l => l.LineCost).HasColumnName("LINE_COST").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(l => l.ScrapWasteQty).HasColumnName("SCRAP_WASTE_QTY").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);

        builder.HasOne(l => l.BatchPrep).WithMany(b => b.ConsumedLines).HasForeignKey(l => l.BatchPrepId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.RawItem).WithMany().HasForeignKey(l => l.RawItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
