using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlVoucherHeaderConfiguration : IEntityTypeConfiguration<GlVoucherHeader>
{
    public void Configure(EntityTypeBuilder<GlVoucherHeader> builder)
    {
        builder.ToTable("GL_VOUCHER_HEADER");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(h => h.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(h => h.FiscalYearId)
            .HasColumnName("FISCAL_YEAR_ID")
            .IsRequired();

        builder.Property(h => h.VoucherYear)
            .HasColumnName("VOUCHER_YEAR")
            .IsRequired();

        builder.Property(h => h.VoucherMonth)
            .HasColumnName("VOUCHER_MONTH")
            .IsRequired();

        builder.Property(h => h.VoucherType)
            .HasColumnName("VOUCHER_TYPE")
            .IsRequired();

        builder.Property(h => h.VoucherNo)
            .HasColumnName("VOUCHER_NO")
            .IsRequired();

        builder.Property(h => h.VoucherDate)
            .HasColumnName("VOUCHER_DATE")
            .IsRequired();

        builder.Property(h => h.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(2000);

        builder.Property(h => h.TotalAmount)
            .HasColumnName("TOTAL_AMOUNT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(h => h.TotalLocalDebit)
            .HasColumnName("TOTAL_LOCAL_DEBIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(h => h.TotalLocalCredit)
            .HasColumnName("TOTAL_LOCAL_CREDIT")
            .HasColumnType("NUMBER(18,3)")
            .HasDefaultValue(0m);

        builder.Property(h => h.Status)
            .HasColumnName("STATUS")
            .HasDefaultValue(1);

        builder.Property(h => h.IsAutoRecord)
            .HasColumnName("IS_AUTO_RECORD")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false);

        builder.Property(h => h.SourceSystemCode)
            .HasColumnName("SOURCE_SYSTEM_CODE")
            .HasMaxLength(100);

        builder.Property(h => h.SourceRefId)
            .HasColumnName("SOURCE_REF_ID");

        builder.Property(h => h.IsStandby)
            .HasColumnName("IS_STANDBY")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false);

        builder.Property(h => h.IsReviewed)
            .HasColumnName("IS_REVIEWED")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false);

        builder.Property(h => h.ReviewUser)
            .HasColumnName("REVIEW_USER")
            .HasMaxLength(100);

        builder.Property(h => h.ReviewDate)
            .HasColumnName("REVIEW_DATE");

        builder.Property(h => h.PostUser)
            .HasColumnName("POST_USER")
            .HasMaxLength(100);

        builder.Property(h => h.PostDate)
            .HasColumnName("POST_DATE");

        builder.Property(h => h.UnpostUser)
            .HasColumnName("UNPOST_USER")
            .HasMaxLength(100);

        builder.Property(h => h.UnpostDate)
            .HasColumnName("UNPOST_DATE");

        builder.Property(h => h.IsReversed)
            .HasColumnName("IS_REVERSED")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false);

        builder.Property(h => h.ReverseUser)
            .HasColumnName("REVERSE_USER")
            .HasMaxLength(100);

        builder.Property(h => h.ReverseDate)
            .HasColumnName("REVERSE_DATE");

        builder.Property(h => h.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(h => h.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(h => h.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(h => h.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(h => new { h.BranchId, h.VoucherYear, h.VoucherMonth, h.VoucherType, h.VoucherNo })
            .IsUnique()
            .HasDatabaseName("UX_GL_VOUCHER_NO");

        builder.HasMany(h => h.Details)
            .WithOne(d => d.Header)
            .HasForeignKey(d => d.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
