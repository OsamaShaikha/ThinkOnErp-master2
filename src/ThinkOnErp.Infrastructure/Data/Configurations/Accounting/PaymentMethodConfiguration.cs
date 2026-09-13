using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PAYMENT_METHOD");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.Code).HasColumnName("CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();

        builder.Property(e => e.MethodType).HasColumnName("METHOD_TYPE").HasMaxLength(30).IsRequired();
        builder.Property(e => e.GlAccountCode).HasColumnName("GL_ACCOUNT_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.BankAccountId).HasColumnName("BANK_ACCOUNT_ID");
        builder.Property(e => e.CashRegisterId).HasColumnName("CASH_REGISTER_ID");

        builder.Property(e => e.CommissionPercent).HasColumnName("COMMISSION_PERCENT").HasPrecision(8, 4).HasDefaultValue(0);
        builder.Property(e => e.CommissionFixedAmount).HasColumnName("COMMISSION_FIXED_AMOUNT").HasPrecision(18, 4).HasDefaultValue(0);
        builder.Property(e => e.CommissionGlAccountCode).HasColumnName("COMMISSION_GL_ACCOUNT_CODE").HasMaxLength(50);

        builder.Property(e => e.RequiresReference).HasColumnName("REQUIRES_REFERENCE").HasDefaultValue(false);
        builder.Property(e => e.RequiresDueDate).HasColumnName("REQUIRES_DUE_DATE").HasDefaultValue(false);
        builder.Property(e => e.AutoPostGl).HasColumnName("AUTO_POST_GL").HasDefaultValue(true);
        builder.Property(e => e.ShowInPos).HasColumnName("SHOW_IN_POS").HasDefaultValue(true);
        builder.Property(e => e.ShowInInvoices).HasColumnName("SHOW_IN_INVOICES").HasDefaultValue(true);
        builder.Property(e => e.ShowInVouchers).HasColumnName("SHOW_IN_VOUCHERS").HasDefaultValue(true);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
        builder.Property(e => e.DisplayOrder).HasColumnName("DISPLAY_ORDER").HasDefaultValue(1);

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(50);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(e => new { e.BranchId, e.Code }).IsUnique().HasDatabaseName("UX_PM_BRANCH_CODE");

        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.GlAccount).WithMany().HasForeignKey(e => e.GlAccountCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CommissionGlAccount).WithMany().HasForeignKey(e => e.CommissionGlAccountCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.BankAccount).WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
    }
}
