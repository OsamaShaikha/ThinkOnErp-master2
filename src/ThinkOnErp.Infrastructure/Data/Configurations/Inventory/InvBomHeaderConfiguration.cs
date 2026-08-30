using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvBomHeaderConfiguration : IEntityTypeConfiguration<InvBomHeader>
{
    public void Configure(EntityTypeBuilder<InvBomHeader> builder)
    {
        builder.ToTable("INV_BOM_HEADER");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(b => b.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(b => b.BomCode).HasColumnName("BOM_CODE").HasMaxLength(30).IsRequired();
        builder.Property(b => b.BomNameAr).HasColumnName("BOM_NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(b => b.BomNameEn).HasColumnName("BOM_NAME_EN").HasMaxLength(200);
        builder.Property(b => b.ParentItemId).HasColumnName("PARENT_ITEM_ID").IsRequired();
        builder.Property(b => b.OutputQty).HasColumnName("OUTPUT_QTY").HasColumnType("NUMBER(14,4)").HasDefaultValue(1m);
        builder.Property(b => b.UomCode).HasColumnName("UOM_CODE").HasMaxLength(20).IsRequired();
        builder.Property(b => b.BomType).HasColumnName("BOM_TYPE_CODE").HasConversion<int>().HasDefaultValue(Domain.Entities.Inventory.Enums.BomType.SalesKit);
        builder.Property(b => b.LaborCost).HasColumnName("LABOR_COST").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(b => b.OverheadCost).HasColumnName("OVERHEAD_COST").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(b => b.IsDefault).HasColumnName("IS_DEFAULT").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(b => b.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(b => b.Notes).HasColumnName("NOTES").HasMaxLength(1000);
        builder.Property(b => b.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(b => b.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(b => b.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(b => b.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(b => b.BomCode).IsUnique();
        builder.HasOne(b => b.ParentItem).WithMany(i => i.AssembledBoms).HasForeignKey(b => b.ParentItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(b => b.Lines).WithOne(l => l.BomHeader).HasForeignKey(l => l.BomId).OnDelete(DeleteBehavior.Cascade);
    }
}
