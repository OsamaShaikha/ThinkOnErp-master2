using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlPostingRuleConfiguration : IEntityTypeConfiguration<GlPostingRule>
{
    public void Configure(EntityTypeBuilder<GlPostingRule> builder)
    {
        builder.ToTable("GL_POSTING_RULE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(e => e.Module).HasColumnName("MODULE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.EventType).HasColumnName("EVENT_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.EventNameAr).HasColumnName("EVENT_NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(e => e.EventNameEn).HasColumnName("EVENT_NAME_EN").HasMaxLength(200).IsRequired();

        builder.Property(e => e.DebitAccountCode).HasColumnName("DEBIT_ACCOUNT_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreditAccountCode).HasColumnName("CREDIT_ACCOUNT_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.DefaultCostCenterCode).HasColumnName("DEFAULT_COST_CENTER_CODE").HasMaxLength(50);
        builder.Property(e => e.DefaultVoucherType).HasColumnName("DEFAULT_VOUCHER_TYPE").IsRequired();
        builder.Property(e => e.DescriptionTemplate).HasColumnName("DESCRIPTION_TEMPLATE").HasMaxLength(500);

        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(50);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        // Indexes
        builder.HasIndex(e => new { e.Module, e.EventType, e.BranchId }).HasDatabaseName("IX_POSTING_MOD_EVT_BR");

        // Relationships
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DebitAccount).WithMany().HasForeignKey(e => e.DebitAccountCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreditAccount).WithMany().HasForeignKey(e => e.CreditAccountCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DefaultCostCenter).WithMany().HasForeignKey(e => e.DefaultCostCenterCode).OnDelete(DeleteBehavior.Restrict);
    }
}
