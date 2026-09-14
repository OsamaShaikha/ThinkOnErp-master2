using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeLoanConfiguration : IEntityTypeConfiguration<EmployeeLoan>
{
    public void Configure(EntityTypeBuilder<EmployeeLoan> builder)
    {
        builder.ToTable("HR_EMPLOYEE_LOAN");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(l => l.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(l => l.LoanType).HasColumnName("LOAN_TYPE").HasMaxLength(100).IsRequired();
        builder.Property(l => l.PrincipalAmount).HasColumnName("PRINCIPAL_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.MonthlyInstallmentAmount).HasColumnName("MONTHLY_INSTALLMENT_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.TotalInstallments).HasColumnName("TOTAL_INSTALLMENTS").IsRequired();
        builder.Property(l => l.TotalPaidAmount).HasColumnName("TOTAL_PAID_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.RemainingBalance).HasColumnName("REMAINING_BALANCE").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.StartDate).HasColumnName("START_DATE").IsRequired();
        builder.Property(l => l.EndDate).HasColumnName("END_DATE");
        builder.Property(l => l.Status).HasColumnName("STATUS").HasMaxLength(100).IsRequired();
        builder.Property(l => l.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(200);
        builder.Property(l => l.ApprovalDate).HasColumnName("APPROVAL_DATE");
        builder.Property(l => l.Notes).HasColumnName("NOTES").HasMaxLength(1000);

        builder.Property(l => l.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(l => l.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(l => l.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(l => l.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(l => l.Employee)
            .WithMany()
            .HasForeignKey(l => l.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Schedules)
            .WithOne(s => s.EmployeeLoan)
            .HasForeignKey(s => s.EmployeeLoanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LoanRepaymentScheduleConfiguration : IEntityTypeConfiguration<LoanRepaymentSchedule>
{
    public void Configure(EntityTypeBuilder<LoanRepaymentSchedule> builder)
    {
        builder.ToTable("HR_LOAN_REPAYMENT_SCHEDULE");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(s => s.EmployeeLoanId).HasColumnName("EMPLOYEE_LOAN_ID").IsRequired();
        builder.Property(s => s.InstallmentNo).HasColumnName("INSTALLMENT_NO").IsRequired();
        builder.Property(s => s.PayPeriod).HasColumnName("PAY_PERIOD").HasMaxLength(40).IsRequired();
        builder.Property(s => s.ScheduledAmount).HasColumnName("SCHEDULED_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.PaidAmount).HasColumnName("PAID_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.CarriedForwardAmount).HasColumnName("CARRIED_FORWARD_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.Status).HasColumnName("STATUS").HasMaxLength(100).IsRequired();
        builder.Property(s => s.PaidDate).HasColumnName("PAID_DATE");
        builder.Property(s => s.PayrollRunLineId).HasColumnName("PAYROLL_RUN_LINE_ID");
    }
}

public sealed class EmployeeAdvanceConfiguration : IEntityTypeConfiguration<EmployeeAdvance>
{
    public void Configure(EntityTypeBuilder<EmployeeAdvance> builder)
    {
        builder.ToTable("HR_EMPLOYEE_ADVANCE");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(a => a.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(a => a.AdvanceAmount).HasColumnName("ADVANCE_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(a => a.TargetPayPeriod).HasColumnName("TARGET_PAY_PERIOD").HasMaxLength(40).IsRequired();
        builder.Property(a => a.DeductedAmount).HasColumnName("DEDUCTED_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(a => a.RemainingBalance).HasColumnName("REMAINING_BALANCE").HasPrecision(18, 4).IsRequired();
        builder.Property(a => a.Status).HasColumnName("STATUS").HasMaxLength(100).IsRequired();
        builder.Property(a => a.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(200);
        builder.Property(a => a.ApprovalDate).HasColumnName("APPROVAL_DATE");
        builder.Property(a => a.Reason).HasColumnName("REASON").HasMaxLength(1000);

        builder.Property(a => a.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(a => a.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(a => a.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(a => a.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PayrollAdjustmentConfiguration : IEntityTypeConfiguration<PayrollAdjustment>
{
    public void Configure(EntityTypeBuilder<PayrollAdjustment> builder)
    {
        builder.ToTable("HR_PAYROLL_ADJUSTMENT");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(a => a.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(a => a.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(a => a.PayPeriod).HasColumnName("PAY_PERIOD").HasMaxLength(40).IsRequired();
        builder.Property(a => a.ComponentCode).HasColumnName("COMPONENT_CODE").HasMaxLength(100).IsRequired();
        builder.Property(a => a.AdjustmentType).HasColumnName("ADJUSTMENT_TYPE").HasMaxLength(20).IsRequired();
        builder.Property(a => a.Amount).HasColumnName("AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(a => a.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
        builder.Property(a => a.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();
        builder.Property(a => a.PayrollRunLineId).HasColumnName("PAYROLL_RUN_LINE_ID");

        builder.Property(a => a.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(a => a.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(a => a.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(a => a.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Component)
            .WithMany()
            .HasForeignKey(a => a.ComponentCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
