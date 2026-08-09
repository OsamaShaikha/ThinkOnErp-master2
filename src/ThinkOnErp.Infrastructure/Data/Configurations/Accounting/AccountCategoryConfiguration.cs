using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public class AccountCategoryConfiguration : IEntityTypeConfiguration<AccountCategory>
{
    public void Configure(EntityTypeBuilder<AccountCategory> builder)
    {
        builder.ToTable("ACCOUNT_CATEGORY", tableBuilder =>
        {
            tableBuilder.ExcludeFromMigrations();
            tableBuilder.HasCheckConstraint(
                "CK_ACC_CATEGORY_CODE",
                "\"CATEGORY_CODE\" BETWEEN 1 AND 8");
            tableBuilder.HasCheckConstraint(
                "CK_ACC_CATEGORY_BALANCE",
                "\"NORMAL_BALANCE\" IN ('D', 'C')");
            tableBuilder.HasCheckConstraint(
                "CK_ACC_CATEGORY_STATEMENT",
                "\"FINANCIAL_STATEMENT\" IN ('BALANCE_SHEET', 'INCOME_STATEMENT')");
        });

        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id)
            .HasColumnType("NUMBER(19)")
            .ValueGeneratedNever();
        builder.Property(category => category.CategoryCode)
            .HasColumnName("CATEGORY_CODE")
            .HasColumnType("NUMBER(2)")
            .IsRequired();
        builder.Property(category => category.NameAr)
            .HasColumnName("NAME_AR")
            .HasColumnType("NVARCHAR2(200)")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(category => category.NameEn)
            .HasColumnName("NAME_EN")
            .HasColumnType("NVARCHAR2(200)")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(category => category.NormalBalance)
            .HasColumnName("NORMAL_BALANCE")
            .HasColumnType("NVARCHAR2(1)")
            .HasMaxLength(1)
            .IsRequired();
        builder.Property(category => category.FinancialStatement)
            .HasColumnName("FINANCIAL_STATEMENT")
            .HasColumnType("NVARCHAR2(20)")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(category => category.DisplayOrder)
            .HasColumnName("DISPLAY_ORDER")
            .HasColumnType("NUMBER(10)")
            .IsRequired();

        builder.HasIndex(category => category.CategoryCode)
            .IsUnique()
            .HasDatabaseName("UX_ACC_CATEGORY_CODE");
    }
}
