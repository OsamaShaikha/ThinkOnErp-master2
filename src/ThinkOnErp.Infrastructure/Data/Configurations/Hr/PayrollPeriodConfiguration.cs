using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.ToTable("HR_PAYROLL_PERIOD");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(p => p.PeriodCode).HasColumnName("PERIOD_CODE").HasMaxLength(20).IsRequired();
        builder.Property(p => p.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(p => p.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(p => p.FiscalYear).HasColumnName("FISCAL_YEAR").IsRequired();
        builder.Property(p => p.Month).HasColumnName("MONTH").IsRequired();
        builder.Property(p => p.StartDate).HasColumnName("START_DATE").IsRequired();
        builder.Property(p => p.EndDate).HasColumnName("END_DATE").IsRequired();
        builder.Property(p => p.PayDate).HasColumnName("PAY_DATE").IsRequired();
        builder.Property(p => p.PayrollType).HasColumnName("PAYROLL_TYPE").HasMaxLength(50).HasDefaultValue("MONTHLY").IsRequired();
        builder.Property(p => p.Status).HasColumnName("STATUS").HasMaxLength(50).HasDefaultValue("OPEN").IsRequired();
        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");

        // Unique constraint: CompanyId + FiscalYear + Month + PayrollType
        builder.HasIndex(p => new { p.CompanyId, p.FiscalYear, p.Month, p.PayrollType }).IsUnique();
    }
}
