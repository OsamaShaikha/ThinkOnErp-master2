using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvItemConfiguration : IEntityTypeConfiguration<InvItem>
{
    public void Configure(EntityTypeBuilder<InvItem> builder)
    {
        builder.ToTable("INV_ITEM");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(i => i.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(i => i.ItemCode).HasColumnName("ITEM_CODE").HasMaxLength(30).IsRequired();
        builder.Property(i => i.ItemNameAr).HasColumnName("ITEM_NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(i => i.ItemNameEn).HasColumnName("ITEM_NAME_EN").HasMaxLength(200);
        builder.Property(i => i.MainGroupId).HasColumnName("MAIN_GROUP_ID").IsRequired();
        builder.Property(i => i.SubGroupId).HasColumnName("SUB_GROUP_ID");
        builder.Property(i => i.ItemType).HasColumnName("ITEM_TYPE").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.UomBase).HasColumnName("UOM_BASE").HasMaxLength(20).IsRequired();
        builder.Property(i => i.CostingMethod).HasColumnName("COSTING_METHOD").HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Entities.Inventory.Enums.CostingMethod.WeightedAverage);
        builder.Property(i => i.StandardCost).HasColumnName("STANDARD_COST").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.Property(i => i.SerialTracking).HasColumnName("SERIAL_TRACKING").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(i => i.LotTracking).HasColumnName("LOT_TRACKING").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(i => i.ExpiryTracking).HasColumnName("EXPIRY_TRACKING").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(i => i.ShelfLifeDays).HasColumnName("SHELF_LIFE_DAYS");

        builder.Property(i => i.AllowNegativeStock).HasColumnName("ALLOW_NEGATIVE_STOCK").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(i => i.ReorderPoint).HasColumnName("REORDER_POINT").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);
        builder.Property(i => i.SafetyStock).HasColumnName("SAFETY_STOCK").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);
        builder.Property(i => i.MinOrderQty).HasColumnName("MIN_ORDER_QTY").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);
        builder.Property(i => i.LeadTimeDays).HasColumnName("LEAD_TIME_DAYS").HasDefaultValue(0);

        builder.Property(i => i.Weight).HasColumnName("WEIGHT").HasColumnType("NUMBER(14,4)").HasDefaultValue(0m);
        builder.Property(i => i.WeightUnit).HasColumnName("WEIGHT_UNIT").HasMaxLength(10);

        builder.Property(i => i.GlControlAccount).HasColumnName("GL_CONTROL_ACCOUNT").HasMaxLength(50);
        builder.Property(i => i.GlRevenueAccount).HasColumnName("GL_REVENUE_ACCOUNT").HasMaxLength(50);
        builder.Property(i => i.GlCogsAccount).HasColumnName("GL_COGS_ACCOUNT").HasMaxLength(50);

        builder.Property(i => i.CountryOfOrigin).HasColumnName("COUNTRY_OF_ORIGIN").HasMaxLength(3);
        builder.Property(i => i.HsCode).HasColumnName("HS_CODE").HasMaxLength(20);
        builder.Property(i => i.Notes).HasColumnName("NOTES").HasMaxLength(2000);

        builder.Property(i => i.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(i => i.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(i => i.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(i => i.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(i => i.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(i => i.ItemCode).IsUnique();
        builder.HasIndex(i => new { i.MainGroupId, i.SubGroupId });

        builder.HasOne(i => i.MainGroup).WithMany(g => g.Items).HasForeignKey(i => i.MainGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.SubGroup).WithMany().HasForeignKey(i => i.SubGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.UomConversions).WithOne(u => u.Item).HasForeignKey(u => u.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.Barcodes).WithOne(b => b.Item).HasForeignKey(b => b.ItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
