using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class PayrollCurrencyPrecisionConfiguration : IEntityTypeConfiguration<PayrollCurrencyPrecision>
{
    public void Configure(EntityTypeBuilder<PayrollCurrencyPrecision> builder)
    {
        builder.ToTable("HR_PAYROLL_CURRENCY_PRECISION");
        builder.HasKey(x => x.CurrencyCode);
        builder.Property(x => x.CurrencyCode).HasColumnName("CURRENCY_CODE").HasColumnType("VARCHAR2(10 BYTE)").HasMaxLength(10).IsRequired();
        builder.Property(x => x.DecimalPlaces).HasColumnName("DECIMAL_PLACES").HasColumnType("NUMBER(2)").IsRequired();
        builder.Property(x => x.CreationUser).HasColumnName("CREATION_USER").HasColumnType("VARCHAR2(100 BYTE)").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(x => x.UpdateUser).HasColumnName("UPDATE_USER").HasColumnType("VARCHAR2(100 BYTE)").HasMaxLength(100);
        builder.Property(x => x.UpdateDate).HasColumnName("UPDATE_DATE");
        builder.ToTable(t => t.HasCheckConstraint("CK_HR_PAY_CURRENCY_DECIMALS", "DECIMAL_PLACES BETWEEN 0 AND 4"));
    }
}
