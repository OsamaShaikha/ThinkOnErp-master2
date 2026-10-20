using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosBranchSettingConfiguration : IEntityTypeConfiguration<PosBranchSetting>
{
    public void Configure(EntityTypeBuilder<PosBranchSetting> builder)
    {
        builder.ToTable("POS_BRANCH_SETTING");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(s => s.DefaultCashAccount).HasColumnName("DEFAULT_CASH_ACCOUNT").HasMaxLength(50);
        builder.Property(s => s.DefaultExpenseAccount).HasColumnName("DEFAULT_EXPENSE_ACCOUNT").HasMaxLength(50);
        builder.Property(s => s.DefaultReceiptAccount).HasColumnName("DEFAULT_RECEIPT_ACCOUNT").HasMaxLength(50);
        builder.Property(s => s.DefaultWarehouseId).HasColumnName("DEFAULT_WAREHOUSE_ID");
        builder.Property(s => s.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.Property(s => s.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(s => s.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(s => s.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(s => s.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(s => s.BranchId).IsUnique();
        builder.HasOne(s => s.Branch).WithMany().HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
