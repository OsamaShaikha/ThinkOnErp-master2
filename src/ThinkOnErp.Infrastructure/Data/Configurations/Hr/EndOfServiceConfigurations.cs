using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EndOfServiceSettlementConfiguration : IEntityTypeConfiguration<EndOfServiceSettlement>
{
    public void Configure(EntityTypeBuilder<EndOfServiceSettlement> builder)
    {
        builder.ToTable("HR_END_OF_SERVICE_SETTLEMENT", table => table.ExcludeFromMigrations());
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(s => s.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(s => s.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(s => s.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();

        builder.Property(s => s.TerminationType).HasColumnName("TERMINATION_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(s => s.TerminationReason).HasColumnName("TERMINATION_REASON").HasMaxLength(1000);
        builder.Property(s => s.NoticeDate).HasColumnName("NOTICE_DATE");
        builder.Property(s => s.LastWorkingDate).HasColumnName("LAST_WORKING_DATE").IsRequired();
        builder.Property(s => s.SettlementDate).HasColumnName("SETTLEMENT_DATE").IsRequired();

        builder.Property(s => s.HireDate).HasColumnName("HIRE_DATE").IsRequired();
        builder.Property(s => s.ServiceYears).HasColumnName("SERVICE_YEARS").HasPrecision(10, 4);
        builder.Property(s => s.ServiceMonths).HasColumnName("SERVICE_MONTHS");
        builder.Property(s => s.ServiceDays).HasColumnName("SERVICE_DAYS");

        builder.Property(s => s.BaseSalarySnapshot).HasColumnName("BASE_SALARY_SNAPSHOT").HasPrecision(18, 3);
        builder.Property(s => s.GrossSalarySnapshot).HasColumnName("GROSS_SALARY_SNAPSHOT").HasPrecision(18, 3);
        builder.Property(s => s.CurrencyCode).HasColumnName("CURRENCY_CODE").HasMaxLength(20).IsRequired();

        builder.Property(s => s.SeverancePay).HasColumnName("SEVERANCE_PAY").HasPrecision(18, 3);
        builder.Property(s => s.UnpaidSalary).HasColumnName("UNPAID_SALARY").HasPrecision(18, 3);
        builder.Property(s => s.LeaveEncashment).HasColumnName("LEAVE_ENCASHMENT").HasPrecision(18, 3);
        builder.Property(s => s.UnusedLeaveDays).HasColumnName("UNUSED_LEAVE_DAYS").HasPrecision(10, 4);
        builder.Property(s => s.NoticePeriodPay).HasColumnName("NOTICE_PERIOD_PAY").HasPrecision(18, 3);
        builder.Property(s => s.OtherEntitlements).HasColumnName("OTHER_ENTITLEMENTS").HasPrecision(18, 3);
        builder.Property(s => s.TotalEntitlements).HasColumnName("TOTAL_ENTITLEMENTS").HasPrecision(18, 3);

        builder.Property(s => s.OutstandingLoans).HasColumnName("OUTSTANDING_LOANS").HasPrecision(18, 3);
        builder.Property(s => s.OutstandingAdvances).HasColumnName("OUTSTANDING_ADVANCES").HasPrecision(18, 3);
        builder.Property(s => s.NegativeLeaveDeduction).HasColumnName("NEGATIVE_LEAVE_DEDUCTION").HasPrecision(18, 3);
        builder.Property(s => s.OtherDeductions).HasColumnName("OTHER_DEDUCTIONS").HasPrecision(18, 3);
        builder.Property(s => s.TotalDeductions).HasColumnName("TOTAL_DEDUCTIONS").HasPrecision(18, 3);

        builder.Property(s => s.NetSettlementAmount).HasColumnName("NET_SETTLEMENT_AMOUNT").HasPrecision(18, 3);
        builder.Property(s => s.Status).HasColumnName("STATUS").HasMaxLength(50).IsRequired();
        builder.Property(s => s.CalculationNotes).HasColumnName("CALCULATION_NOTES").HasMaxLength(2000);

        builder.Property(s => s.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(200);
        builder.Property(s => s.ApprovedAtUtc).HasColumnName("APPROVED_AT_UTC");
        builder.Property(s => s.CancelledBy).HasColumnName("CANCELLED_BY").HasMaxLength(200);
        builder.Property(s => s.CancelledAtUtc).HasColumnName("CANCELLED_AT_UTC");
        builder.Property(s => s.CancellationReason).HasColumnName("CANCELLATION_REASON").HasMaxLength(1000);

        builder.Property(s => s.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(s => s.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(s => s.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(s => s.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasMany(s => s.Lines)
            .WithOne(l => l.Settlement)
            .HasForeignKey(l => l.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Employee)
            .WithMany()
            .HasForeignKey(s => s.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_EOS_EMPLOYEE");

        builder.HasIndex(s => new { s.CompanyId, s.EmployeeCode, s.Status }).HasDatabaseName("IX_HR_EOS_EMP_STATUS");
    }
}

public sealed class EndOfServiceSettlementLineConfiguration : IEntityTypeConfiguration<EndOfServiceSettlementLine>
{
    public void Configure(EntityTypeBuilder<EndOfServiceSettlementLine> builder)
    {
        builder.ToTable("HR_EOS_SETTLEMENT_LINE", table => table.ExcludeFromMigrations());
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(l => l.SettlementId).HasColumnName("SETTLEMENT_ID").IsRequired();
        builder.Property(l => l.ComponentCode).HasColumnName("COMPONENT_CODE").HasMaxLength(50).IsRequired();
        builder.Property(l => l.ComponentType).HasColumnName("COMPONENT_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(l => l.Amount).HasColumnName("AMOUNT").HasPrecision(18, 3).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);

        builder.HasIndex(l => l.SettlementId).HasDatabaseName("IX_HR_EOS_LINE_SETTLE");
    }
}

public sealed class EndOfServicePolicyConfiguration : IEntityTypeConfiguration<EndOfServicePolicy>
{
    public void Configure(EntityTypeBuilder<EndOfServicePolicy> builder)
    {
        builder.ToTable("HR_END_OF_SERVICE_POLICY", table => table.ExcludeFromMigrations());
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(p => p.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(p => p.EffectiveFrom).HasColumnName("EFFECTIVE_FROM");
        builder.Property(p => p.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(p => p.PolicyName).HasColumnName("POLICY_NAME").HasMaxLength(200).IsRequired();
        builder.Property(p => p.CalculationBasis).HasColumnName("CALCULATION_BASIS").HasMaxLength(50).IsRequired();
        builder.Property(p => p.LeaveDailyRateDivisor).HasColumnName("LEAVE_DAILY_RATE_DIVISOR").HasPrecision(8, 2);
        builder.Property(p => p.NoticePeriodDays).HasColumnName("NOTICE_PERIOD_DAYS");
        builder.Property(p => p.ProrateFinalMonthByWorkingDays).HasColumnName("PRORATE_BY_WORKING_DAYS");

        builder.Property(p => p.ResignationFactorUnderTwoYears).HasColumnName("RESIGN_FACTOR_UNDER_2Y").HasPrecision(6, 4);
        builder.Property(p => p.ResignationFactorTwoToFiveYears).HasColumnName("RESIGN_FACTOR_2_TO_5Y").HasPrecision(6, 4);
        builder.Property(p => p.ResignationFactorFiveToTenYears).HasColumnName("RESIGN_FACTOR_5_TO_10Y").HasPrecision(6, 4);
        builder.Property(p => p.ResignationFactorTenYearsAndAbove).HasColumnName("RESIGN_FACTOR_10Y_PLUS").HasPrecision(6, 4);

        builder.Property(p => p.FirstFiveYearsRatePerYear).HasColumnName("FIRST_5Y_RATE_PER_YEAR").HasPrecision(6, 4);
        builder.Property(p => p.SubsequentYearsRatePerYear).HasColumnName("SUBSEQUENT_RATE_PER_YEAR").HasPrecision(6, 4);

        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").IsRequired();
        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(p => new { p.CompanyId, p.IsActive }).HasDatabaseName("IX_HR_EOS_POL_COMP");
    }
}

