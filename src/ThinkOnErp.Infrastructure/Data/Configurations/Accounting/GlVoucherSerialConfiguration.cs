using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlVoucherSerialConfiguration : IEntityTypeConfiguration<GlVoucherSerial>
{
    public void Configure(EntityTypeBuilder<GlVoucherSerial> builder)
    {
        builder.ToTable("GL_VOUCHER_SERIAL");

        builder.HasKey(s => new { s.BranchId, s.SerialYear, s.SerialMonth, s.VoucherType });

        builder.Property(s => s.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(s => s.SerialYear)
            .HasColumnName("SERIAL_YEAR")
            .IsRequired();

        builder.Property(s => s.SerialMonth)
            .HasColumnName("SERIAL_MONTH")
            .IsRequired();

        builder.Property(s => s.VoucherType)
            .HasColumnName("VOUCHER_TYPE")
            .IsRequired();

        builder.Property(s => s.LastSerialNo)
            .HasColumnName("LAST_SERIAL_NO")
            .HasDefaultValue(0L);
    }
}
