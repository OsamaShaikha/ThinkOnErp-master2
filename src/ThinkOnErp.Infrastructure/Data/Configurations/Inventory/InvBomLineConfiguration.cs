using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvBomLineConfiguration : IEntityTypeConfiguration<InvBomLine>
{
    public void Configure(EntityTypeBuilder<InvBomLine> builder)
    {
        builder.ToTable("INV_BOM_LINE");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.BomId).HasColumnName("BOM_ID").IsRequired();
        builder.Property(l => l.LineNo).HasColumnName("LINE_NO").IsRequired();
        builder.Property(l => l.ComponentItemId).HasColumnName("COMPONENT_ITEM_ID").IsRequired();
        builder.Property(l => l.UomCode).HasColumnName("UOM_CODE").HasMaxLength(20).IsRequired();
        builder.Property(l => l.UomFactor).HasColumnName("UOM_FACTOR").HasColumnType("NUMBER(18,6)").HasDefaultValue(1m);
        builder.Property(l => l.Quantity).HasColumnName("QUANTITY").HasColumnType("NUMBER(14,4)").IsRequired();
        builder.Property(l => l.ScrapPercent).HasColumnName("SCRAP_PERCENT").HasColumnType("NUMBER(7,4)").HasDefaultValue(0m);
        builder.Property(l => l.CostSharePercent).HasColumnName("COST_SHARE_PERCENT").HasColumnType("NUMBER(7,4)").HasDefaultValue(0m);
        builder.Property(l => l.AllowSubstitute).HasColumnName("ALLOW_SUBSTITUTE").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(l => l.SubstituteItemId).HasColumnName("SUBSTITUTE_ITEM_ID");
        builder.Property(l => l.Notes).HasColumnName("NOTES").HasMaxLength(500);

        builder.HasOne(l => l.ComponentItem).WithMany().HasForeignKey(l => l.ComponentItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.SubstituteItem).WithMany().HasForeignKey(l => l.SubstituteItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
