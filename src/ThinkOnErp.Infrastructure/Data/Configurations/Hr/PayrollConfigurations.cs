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

        builder.Property(p => p.PeriodCode).HasColumnName("PERIOD_CODE").HasMaxLength(20).IsRequired();
        builder.HasIndex(p => p.PeriodCode).IsUnique().HasDatabaseName("UX_HR_PAY_PERIOD_CODE");
        builder.Property(p => p.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(p => p.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(p => p.FiscalYear).HasColumnName("FISCAL_YEAR").IsRequired();
        builder.Property(p => p.Month).HasColumnName("MONTH").IsRequired();
        builder.Property(p => p.StartDate).HasColumnName("START_DATE").IsRequired();
        builder.Property(p => p.EndDate).HasColumnName("END_DATE").IsRequired();
        builder.Property(p => p.PayDate).HasColumnName("PAY_DATE").IsRequired();
        builder.Property(p => p.PayrollType).HasColumnName("PAYROLL_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(p => p.Status).HasColumnName("STATUS").HasMaxLength(50).IsRequired();

        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.ToTable("HR_PAYROLL_RUN");
        builder.Property(r => r.CorrectionOfRunId).HasColumnName("CORRECTION_OF_RUN_ID");
        builder.Property(r => r.CorrectionRootRunId).HasColumnName("CORRECTION_ROOT_RUN_ID");
        builder.Property(r => r.CorrectionVersion).HasColumnName("CORRECTION_VERSION").HasDefaultValue(1).IsRequired();
        builder.Property(r => r.CorrectionReason).HasColumnName("CORRECTION_REASON").HasMaxLength(1000);
        builder.Property(r => r.CancelledBy).HasColumnName("CANCELLED_BY").HasMaxLength(200);
        builder.Property(r => r.CancellationDate).HasColumnName("CANCELLATION_DATE");
        builder.Property(r => r.CancellationReason).HasColumnName("CANCELLATION_REASON").HasMaxLength(1000);
        builder.Property(r => r.InputFingerprint).HasColumnName("INPUT_FINGERPRINT").HasMaxLength(64);
        builder.HasKey(r => r.Id);
        builder.HasOne<PayrollRun>().WithMany().HasForeignKey(r => r.CorrectionOfRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollRun>().WithMany().HasForeignKey(r => r.CorrectionRootRunId).OnDelete(DeleteBehavior.Restrict);
        // Oracle's regular unique index would also restrict rows whose root is NULL. The
        // family/version invariant is provisioned as a function-based unique index by
        // HrPayrollCorrectionIndexSchema so each original run can start at version 1.
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(r => r.PayPeriod).HasColumnName("PAY_PERIOD").HasMaxLength(20).IsRequired();
        builder.Property(r => r.RunDate).HasColumnName("RUN_DATE").IsRequired();
        builder.Property(r => r.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(r => r.Status).HasColumnName("STATUS").HasMaxLength(30).IsRequired();

        builder.Property(r => r.TotalGrossSalary).HasColumnName("TOTAL_GROSS_SALARY").HasPrecision(18, 4).IsRequired();
        builder.Property(r => r.TotalNetSalary).HasColumnName("TOTAL_NET_SALARY").HasPrecision(18, 4).IsRequired();
        builder.Property(r => r.TotalEmployeeSsc).HasColumnName("TOTAL_EMPLOYEE_SSC").HasPrecision(18, 4).IsRequired();
        builder.Property(r => r.TotalEmployerSsc).HasColumnName("TOTAL_EMPLOYER_SSC").HasPrecision(18, 4).IsRequired();
        builder.Property(r => r.TotalIncomeTax).HasColumnName("TOTAL_INCOME_TAX").HasPrecision(18, 4).IsRequired();
        builder.Property(r => r.TotalNationalContrib).HasColumnName("TOTAL_NATIONAL_CONTRIB").HasPrecision(18, 4).IsRequired();
        builder.Property(r => r.TotalOtherDeductions).HasColumnName("TOTAL_OTHER_DEDUCTIONS").HasPrecision(18, 4).IsRequired();

        builder.Property(r => r.CalculatedBy).HasColumnName("CALCULATED_BY").HasMaxLength(100);
        builder.Property(r => r.CalculationDate).HasColumnName("CALCULATION_DATE");
        builder.Property(r => r.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(100);
        builder.Property(r => r.ApprovalDate).HasColumnName("APPROVAL_DATE");
        builder.Property(r => r.PostedBy).HasColumnName("POSTED_BY").HasMaxLength(100);
        builder.Property(r => r.PostDate).HasColumnName("POST_DATE");
        builder.Property(r => r.PaidBy).HasColumnName("PAID_BY").HasMaxLength(100);
        builder.Property(r => r.PaymentDate).HasColumnName("PAYMENT_DATE");
        builder.Property(r => r.JournalVoucherId).HasColumnName("JOURNAL_VOUCHER_ID");

        builder.Property(r => r.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(r => r.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(r => r.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasMany(r => r.Lines)
            .WithOne(l => l.PayrollRun)
            .HasForeignKey(l => l.PayrollRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PayrollRunLineConfiguration : IEntityTypeConfiguration<PayrollRunLine>
{
    public void Configure(EntityTypeBuilder<PayrollRunLine> builder)
    {
        builder.ToTable("HR_PAYROLL_RUN_LINE");
        builder.Property(l => l.CurrencyCode).HasColumnName("CURRENCY_CODE").HasMaxLength(30);
        builder.Property(l => l.BeneficiaryName).HasColumnName("BENEFICIARY_NAME").HasMaxLength(400);
        builder.Property(l => l.BeneficiaryNationalId).HasColumnName("BENEFICIARY_NATIONAL_ID").HasMaxLength(100);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(l => l.PayrollRunId).HasColumnName("PAYROLL_RUN_ID").IsRequired();
        builder.Property(l => l.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(l => l.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(l => l.DepartmentCode).HasColumnName("DEPARTMENT_CODE").HasMaxLength(50);
        builder.Property(l => l.CostCenterCode).HasColumnName("COST_CENTER_CODE").HasMaxLength(50);

        builder.Property(l => l.BasicSalary).HasColumnName("BASIC_SALARY").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.TotalEarnings).HasColumnName("TOTAL_EARNINGS").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.GrossSalary).HasColumnName("GROSS_SALARY").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.SscEligibleSalary).HasColumnName("SSC_ELIGIBLE_SALARY").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.SscEmployeeContrib).HasColumnName("SSC_EMPLOYEE_CONTRIB").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.SscEmployerContrib).HasColumnName("SSC_EMPLOYER_CONTRIB").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.TaxableGross).HasColumnName("TAXABLE_GROSS").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.AnnualExemptions).HasColumnName("ANNUAL_EXEMPTIONS").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.AnnualTaxableNet).HasColumnName("ANNUAL_TAXABLE_NET").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.IncomeTaxWithheld).HasColumnName("INCOME_TAX_WITHHELD").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.NationalContribWithheld).HasColumnName("NATIONAL_CONTRIB_WITHHELD").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.OtherDeductions).HasColumnName("OTHER_DEDUCTIONS").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.TotalDeductions).HasColumnName("TOTAL_DEDUCTIONS").HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.NetPay).HasColumnName("NET_PAY").HasPrecision(18, 4).IsRequired();

        builder.Property(l => l.PaymentMethod).HasColumnName("PAYMENT_METHOD").HasMaxLength(30).IsRequired();
        builder.Property(l => l.BankCode).HasColumnName("BANK_CODE").HasMaxLength(50);
        builder.Property(l => l.Iban).HasColumnName("IBAN").HasMaxLength(50);
        builder.Property(l => l.Status).HasColumnName("STATUS").HasMaxLength(30).IsRequired();

        builder.Property(l => l.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(l => l.CreationDate).HasColumnName("CREATION_DATE").IsRequired();

        builder.HasOne(l => l.Employee)
            .WithMany()
            .HasForeignKey(l => l.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Components)
            .WithOne(c => c.PayrollRunLine)
            .HasForeignKey(c => c.PayrollLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Snapshot)
            .WithOne(s => s.PayrollRunLine)
            .HasForeignKey<PayrollCalculationSnapshot>(s => s.PayrollRunLineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PayrollRunLineComponentConfiguration : IEntityTypeConfiguration<PayrollRunLineComponent>
{
    public void Configure(EntityTypeBuilder<PayrollRunLineComponent> builder)
    {
        builder.ToTable("HR_PAYROLL_RUN_LINE_COMPONENT");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(c => c.PayrollLineId).HasColumnName("PAYROLL_LINE_ID").IsRequired();
        builder.Property(c => c.ComponentCode).HasColumnName("COMPONENT_CODE").HasMaxLength(50).IsRequired();
        builder.Property(c => c.ComponentNameLocal).HasColumnName("COMPONENT_NAME_LOCAL").HasMaxLength(200).IsRequired();
        builder.Property(c => c.ComponentNameEn).HasColumnName("COMPONENT_NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(c => c.ComponentType).HasColumnName("COMPONENT_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(c => c.Amount).HasColumnName("AMOUNT").HasPrecision(18, 3).IsRequired();
    }
}

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
        builder.Property(s => s.CalculationTimestamp).HasColumnName("CALCULATION_TIMESTAMP").IsRequired();

        builder.Property(s => s.TotalBaseDays).HasColumnName("TOTAL_BASE_DAYS").HasPrecision(5, 2).IsRequired();
        builder.Property(s => s.EligibleDays).HasColumnName("ELIGIBLE_DAYS").HasPrecision(5, 2).IsRequired();
        builder.Property(s => s.ProrationFactor).HasColumnName("PRORATION_FACTOR").HasPrecision(10, 6).IsRequired();
        builder.Property(s => s.ProrationPolicyCode).HasColumnName("PRORATION_POLICY_CODE").HasMaxLength(50).IsRequired();
        builder.Property(s => s.ProrationMethodUsed).HasColumnName("PRORATION_METHOD_USED").HasMaxLength(50).IsRequired();

        builder.Property(s => s.OvertimeHoursApplied).HasColumnName("OVERTIME_HOURS_APPLIED").HasPrecision(5, 2).IsRequired();
        builder.Property(s => s.OvertimeEarningsApplied).HasColumnName("OVERTIME_EARNINGS_APPLIED").HasPrecision(18, 4).IsRequired();

        builder.Property(s => s.SscPolicyCode).HasColumnName("SSC_POLICY_CODE").HasMaxLength(50).IsRequired();
        builder.Property(s => s.SscEmpRateApplied).HasColumnName("SSC_EMP_RATE_APPLIED").HasPrecision(10, 6).IsRequired();
        builder.Property(s => s.SscEmprRateApplied).HasColumnName("SSC_EMPR_RATE_APPLIED").HasPrecision(10, 6).IsRequired();
        builder.Property(s => s.SscCapApplied).HasColumnName("SSC_CAP_APPLIED").HasPrecision(18, 4).IsRequired();

        builder.Property(s => s.TaxPolicyCode).HasColumnName("TAX_POLICY_CODE").HasMaxLength(50).IsRequired();
        builder.Property(s => s.TaxExemptionsApplied).HasColumnName("TAX_EXEMPTIONS_APPLIED").HasPrecision(18, 4).IsRequired();

        builder.Property(s => s.LoanDeductionsApplied).HasColumnName("LOAN_DEDUCTIONS_APPLIED").HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.LoanDeductionsCarriedFwd).HasColumnName("LOAN_DEDUCTIONS_CARRIED_FWD").HasPrecision(18, 4).IsRequired();

        builder.Property(s => s.ExplanationJson).HasColumnName("EXPLANATION_JSON").IsRequired();
    }
}
