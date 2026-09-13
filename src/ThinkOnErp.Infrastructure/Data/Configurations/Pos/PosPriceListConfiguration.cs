using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosPriceListConfiguration : IEntityTypeConfiguration<PosPriceList>
{
    public void Configure(EntityTypeBuilder<PosPriceList> builder)
    {
        builder.ToTable("POS_PRICE_LIST");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(p => p.PriceListCode).HasColumnName("PRICE_LIST_CODE").HasMaxLength(50).IsRequired();
        builder.Property(p => p.PriceListNameLocal).HasColumnName("PRICE_LIST_NAME_LOCAL").HasMaxLength(150).IsRequired();
        builder.Property(p => p.PriceListNameEn).HasColumnName("PRICE_LIST_NAME_EN").HasMaxLength(150);
        builder.Property(p => p.ApplicableOrderType).HasColumnName("APPLICABLE_ORDER_TYPE").HasConversion<int>();
        builder.Property(p => p.CurrencyId).HasColumnName("CURRENCY_ID");
        builder.Property(p => p.IsDefault).HasColumnName("IS_DEFAULT").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(p => new { p.BranchId, p.PriceListCode }).IsUnique();
        builder.HasOne(p => p.Branch).WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PosPriceListItemConfiguration : IEntityTypeConfiguration<PosPriceListItem>
{
    public void Configure(EntityTypeBuilder<PosPriceListItem> builder)
    {
        builder.ToTable("POS_PRICE_LIST_ITEM");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(i => i.PriceListId).HasColumnName("PRICE_LIST_ID").IsRequired();
        builder.Property(i => i.ItemId).HasColumnName("ITEM_ID").IsRequired();
        builder.Property(i => i.Price).HasColumnName("PRICE").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(i => i.MinPrice).HasColumnName("MIN_PRICE").HasColumnType("NUMBER(18,4)");

        builder.HasIndex(i => new { i.PriceListId, i.ItemId }).IsUnique();
        builder.HasOne(i => i.PriceList).WithMany(p => p.Items).HasForeignKey(i => i.PriceListId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Item).WithMany().HasForeignKey(i => i.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
