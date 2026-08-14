using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlVoucherTypeConfiguration : IEntityTypeConfiguration<GlVoucherType>
{
    public void Configure(EntityTypeBuilder<GlVoucherType> builder)
    {
        builder.ToTable("GL_VOUCHER_TYPE");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(t => t.TypeCode)
            .HasColumnName("TYPE_CODE")
            .IsRequired();

        builder.Property(t => t.TypeKey)
            .HasColumnName("TYPE_KEY")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Prefix)
            .HasColumnName("PREFIX")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.Category)
            .HasColumnName("CATEGORY")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.SerialResetPolicy)
            .HasColumnName("SERIAL_RESET_POLICY")
            .HasMaxLength(20)
            .HasDefaultValue("MONTHLY")
            .IsRequired();

        builder.Property(t => t.RequiresReview)
            .HasColumnName("REQUIRES_REVIEW")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        builder.Property(t => t.AllowManualEntry)
            .HasColumnName("ALLOW_MANUAL_ENTRY")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        builder.Property(t => t.IsSystem)
            .HasColumnName("IS_SYSTEM")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false);

        builder.Property(t => t.DisplayOrder)
            .HasColumnName("DISPLAY_ORDER")
            .HasDefaultValue(1);

        builder.Property(t => t.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        builder.Property(t => t.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(500);

        builder.Property(t => t.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(t => t.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(t => t.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(t => t.TypeCode)
            .IsUnique()
            .HasDatabaseName("UX_GL_VOUCHER_TYPE_CODE");

        builder.HasIndex(t => t.TypeKey)
            .IsUnique()
            .HasDatabaseName("UX_GL_VOUCHER_TYPE_KEY");
    }
}
