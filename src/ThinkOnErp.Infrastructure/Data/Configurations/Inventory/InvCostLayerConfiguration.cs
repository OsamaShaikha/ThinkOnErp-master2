using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvCostLayerConfiguration : IEntityTypeConfiguration<InvCostLayer>
{
    public void Configure(EntityTypeBuilder<InvCostLayer> builder)
    {
        builder.ToTable("INV_COST_LAYER");

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

        builder.Property(e => e.ReceivedQty)
            .HasColumnName("RECEIVED_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.RemainingQty)
            .HasColumnName("REMAINING_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.UnitCost)
            .HasColumnName("UNIT_COST")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(e => e.ReceivedDate)
            .HasColumnName("RECEIVED_DATE")
            .IsRequired();

        builder.Property(e => e.LotId)
            .HasColumnName("LOT_ID");

        builder.Property(e => e.SourceLedgerId)
            .HasColumnName("SOURCE_LEDGER_ID");

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

    }
}