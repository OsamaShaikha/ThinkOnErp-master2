using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvItemCategoryConfiguration : IEntityTypeConfiguration<InvItemCategory>
{
    public void Configure(EntityTypeBuilder<InvItemCategory> builder)
    {
        builder.ToTable("INV_ITEM_CATEGORY");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(g => g.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(g => g.ParentCategoryId).HasColumnName("PARENT_CATEGORY_ID");
        builder.Property(g => g.CategoryCode).HasColumnName("CATEGORY_CODE").HasColumnType("NUMBER(10)").IsRequired();
        builder.Property(g => g.CategoryNameLocal).HasColumnName("CATEGORY_NAME_LOCAL").HasMaxLength(150).IsRequired();
        builder.Property(g => g.CategoryNameEn).HasColumnName("CATEGORY_NAME_EN").HasMaxLength(150);
        builder.Property(g => g.CategoryLevel).HasColumnName("CATEGORY_LEVEL").HasDefaultValue(1);
        builder.Property(g => g.GlControlAccount).HasColumnName("GL_CONTROL_ACCOUNT").HasMaxLength(50);
        builder.Property(g => g.GlCogsAccount).HasColumnName("GL_COGS_ACCOUNT").HasMaxLength(50);
        builder.Property(g => g.GlRevenueAccount).HasColumnName("GL_REVENUE_ACCOUNT").HasMaxLength(50);
        builder.Property(g => g.GlAdjustmentAccount).HasColumnName("GL_ADJUSTMENT_ACCOUNT").HasMaxLength(50);
        builder.Property(g => g.ImageBase64).HasColumnName("IMAGE_BASE64").HasColumnType("CLOB");
        builder.Property(g => g.ColorCode).HasColumnName("COLOR_CODE");
        builder.Property(g => g.ShowInPos).HasColumnName("IS_SHOW_IN_POS").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(g => g.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(g => g.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(g => g.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(g => g.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(g => g.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        // Ignore backward compatibility aliases
        builder.Ignore(g => g.ParentGroupId);
        builder.Ignore(g => g.GroupCode);
        builder.Ignore(g => g.GroupNameLocal);
        builder.Ignore(g => g.GroupNameEn);
        builder.Ignore(g => g.GroupLevel);
        builder.Ignore(g => g.ParentGroup);
        builder.Ignore(g => g.SubGroups);

        builder.HasIndex(g => g.CategoryCode).IsUnique();
        builder.HasOne(g => g.ParentCategory).WithMany(p => p.SubCategories).HasForeignKey(g => g.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
