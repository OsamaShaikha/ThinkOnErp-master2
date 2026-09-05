using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class LoanRepaymentScheduleConfiguration : IEntityTypeConfiguration<LoanRepaymentSchedule>
{
    public void Configure(EntityTypeBuilder<LoanRepaymentSchedule> builder)
    {
        builder.ToTable("HR_LOAN_REPAYMENT_SCHEDULE");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.EmployeeLoanId).HasColumnName("EMPLOYEE_LOAN_ID").IsRequired();
        builder.Property(s => s.InstallmentNo).HasColumnName("INSTALLMENT_NO").IsRequired();
        builder.Property(s => s.PayPeriod).HasColumnName("PAY_PERIOD").HasMaxLength(20).IsRequired();
        builder.Property(s => s.ScheduledAmount).HasColumnName("SCHEDULED_AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(s => s.PaidAmount).HasColumnName("PAID_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0);
        builder.Property(s => s.CarriedForwardAmount).HasColumnName("CARRIED_FORWARD_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0);
        builder.Property(s => s.Status).HasColumnName("STATUS").HasMaxLength(50).HasDefaultValue("PENDING").IsRequired();
        builder.Property(s => s.PaidDate).HasColumnName("PAID_DATE");
        builder.Property(s => s.PayrollRunLineId).HasColumnName("PAYROLL_RUN_LINE_ID");

        builder.HasIndex(s => new { s.EmployeeLoanId, s.PayPeriod });
        builder.HasIndex(s => new { s.PayPeriod, s.Status });
    }
}
