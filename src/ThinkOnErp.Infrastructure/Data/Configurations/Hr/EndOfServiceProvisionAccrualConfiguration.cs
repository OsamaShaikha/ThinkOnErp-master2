using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EndOfServiceProvisionAccrualConfiguration : IEntityTypeConfiguration<EndOfServiceProvisionAccrual>
{
    public void Configure(EntityTypeBuilder<EndOfServiceProvisionAccrual> builder)
    {
        builder.ToTable("HR_EOS_PROVISION_ACCRUAL");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.PayPeriod)
            .HasColumnName("PAY_PERIOD")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.BasicSalary)
            .HasColumnName("BASIC_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(a => a.ServiceYears)
            .HasColumnName("SERVICE_YEARS")
            .HasColumnType("NUMBER(6,2)")
            .IsRequired();

        builder.Property(a => a.MonthlyAccrualAmount)
            .HasColumnName("MONTHLY_ACCRUAL_AMOUNT")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(a => a.TotalAccumulatedProvision)
            .HasColumnName("TOTAL_ACCUMULATED_PROVISION")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(a => a.JournalVoucherId)
            .HasColumnName("JOURNAL_VOUCHER_ID");

        builder.Property(a => a.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.HasIndex(a => new { a.EmployeeCode, a.PayPeriod })
            .IsUnique()
            .HasDatabaseName("IX_HR_EOS_EMP_PERIOD");

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
