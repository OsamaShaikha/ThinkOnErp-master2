using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosInventoryConflictConfiguration : IEntityTypeConfiguration<PosInventoryConflict>
{
    public void Configure(EntityTypeBuilder<PosInventoryConflict> builder)
    {
        builder.ToTable("POS_INVENTORY_CONFLICT");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(c => c.OrderId).HasColumnName("ORDER_ID").IsRequired();
        builder.Property(c => c.ItemId).HasColumnName("ITEM_ID").IsRequired();

        builder.Property(c => c.SoldQuantity).HasColumnName("SOLD_QUANTITY").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(c => c.AvailableStockAtSync).HasColumnName("AVAILABLE_STOCK_AT_SYNC").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(c => c.DeficitQuantity).HasColumnName("DEFICIT_QUANTITY").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(c => c.Status).HasColumnName("STATUS").HasConversion<int>().IsRequired();

        builder.Property(c => c.ResolutionNotes).HasColumnName("RESOLUTION_NOTES").HasMaxLength(500);
        builder.Property(c => c.ResolvedBy).HasColumnName("RESOLVED_BY").HasMaxLength(100);
        builder.Property(c => c.ResolvedAt).HasColumnName("RESOLVED_AT").HasColumnType("TIMESTAMP");

        builder.Property(c => c.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");

        builder.HasOne(c => c.Branch).WithMany().HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Order).WithMany().HasForeignKey(c => c.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Item).WithMany().HasForeignKey(c => c.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
