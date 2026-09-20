using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvModifierGroupConfiguration : IEntityTypeConfiguration<InvModifierGroup>
{
    public void Configure(EntityTypeBuilder<InvModifierGroup> builder)
    {
        builder.ToTable("INV_MODIFIER_GROUP");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(g => g.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(g => g.GroupCode).HasColumnName("GROUP_CODE").HasMaxLength(50).IsRequired();
        builder.Property(g => g.GroupNameLocal).HasColumnName("GROUP_NAME_LOCAL").HasMaxLength(150).IsRequired();
        builder.Property(g => g.GroupNameEn).HasColumnName("GROUP_NAME_EN").HasMaxLength(150);
        builder.Property(g => g.IsRequired).HasColumnName("IS_REQUIRED").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(g => g.SelectionType).HasColumnName("SELECTION_TYPE").HasConversion<int>().IsRequired();
        builder.Property(g => g.MinSelections).HasColumnName("MIN_SELECTIONS").HasColumnType("NUMBER(10)").HasDefaultValue(0);
        builder.Property(g => g.MaxSelections).HasColumnName("MAX_SELECTIONS").HasColumnType("NUMBER(10)");
        builder.Property(g => g.SortOrder).HasColumnName("SORT_ORDER").HasColumnType("NUMBER(10)").HasDefaultValue(0);
        builder.Property(g => g.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.Property(g => g.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(g => g.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(g => g.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(g => g.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(g => new { g.BranchId, g.GroupCode }).IsUnique();
        builder.HasOne(g => g.Branch).WithMany().HasForeignKey(g => g.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
