using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.ToTable("HR_PAYROLL_RUN");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(r => r.BranchId)
            .HasColumnName("BRANCH_ID");

        builder.Property(r => r.PayPeriod)
            .HasColumnName("PAY_PERIOD")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.RunDate)
            .HasColumnName("RUN_DATE")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("DRAFT")
            .IsRequired();

        builder.Property(r => r.TotalGrossSalary)
            .HasColumnName("TOTAL_GROSS_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m);

        builder.Property(r => r.TotalNetSalary)
            .HasColumnName("TOTAL_NET_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m);

        builder.Property(r => r.TotalEmployeeSsc)
            .HasColumnName("TOTAL_EMPLOYEE_SSC")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m);

        builder.Property(r => r.TotalEmployerSsc)
            .HasColumnName("TOTAL_EMPLOYER_SSC")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m);

        builder.Property(r => r.TotalIncomeTax)
            .HasColumnName("TOTAL_INCOME_TAX")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m);

        builder.Property(r => r.TotalNationalContribution)
            .HasColumnName("TOTAL_NATIONAL_CONTRIB")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m);

        builder.Property(r => r.TotalOtherDeductions)
            .HasColumnName("TOTAL_OTHER_DEDUCTIONS")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m);

        builder.Property(r => r.JournalVoucherId)
            .HasColumnName("JOURNAL_VOUCHER_ID");

        builder.Property(r => r.CalculatedBy)
            .HasColumnName("CALCULATED_BY")
            .HasMaxLength(100);

        builder.Property(r => r.CalculationDate)
            .HasColumnName("CALCULATION_DATE");

        builder.Property(r => r.ApprovedBy)
            .HasColumnName("APPROVED_BY")
            .HasMaxLength(100);

        builder.Property(r => r.ApprovalDate)
            .HasColumnName("APPROVAL_DATE");

        builder.Property(r => r.PostedBy)
            .HasColumnName("POSTED_BY")
            .HasMaxLength(100);

        builder.Property(r => r.PostDate)
            .HasColumnName("POST_DATE");

        builder.Property(r => r.PaidBy)
            .HasColumnName("PAID_BY")
            .HasMaxLength(100);

        builder.Property(r => r.PaymentDate)
            .HasColumnName("PAYMENT_DATE");

        builder.Property(r => r.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(r => r.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(r => r.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(r => new { r.PayPeriod, r.BranchId })
            .HasDatabaseName("IX_HR_PAYROLL_RUN_PERIOD");
    }
}
