using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvOpeningLineConfiguration : IEntityTypeConfiguration<InvOpeningLine>
{
    public void Configure(EntityTypeBuilder<InvOpeningLine> builder)
    {
        builder.ToTable("INV_OPENING_LINE");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.BatchId).HasColumnName("BATCH_ID").IsRequired();
        builder.Property(l => l.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(l => l.WarehouseId).HasColumnName("WAREHOUSE_ID").IsRequired();
        builder.Property(l => l.ItemId).HasColumnName("ITEM_ID").IsRequired();
        builder.Property(l => l.BinId).HasColumnName("BIN_ID");

        builder.Property(l => l.UomCode).HasColumnName("UOM_CODE").HasColumnType("NUMBER(6)").IsRequired();
        builder.Property(l => l.UomFactor).HasColumnName("UOM_FACTOR").HasColumnType("NUMBER(18,6)").HasDefaultValue(1m);
        builder.Property(l => l.Quantity).HasColumnName("QUANTITY").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(l => l.BaseQuantity).HasColumnName("BASE_QUANTITY").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(l => l.UnitCost).HasColumnName("UNIT_COST").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(l => l.TotalCost).HasColumnName("TOTAL_COST").HasColumnType("NUMBER(18,4)").IsRequired();

        builder.Property(l => l.LotNumber).HasColumnName("LOT_NUMBER").HasMaxLength(50);
        builder.Property(l => l.SerialNumber).HasColumnName("SERIAL_NUMBER").HasMaxLength(100);
        builder.Property(l => l.ExpiryDate).HasColumnName("EXPIRY_DATE");
        builder.Property(l => l.Notes).HasColumnName("NOTES").HasMaxLength(500);

        builder.HasOne(l => l.Warehouse).WithMany().HasForeignKey(l => l.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.Item).WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
