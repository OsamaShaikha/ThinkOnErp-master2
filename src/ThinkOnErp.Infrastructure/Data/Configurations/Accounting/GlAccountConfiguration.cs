using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public class GlAccountConfiguration : IEntityTypeConfiguration<GlAccount>
{
    public void Configure(EntityTypeBuilder<GlAccount> builder)
    {
        builder.ToTable("GL_ACCOUNT", tableBuilder =>
        {
            tableBuilder.ExcludeFromMigrations();
            tableBuilder.HasCheckConstraint(
                "CK_GL_ACCOUNT_LEVEL",
                "\"ACCOUNT_LEVEL\" BETWEEN 1 AND 5");
            tableBuilder.HasCheckConstraint(
                "CK_GL_ACCOUNT_TYPE",
                "\"ACCOUNT_TYPE\" IN ('HEADER', 'DETAIL')");
            tableBuilder.HasCheckConstraint(
                "CK_GL_ACCOUNT_BALANCE",
                "\"NORMAL_BALANCE\" IN ('D', 'C')");
            tableBuilder.HasCheckConstraint(
                "CK_GL_ACCOUNT_FLAGS",
                "\"IS_CONTRA\" IN (0, 1) AND " +
                "\"IS_CONTROL_ACCOUNT\" IN (0, 1) AND " +
                "\"IS_BRANCH_SPECIFIC\" IN (0, 1) AND " +
                "\"IS_CLEARING\" IN (0, 1) AND " +
                "\"IS_ACTIVE\" IN (0, 1)");
            tableBuilder.HasCheckConstraint(
                "CK_GL_ACCOUNT_CONTROL_TYPE",
                "(\"IS_CONTROL_ACCOUNT\" = 0 AND \"CONTROL_ACCOUNT_TYPE\" IS NULL) OR " +
                "(\"IS_CONTROL_ACCOUNT\" = 1 AND \"CONTROL_ACCOUNT_TYPE\" IN ('AR', 'AP', 'INVENTORY'))");
        });

        builder.HasKey(account => account.AccountCode);

        builder.Property(account => account.AccountCode)
            .HasColumnName("ACCOUNT_CODE")
            .HasColumnType("NVARCHAR2(50)")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(account => account.OldAccountCode)
            .HasColumnName("OLD_ACCOUNT_CODE")
            .HasColumnType("NVARCHAR2(50)")
            .HasMaxLength(50);
        builder.Property(account => account.AccountNameAr)
            .HasColumnName("ACCOUNT_NAME_AR")
            .HasColumnType("NVARCHAR2(200)")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(account => account.AccountNameEn)
            .HasColumnName("ACCOUNT_NAME_EN")
            .HasColumnType("NVARCHAR2(200)")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(account => account.ParentAccountCode)
            .HasColumnName("PARENT_ACCOUNT_CODE")
            .HasColumnType("NVARCHAR2(50)")
            .HasMaxLength(50);
        builder.Property(account => account.AccountLevel)
            .HasColumnName("ACCOUNT_LEVEL")
            .HasColumnType("NUMBER(2)")
            .IsRequired();
        builder.Property(account => account.AccountType)
            .HasColumnName("ACCOUNT_TYPE")
            .HasColumnType("NVARCHAR2(10)")
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(account => account.NormalBalance)
            .HasColumnName("NORMAL_BALANCE")
            .HasColumnType("NVARCHAR2(1)")
            .HasMaxLength(1)
            .IsRequired();
        builder.Property(account => account.IsContra)
            .HasColumnName("IS_CONTRA")
            .HasColumnType("NUMBER(1)")
            .IsRequired();
        builder.Property(account => account.IsControlAccount)
            .HasColumnName("IS_CONTROL_ACCOUNT")
            .HasColumnType("NUMBER(1)")
            .IsRequired();
        builder.Property(account => account.ControlAccountType)
            .HasColumnName("CONTROL_ACCOUNT_TYPE")
            .HasColumnType("NVARCHAR2(20)")
            .HasMaxLength(20);
        builder.Property(account => account.IsBranchSpecific)
            .HasColumnName("IS_BRANCH_SPECIFIC")
            .HasColumnType("NUMBER(1)")
            .IsRequired();
        builder.Property(account => account.IsClearing)
            .HasColumnName("IS_CLEARING")
            .HasColumnType("NUMBER(1)")
            .IsRequired();
        builder.Property(account => account.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .IsRequired();
        builder.Property(account => account.Description)
            .HasColumnName("DESCRIPTION")
            .HasColumnType("NVARCHAR2(1000)")
            .HasMaxLength(1000);
        builder.Property(account => account.Notes)
            .HasColumnName("NOTES")
            .HasColumnType("NVARCHAR2(2000)")
            .HasMaxLength(2000);

        builder.Ignore(account => account.IsPostable);

        builder.HasOne(account => account.ParentAccount)
            .WithMany(account => account.ChildrenAccounts)
            .HasForeignKey(account => account.ParentAccountCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(account => account.ParentAccountCode)
            .HasDatabaseName("IX_GL_ACCOUNT_PARENT");
        builder.HasIndex(account => account.IsControlAccount)
            .HasDatabaseName("IX_GL_ACCOUNT_CTRL");
        builder.HasIndex(account => account.OldAccountCode)
            .HasDatabaseName("IX_GL_ACCOUNT_OLD_CODE");
    }
}

