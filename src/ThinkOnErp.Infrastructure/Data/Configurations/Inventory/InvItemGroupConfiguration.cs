using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvItemGroupConfiguration : IEntityTypeConfiguration<InvItemGroup>
{
    public void Configure(EntityTypeBuilder<InvItemGroup> builder)
    {
        builder.ToTable("INV_ITEM_GROUP");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(g => g.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(g => g.ParentGroupId).HasColumnName("PARENT_GROUP_ID");
        builder.Property(g => g.GroupCode).HasColumnName("GROUP_CODE").HasMaxLength(30).IsRequired();
        builder.Property(g => g.GroupNameAr).HasColumnName("GROUP_NAME_AR").HasMaxLength(150).IsRequired();
        builder.Property(g => g.GroupNameEn).HasColumnName("GROUP_NAME_EN").HasMaxLength(150);
        builder.Property(g => g.GroupLevel).HasColumnName("GROUP_LEVEL").HasDefaultValue(1);
        builder.Property(g => g.GlControlAccount).HasColumnName("GL_CONTROL_ACCOUNT").HasMaxLength(50);
        builder.Property(g => g.GlCogsAccount).HasColumnName("GL_COGS_ACCOUNT").HasMaxLength(50);
        builder.Property(g => g.GlRevenueAccount).HasColumnName("GL_REVENUE_ACCOUNT").HasMaxLength(50);
        builder.Property(g => g.GlAdjustmentAccount).HasColumnName("GL_ADJUSTMENT_ACCOUNT").HasMaxLength(50);
        builder.Property(g => g.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(g => g.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(g => g.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(g => g.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(g => g.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(g => g.GroupCode).IsUnique();
        builder.HasOne(g => g.ParentGroup).WithMany(p => p.SubGroups).HasForeignKey(g => g.ParentGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
