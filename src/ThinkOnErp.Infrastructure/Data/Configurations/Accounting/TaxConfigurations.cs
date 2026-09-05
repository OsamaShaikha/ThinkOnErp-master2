using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class TaxCategoryConfiguration : IEntityTypeConfiguration<TaxCategory>
{
    public void Configure(EntityTypeBuilder<TaxCategory> builder)
    {
        builder.ToTable("TAX_CATEGORY");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(c => c.CategoryCode).HasColumnName("CATEGORY_CODE").HasMaxLength(50).IsRequired();
        builder.Property(c => c.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(150).IsRequired();
        builder.Property(c => c.NameEn).HasColumnName("NAME_EN").HasMaxLength(150).IsRequired();
        builder.Property(c => c.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
        builder.Property(c => c.DisplayOrder).HasColumnName("DISPLAY_ORDER").HasDefaultValue(1);
        builder.Property(c => c.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);

        builder.Property(c => c.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(c => c.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(c => c.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(c => c.CategoryCode).IsUnique().HasDatabaseName("UX_TAX_CAT_CODE");
    }
}

public sealed class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("TAX_RATE");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(r => r.TaxRateCode).HasColumnName("TAX_RATE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(r => r.TaxCategoryId).HasColumnName("TAX_CATEGORY_ID").IsRequired();
        builder.Property(r => r.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(150).IsRequired();
        builder.Property(r => r.NameEn).HasColumnName("NAME_EN").HasMaxLength(150).IsRequired();

        builder.Property(r => r.RatePercent).HasColumnName("RATE_PERCENT").HasPrecision(8, 4).IsRequired();
        builder.Property(r => r.RateType).HasColumnName("RATE_TYPE").HasMaxLength(30).HasDefaultValue("PERCENTAGE").IsRequired();

        builder.Property(r => r.SalesTaxGlAccountCode).HasColumnName("SALES_TAX_GL_ACCOUNT_CODE").HasMaxLength(50);
        builder.Property(r => r.PurchaseTaxGlAccountCode).HasColumnName("PURCHASE_TAX_GL_ACCOUNT_CODE").HasMaxLength(50);

        builder.Property(r => r.IsExempt).HasColumnName("IS_EXEMPT").HasDefaultValue(false);
        builder.Property(r => r.IsZeroRated).HasColumnName("IS_ZERO_RATED").HasDefaultValue(false);

        builder.Property(r => r.ExemptionReasonCode).HasColumnName("EXEMPTION_REASON_CODE").HasMaxLength(50);
        builder.Property(r => r.ExemptionReasonLocal).HasColumnName("EXEMPTION_REASON_LOCAL").HasMaxLength(300);
        builder.Property(r => r.ExemptionReasonEn).HasColumnName("EXEMPTION_REASON_EN").HasMaxLength(300);

        builder.Property(r => r.DisplayOrder).HasColumnName("DISPLAY_ORDER").HasDefaultValue(1);
        builder.Property(r => r.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
        builder.Property(r => r.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);

        builder.Property(r => r.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(r => r.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(r => r.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(r => r.TaxRateCode).IsUnique().HasDatabaseName("UX_TAX_RATE_CODE");

        builder.HasOne(r => r.Category)
            .WithMany(c => c.TaxRates)
            .HasForeignKey(r => r.TaxCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.SalesTaxGlAccount)
            .WithMany()
            .HasForeignKey(r => r.SalesTaxGlAccountCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PurchaseTaxGlAccount)
            .WithMany()
            .HasForeignKey(r => r.PurchaseTaxGlAccountCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TaxGroupConfiguration : IEntityTypeConfiguration<TaxGroup>
{
    public void Configure(EntityTypeBuilder<TaxGroup> builder)
    {
        builder.ToTable("TAX_GROUP");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(g => g.GroupCode).HasColumnName("GROUP_CODE").HasMaxLength(50).IsRequired();
        builder.Property(g => g.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(150).IsRequired();
        builder.Property(g => g.NameEn).HasColumnName("NAME_EN").HasMaxLength(150).IsRequired();
        builder.Property(g => g.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
        builder.Property(g => g.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);

        builder.Property(g => g.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(g => g.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(g => g.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(g => g.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(g => g.GroupCode).IsUnique().HasDatabaseName("UX_TAX_GRP_CODE");

        builder.HasMany(g => g.Items)
            .WithOne(i => i.Group)
            .HasForeignKey(i => i.TaxGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TaxGroupItemConfiguration : IEntityTypeConfiguration<TaxGroupItem>
{
    public void Configure(EntityTypeBuilder<TaxGroupItem> builder)
    {
        builder.ToTable("TAX_GROUP_ITEM");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(i => i.TaxGroupId).HasColumnName("TAX_GROUP_ID").IsRequired();
        builder.Property(i => i.TaxRateId).HasColumnName("TAX_RATE_ID").IsRequired();
        builder.Property(i => i.ApplicationOrder).HasColumnName("APPLICATION_ORDER").HasDefaultValue(1);
        builder.Property(i => i.IsCompound).HasColumnName("IS_COMPOUND").HasDefaultValue(false);

        builder.HasOne(i => i.TaxRate)
            .WithMany()
            .HasForeignKey(i => i.TaxRateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TaxGroupId, i.TaxRateId }).IsUnique().HasDatabaseName("UX_TAX_GRP_ITEM");
    }
}

public sealed class TaxTransactionConfiguration : IEntityTypeConfiguration<TaxTransaction>
{
    public void Configure(EntityTypeBuilder<TaxTransaction> builder)
    {
        builder.ToTable("TAX_TRANSACTION");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(t => t.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(t => t.FiscalYearId).HasColumnName("FISCAL_YEAR_ID").IsRequired();
        builder.Property(t => t.TaxRateId).HasColumnName("TAX_RATE_ID").IsRequired();

        builder.Property(t => t.SourceModule).HasColumnName("SOURCE_MODULE").HasMaxLength(30).IsRequired();
        builder.Property(t => t.SourceDocumentId).HasColumnName("SOURCE_DOCUMENT_ID").IsRequired();
        builder.Property(t => t.SourceDocumentNo).HasColumnName("SOURCE_DOCUMENT_NO").HasMaxLength(50).IsRequired();
        builder.Property(t => t.DocumentDate).HasColumnName("DOCUMENT_DATE").IsRequired();
        builder.Property(t => t.TaxDate).HasColumnName("TAX_DATE").IsRequired();

        builder.Property(t => t.PartyType).HasColumnName("PARTY_TYPE").HasMaxLength(20);
        builder.Property(t => t.PartyCode).HasColumnName("PARTY_CODE").HasMaxLength(50);
        builder.Property(t => t.PartyName).HasColumnName("PARTY_NAME").HasMaxLength(200);
        builder.Property(t => t.PartyTaxNumber).HasColumnName("PARTY_TAX_NUMBER").HasMaxLength(50);

        builder.Property(t => t.BaseAmount).HasColumnName("BASE_AMOUNT").HasPrecision(18, 3).IsRequired();
        builder.Property(t => t.TaxPercent).HasColumnName("TAX_PERCENT").HasPrecision(8, 4).IsRequired();
        builder.Property(t => t.TaxAmount).HasColumnName("TAX_AMOUNT").HasPrecision(18, 3).IsRequired();

        builder.Property(t => t.LocalBaseAmount).HasColumnName("LOCAL_BASE_AMOUNT").HasPrecision(18, 3).IsRequired();
        builder.Property(t => t.LocalTaxAmount).HasColumnName("LOCAL_TAX_AMOUNT").HasPrecision(18, 3).IsRequired();

        builder.Property(t => t.CurrencyId).HasColumnName("CURRENCY_ID").HasDefaultValue(1L).IsRequired();
        builder.Property(t => t.ExchangeRate).HasColumnName("EXCHANGE_RATE").HasPrecision(14, 6).HasDefaultValue(1.0m).IsRequired();

        builder.Property(t => t.IsSalesTax).HasColumnName("IS_SALES_TAX").IsRequired();
        builder.Property(t => t.IsRecoverable).HasColumnName("IS_RECOVERABLE").HasDefaultValue(true).IsRequired();

        builder.Property(t => t.TaxGlAccountCode).HasColumnName("TAX_GL_ACCOUNT_CODE").HasMaxLength(50);
        builder.Property(t => t.GlVoucherId).HasColumnName("GL_VOUCHER_ID");

        builder.Property(t => t.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(t => t.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(t => t.CreationDate).HasColumnName("CREATION_DATE").IsRequired();

        builder.HasOne(t => t.TaxRate)
            .WithMany()
            .HasForeignKey(t => t.TaxRateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.GlVoucher)
            .WithMany()
            .HasForeignKey(t => t.GlVoucherId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.TaxDate, t.IsSalesTax }).HasDatabaseName("IX_TAX_TX_DATE_TYPE");
        builder.HasIndex(t => new { t.BranchId, t.FiscalYearId }).HasDatabaseName("IX_TAX_TX_BRANCH_YR");
        builder.HasIndex(t => new { t.SourceModule, t.SourceDocumentId }).HasDatabaseName("IX_TAX_TX_SRC_DOC");
    }
}
