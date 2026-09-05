using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class PayrollCalculationSnapshotConfiguration : IEntityTypeConfiguration<PayrollCalculationSnapshot>
{
    public void Configure(EntityTypeBuilder<PayrollCalculationSnapshot> builder)
    {
        builder.ToTable("HR_PAYROLL_CALC_SNAPSHOT");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.PayrollRunLineId).HasColumnName("PAYROLL_RUN_LINE_ID").IsRequired();
        builder.Property(s => s.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(s => s.PayPeriod).HasColumnName("PAY_PERIOD").HasMaxLength(20).IsRequired();
        builder.Property(s => s.ProrationPolicyCode).HasColumnName("PRORATION_POLICY_CODE").HasMaxLength(50);
        builder.Property(s => s.ProrationMethodUsed).HasColumnName("PRORATION_METHOD_USED").HasMaxLength(50);
        builder.Property(s => s.ProrationFactor).HasColumnName("PRORATION_FACTOR").HasColumnType("NUMBER(10,6)").HasDefaultValue(1.0m);
        builder.Property(s => s.EligibleDays).HasColumnName("ELIGIBLE_DAYS").HasColumnType("NUMBER(5,2)").HasDefaultValue(0);
        builder.Property(s => s.TotalBaseDays).HasColumnName("TOTAL_BASE_DAYS").HasColumnType("NUMBER(5,2)").HasDefaultValue(0);
        builder.Property(s => s.TaxPolicyCode).HasColumnName("TAX_POLICY_CODE").HasMaxLength(50);
        builder.Property(s => s.TaxExemptionsApplied).HasColumnName("TAX_EXEMPTIONS_APPLIED").HasColumnType("NUMBER(18,4)").HasDefaultValue(0);
        builder.Property(s => s.SscPolicyCode).HasColumnName("SSC_POLICY_CODE").HasMaxLength(50);
        builder.Property(s => s.SscCapApplied).HasColumnName("SSC_CAP_APPLIED").HasColumnType("NUMBER(18,4)").HasDefaultValue(0);
        builder.Property(s => s.SscEmployeeRateApplied).HasColumnName("SSC_EMP_RATE_APPLIED").HasColumnType("NUMBER(10,6)").HasDefaultValue(0);
        builder.Property(s => s.SscEmployerRateApplied).HasColumnName("SSC_EMPR_RATE_APPLIED").HasColumnType("NUMBER(10,6)").HasDefaultValue(0);
        builder.Property(s => s.OvertimeHoursApplied).HasColumnName("OVERTIME_HOURS_APPLIED").HasColumnType("NUMBER(5,2)").HasDefaultValue(0);
        builder.Property(s => s.OvertimeEarningsApplied).HasColumnName("OVERTIME_EARNINGS_APPLIED").HasColumnType("NUMBER(18,4)").HasDefaultValue(0);
        builder.Property(s => s.LoanDeductionsApplied).HasColumnName("LOAN_DEDUCTIONS_APPLIED").HasColumnType("NUMBER(18,4)").HasDefaultValue(0);
        builder.Property(s => s.LoanDeductionsCarriedForward).HasColumnName("LOAN_DEDUCTIONS_CARRIED_FWD").HasColumnType("NUMBER(18,4)").HasDefaultValue(0);
        builder.Property(s => s.MathematicalExplanationJson).HasColumnName("EXPLANATION_JSON").HasColumnType("CLOB");
        builder.Property(s => s.CalculationTimestamp).HasColumnName("CALCULATION_TIMESTAMP").IsRequired();

        builder.HasIndex(s => s.PayrollRunLineId).IsUnique();
        builder.HasIndex(s => new { s.EmployeeCode, s.PayPeriod });
        builder.HasOne(s => s.PayrollRunLine).WithMany().HasForeignKey(s => s.PayrollRunLineId).OnDelete(DeleteBehavior.Cascade);
    }
}
