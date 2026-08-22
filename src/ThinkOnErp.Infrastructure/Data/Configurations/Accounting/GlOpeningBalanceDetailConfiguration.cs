using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlOpeningBalanceDetailConfiguration : IEntityTypeConfiguration<GlOpeningBalanceDetail>
{
    public void Configure(EntityTypeBuilder<GlOpeningBalanceDetail> builder)
    {
        builder.ToTable("GL_OPENING_BALANCE_DETAIL", tableBuilder =>
        {
            tableBuilder.ExcludeFromMigrations();
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(d => d.HeaderId)
            .HasColumnName("HEADER_ID")
            .IsRequired();

        builder.Property(d => d.LineSer)
            .HasColumnName("LINE_SER")
            .HasColumnType("NUMBER(5)")
            .IsRequired();

        builder.Property(d => d.AccountCode)
            .HasColumnName("ACCOUNT_CODE")
            .HasColumnType("NVARCHAR2(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.DebitAmount)
            .HasColumnName("DEBIT_AMOUNT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.CreditAmount)
            .HasColumnName("CREDIT_AMOUNT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.LocalDebit)
            .HasColumnName("LOCAL_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.LocalCredit)
            .HasColumnName("LOCAL_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(d => d.CurrencyId)
            .HasColumnName("CURRENCY_ID")
            .HasDefaultValue(1L);

        builder.Property(d => d.ExchangeRate)
            .HasColumnName("EXCHANGE_RATE")
            .HasColumnType("NUMBER(14,6)")
            .HasDefaultValue(1.0m);

        builder.Property(d => d.Description)
            .HasColumnName("DESCRIPTION")
            .HasColumnType("NVARCHAR2(500)")
            .HasMaxLength(500);

        builder.Property(d => d.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasColumnType("NVARCHAR2(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        // Unique line number per header
        builder.HasIndex(d => new { d.HeaderId, d.LineSer })
            .IsUnique()
            .HasDatabaseName("UX_GL_OB_DETAIL_LINE");

        // Index for account queries
        builder.HasIndex(d => d.AccountCode)
            .HasDatabaseName("IX_GL_OB_DETAIL_ACCOUNT");

        // FK to GlAccount
        builder.HasOne(d => d.Account)
            .WithMany()
            .HasForeignKey(d => d.AccountCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
