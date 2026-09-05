using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("HR_EMPLOYEE");

        builder.HasKey(e => e.EmployeeCode);

        builder.Property(e => e.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.NationalId)
            .HasColumnName("NATIONAL_ID")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.PassportNumber)
            .HasColumnName("PASSPORT_NUMBER")
            .HasMaxLength(50);

        builder.Property(e => e.Nationality)
            .HasColumnName("NATIONALITY")
            .HasMaxLength(100)
            .HasDefaultValue("Jordanian")
            .IsRequired();

        builder.Property(e => e.DateOfBirth)
            .HasColumnName("DATE_OF_BIRTH")
            .IsRequired();

        builder.Property(e => e.Gender)
            .HasColumnName("GENDER")
            .HasMaxLength(10)
            .HasDefaultValue("MALE")
            .IsRequired();

        builder.Property(e => e.MaritalStatus)
            .HasColumnName("MARITAL_STATUS")
            .HasMaxLength(20)
            .HasDefaultValue("SINGLE")
            .IsRequired();

        builder.Property(e => e.HireDate)
            .HasColumnName("HIRE_DATE")
            .IsRequired();

        builder.Property(e => e.PositionCode)
            .HasColumnName("POSITION_CODE")
            .HasMaxLength(50);

        builder.Property(e => e.DepartmentCode)
            .HasColumnName("DEPARTMENT_CODE")
            .HasMaxLength(50);

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID");

        builder.Property(e => e.EmploymentType)
            .HasColumnName("EMPLOYMENT_TYPE")
            .HasMaxLength(30)
            .HasDefaultValue("FULL_TIME")
            .IsRequired();

        builder.Property(e => e.EmploymentStatus)
            .HasColumnName("EMPLOYMENT_STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("ACTIVE")
            .IsRequired();

        builder.Property(e => e.SscNumber)
            .HasColumnName("SSC_NUMBER")
            .HasMaxLength(50);

        builder.Property(e => e.IsHighRiskRole)
            .HasColumnName("IS_HIGH_RISK_ROLE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.TaxExemptionCount)
            .HasColumnName("TAX_EXEMPTION_COUNT")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.BankAccountNumber)
            .HasColumnName("BANK_ACCOUNT_NUMBER")
            .HasMaxLength(50);

        builder.Property(e => e.BankName)
            .HasColumnName("BANK_NAME")
            .HasMaxLength(100);

        builder.Property(e => e.BankIban)
            .HasColumnName("BANK_IBAN")
            .HasMaxLength(50);

        builder.Property(e => e.Email)
            .HasColumnName("EMAIL")
            .HasMaxLength(100);

        builder.Property(e => e.Phone)
            .HasColumnName("PHONE")
            .HasMaxLength(50);

        builder.Property(e => e.ProbationEndDate)
            .HasColumnName("PROBATION_END_DATE");

        builder.Property(e => e.TerminationDate)
            .HasColumnName("TERMINATION_DATE");

        builder.Property(e => e.TerminationReason)
            .HasColumnName("TERMINATION_REASON")
            .HasMaxLength(500);

        builder.Property(e => e.ManagerEmployeeCode)
            .HasColumnName("MANAGER_EMPLOYEE_CODE")
            .HasMaxLength(50);

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(e => e.NationalId)
            .IsUnique()
            .HasDatabaseName("IX_HR_EMP_NATIONAL_ID");

        // Relationships
        builder.HasOne(e => e.Position)
            .WithMany(p => p.Employees)
            .HasForeignKey(e => e.PositionCode)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentCode)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Manager)
            .WithMany(m => m.DirectReports)
            .HasForeignKey(e => e.ManagerEmployeeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
