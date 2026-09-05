using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("VENDOR");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.VendorCode)
            .HasColumnName("VENDOR_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.NameLocal)
            .HasColumnName("NAME_LOCAL")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ApControlAccountCode)
            .HasColumnName("AP_CONTROL_ACCOUNT_CODE")
            .HasMaxLength(50)
            .HasDefaultValue("211101")
            .IsRequired();

        builder.Property(e => e.DefaultCurrencyId)
            .HasColumnName("DEFAULT_CURRENCY_ID");

        builder.Property(e => e.PaymentTermsDays)
            .HasColumnName("PAYMENT_TERMS_DAYS")
            .HasDefaultValue(30)
            .IsRequired();

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID");

        builder.Property(e => e.TaxNumber)
            .HasColumnName("TAX_NUMBER")
            .HasMaxLength(50);

        builder.Property(e => e.Phone)
            .HasColumnName("PHONE")
            .HasMaxLength(50);

        builder.Property(e => e.Email)
            .HasColumnName("EMAIL")
            .HasMaxLength(100);

        builder.Property(e => e.Address)
            .HasColumnName("ADDRESS")
            .HasMaxLength(500);

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasDefaultValue(true)
            .IsRequired();

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

        // Unique index on VendorCode
        builder.HasIndex(e => e.VendorCode)
            .IsUnique()
            .HasDatabaseName("UX_VENDOR_CODE");

        // Relationships
        builder.HasOne(e => e.ApControlAccount)
            .WithMany()
            .HasForeignKey(e => e.ApControlAccountCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.DefaultCurrency)
            .WithMany()
            .HasForeignKey(e => e.DefaultCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
