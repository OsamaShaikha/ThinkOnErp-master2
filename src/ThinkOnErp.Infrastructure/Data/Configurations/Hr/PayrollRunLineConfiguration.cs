using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class PayrollRunLineConfiguration : IEntityTypeConfiguration<PayrollRunLine>
{
    public void Configure(EntityTypeBuilder<PayrollRunLine> builder)
    {
        builder.ToTable("HR_PAYROLL_RUN_LINE");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(l => l.PayrollRunId)
            .HasColumnName("PAYROLL_RUN_ID")
            .IsRequired();

        builder.Property(l => l.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.DepartmentCode)
            .HasColumnName("DEPARTMENT_CODE")
            .HasMaxLength(50);

        builder.Property(l => l.CostCenterCode)
            .HasColumnName("COST_CENTER_CODE")
            .HasMaxLength(50);

        builder.Property(l => l.BranchId)
            .HasColumnName("BRANCH_ID");

        builder.Property(l => l.BasicSalary)
            .HasColumnName("BASIC_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.TotalEarnings)
            .HasColumnName("TOTAL_EARNINGS")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.GrossSalary)
            .HasColumnName("GROSS_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.SscEligibleSalary)
            .HasColumnName("SSC_ELIGIBLE_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.SscEmployeeContribution)
            .HasColumnName("SSC_EMPLOYEE_CONTRIB")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.SscEmployerContribution)
            .HasColumnName("SSC_EMPLOYER_CONTRIB")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.TaxableGross)
            .HasColumnName("TAXABLE_GROSS")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.AnnualExemptions)
            .HasColumnName("ANNUAL_EXEMPTIONS")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.AnnualTaxableNet)
            .HasColumnName("ANNUAL_TAXABLE_NET")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.IncomeTaxWithheld)
            .HasColumnName("INCOME_TAX_WITHHELD")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.NationalContributionWithheld)
            .HasColumnName("NATIONAL_CONTRIB_WITHHELD")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.OtherDeductions)
            .HasColumnName("OTHER_DEDUCTIONS")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.TotalDeductions)
            .HasColumnName("TOTAL_DEDUCTIONS")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.NetPay)
            .HasColumnName("NET_PAY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.PaymentMethod)
            .HasColumnName("PAYMENT_METHOD")
            .HasMaxLength(30)
            .HasDefaultValue("BANK_TRANSFER")
            .IsRequired();

        builder.Property(l => l.BankCode)
            .HasColumnName("BANK_CODE")
            .HasMaxLength(50);

        builder.Property(l => l.Iban)
            .HasColumnName("IBAN")
            .HasMaxLength(50);

        builder.Property(l => l.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("CALCULATED")
            .IsRequired();

        builder.Property(l => l.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.HasIndex(l => new { l.PayrollRunId, l.EmployeeCode })
            .IsUnique()
            .HasDatabaseName("IX_HR_PAYROLL_LINE_EMP");

        builder.HasOne(l => l.PayrollRun)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.PayrollRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Employee)
            .WithMany()
            .HasForeignKey(l => l.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
