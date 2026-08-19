using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class ArSubledgerTransactionConfiguration : IEntityTypeConfiguration<ArSubledgerTransaction>
{
    public void Configure(EntityTypeBuilder<ArSubledgerTransaction> builder)
    {
        builder.ToTable("AR_SUBLEDGER_TRANSACTION");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.CustomerCode)
            .HasColumnName("CUSTOMER_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.JournalLineId)
            .HasColumnName("JOURNAL_LINE_ID")
            .IsRequired();

        builder.Property(e => e.VoucherId)
            .HasColumnName("VOUCHER_ID")
            .IsRequired();

        builder.Property(e => e.TransactionType)
            .HasColumnName("TRANSACTION_TYPE")
            .HasMaxLength(30)
            .HasDefaultValue("INVOICE")
            .IsRequired();

        builder.Property(e => e.TransactionDate)
            .HasColumnName("TRANSACTION_DATE")
            .IsRequired();

        builder.Property(e => e.DueDate)
            .HasColumnName("DUE_DATE");

        builder.Property(e => e.Amount)
            .HasColumnName("AMOUNT")
            .HasColumnType("NUMBER(18,3)")
            .IsRequired();

        builder.Property(e => e.CurrencyId)
            .HasColumnName("CURRENCY_ID")
            .IsRequired();

        builder.Property(e => e.ExchangeRate)
            .HasColumnName("EXCHANGE_RATE")
            .HasColumnType("NUMBER(18,6)")
            .HasDefaultValue(1.0m)
            .IsRequired();

        builder.Property(e => e.LocalAmount)
            .HasColumnName("LOCAL_AMOUNT")
            .HasColumnType("NUMBER(18,3)")
            .IsRequired();

        builder.Property(e => e.OpenAmount)
            .HasColumnName("OPEN_AMOUNT")
            .HasColumnType("NUMBER(18,3)")
            .IsRequired();

        builder.Property(e => e.LocalOpenAmount)
            .HasColumnName("LOCAL_OPEN_AMOUNT")
            .HasColumnType("NUMBER(18,3)")
            .IsRequired();

        builder.Property(e => e.ReferenceNo)
            .HasColumnName("REFERENCE_NO")
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(500);

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(e => e.CustomerCode)
            .HasDatabaseName("IX_AR_SUB_CUST");

        builder.HasIndex(e => e.TransactionDate)
            .HasDatabaseName("IX_AR_SUB_DATE");

        builder.HasIndex(e => new { e.CustomerCode, e.OpenAmount })
            .HasDatabaseName("IX_AR_SUB_CUST_OPEN");

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerCode)
            .HasPrincipalKey(c => c.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Voucher)
            .WithMany()
            .HasForeignKey(e => e.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.JournalLine)
            .WithMany()
            .HasForeignKey(e => e.JournalLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ArCashApplicationConfiguration : IEntityTypeConfiguration<ArCashApplication>
{
    public void Configure(EntityTypeBuilder<ArCashApplication> builder)
    {
        builder.ToTable("AR_CASH_APPLICATION");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.PaymentTransactionId)
            .HasColumnName("PAYMENT_TRANSACTION_ID")
            .IsRequired();

        builder.Property(e => e.InvoiceTransactionId)
            .HasColumnName("INVOICE_TRANSACTION_ID")
            .IsRequired();

        builder.Property(e => e.AppliedAmount)
            .HasColumnName("APPLIED_AMOUNT")
            .HasColumnType("NUMBER(18,3)")
            .IsRequired();

        builder.Property(e => e.LocalAppliedAmount)
            .HasColumnName("LOCAL_APPLIED_AMOUNT")
            .HasColumnType("NUMBER(18,3)")
            .IsRequired();

        builder.Property(e => e.AppliedDate)
            .HasColumnName("APPLIED_DATE")
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(500);

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.HasOne(e => e.PaymentTransaction)
            .WithMany(t => t.PaymentApplications)
            .HasForeignKey(e => e.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.InvoiceTransaction)
            .WithMany(t => t.InvoiceApplications)
            .HasForeignKey(e => e.InvoiceTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
