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
        builder.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100);

        builder.Property(e => e.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(400).IsRequired();
        builder.Property(e => e.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(e => e.NationalId).HasColumnName("NATIONAL_ID").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Nationality).HasColumnName("NATIONALITY").HasMaxLength(200).IsRequired();
        builder.Property(e => e.PassportNumber).HasColumnName("PASSPORT_NUMBER").HasMaxLength(100);
        builder.Property(e => e.DateOfBirth).HasColumnName("DATE_OF_BIRTH").IsRequired();
        builder.Property(e => e.Gender).HasColumnName("GENDER").HasMaxLength(20).IsRequired();
        builder.Property(e => e.MaritalStatus).HasColumnName("MARITAL_STATUS").HasMaxLength(40).IsRequired();
        builder.Property(e => e.Email).HasColumnName("EMAIL").HasMaxLength(200);
        builder.Property(e => e.Phone).HasColumnName("PHONE").HasMaxLength(100);
        builder.Property(e => e.HireDate).HasColumnName("HIRE_DATE").IsRequired();
        builder.Property(e => e.ProbationEndDate).HasColumnName("PROBATION_END_DATE");
        builder.Property(e => e.TerminationDate).HasColumnName("TERMINATION_DATE");
        builder.Property(e => e.TerminationReason).HasColumnName("TERMINATION_REASON").HasMaxLength(1000);
        builder.Property(e => e.EmploymentType).HasColumnName("EMPLOYMENT_TYPE").HasMaxLength(60).IsRequired();
        builder.Property(e => e.EmploymentStatus).HasColumnName("EMPLOYMENT_STATUS").HasMaxLength(60).IsRequired();
        builder.Property(e => e.DepartmentCode).HasColumnName("DEPARTMENT_CODE").HasMaxLength(100);
        builder.Property(e => e.PositionCode).HasColumnName("POSITION_CODE").HasMaxLength(100);
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(e => e.ManagerEmployeeCode).HasColumnName("MANAGER_EMPLOYEE_CODE").HasMaxLength(100);
        builder.Property(e => e.SscNumber).HasColumnName("SSC_NUMBER").HasMaxLength(100);
        builder.Property(e => e.TaxExemptionCount).HasColumnName("TAX_EXEMPTION_COUNT").IsRequired();
        builder.Property(e => e.IsHighRiskRole).HasColumnName("IS_HIGH_RISK_ROLE").IsRequired();
        builder.Property(e => e.BankName).HasColumnName("BANK_NAME").HasMaxLength(200);
        builder.Property(e => e.BankAccountNumber).HasColumnName("BANK_ACCOUNT_NUMBER").HasMaxLength(100);
        builder.Property(e => e.BankIban).HasColumnName("BANK_IBAN").HasMaxLength(100);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasMany(e => e.Dependents)
            .WithOne(d => d.Employee)
            .HasForeignKey(d => d.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.SalaryStructures)
            .WithOne(s => s.Employee)
            .HasForeignKey(s => s.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class EmployeeDependentConfiguration : IEntityTypeConfiguration<EmployeeDependent>
{
    public void Configure(EntityTypeBuilder<EmployeeDependent> builder)
    {
        builder.ToTable("HR_EMPLOYEE_DEPENDENT");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(d => d.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(d => d.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(400).IsRequired();
        builder.Property(d => d.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(d => d.Relationship).HasColumnName("RELATIONSHIP").HasMaxLength(60).IsRequired();
        builder.Property(d => d.DateOfBirth).HasColumnName("DATE_OF_BIRTH").IsRequired();
        builder.Property(d => d.Gender).HasColumnName("GENDER").HasMaxLength(20).IsRequired();
        builder.Property(d => d.NationalId).HasColumnName("NATIONAL_ID").HasMaxLength(100);
        builder.Property(d => d.IsTaxExemptionClaimed).HasColumnName("IS_TAX_EXEMPTION_CLAIMED").IsRequired();
        builder.Property(d => d.IsMedicalCovered).HasColumnName("IS_MEDICAL_COVERED").IsRequired();
        builder.Property(d => d.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(d => d.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(d => d.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(d => d.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(d => d.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class SalaryComponentConfiguration : IEntityTypeConfiguration<SalaryComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> builder)
    {
        builder.ToTable("HR_SALARY_COMPONENT");
        builder.HasKey(c => c.ComponentCode);
        builder.Property(c => c.ComponentCode).HasColumnName("COMPONENT_CODE").HasMaxLength(100);

        builder.Property(c => c.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(400).IsRequired();
        builder.Property(c => c.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(c => c.ComponentType).HasColumnName("COMPONENT_TYPE").HasMaxLength(60).IsRequired();
        builder.Property(c => c.CalculationType).HasColumnName("CALCULATION_TYPE").HasMaxLength(60).IsRequired();
        builder.Property(c => c.DefaultAmount).HasColumnName("DEFAULT_AMOUNT").HasPrecision(18, 4);
        builder.Property(c => c.DefaultPercent).HasColumnName("DEFAULT_PERCENT").HasPrecision(8, 4);
        builder.Property(c => c.IsTaxable).HasColumnName("IS_TAXABLE").IsRequired();
        builder.Property(c => c.IsSscApplicable).HasColumnName("IS_SSC_APPLICABLE").IsRequired();
        builder.Property(c => c.GlAccountCode).HasColumnName("GL_ACCOUNT_CODE").HasMaxLength(100);
        builder.Property(c => c.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(c => c.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(c => c.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(c => c.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class SalaryStructureConfiguration : IEntityTypeConfiguration<SalaryStructure>
{
    public void Configure(EntityTypeBuilder<SalaryStructure> builder)
    {
        builder.ToTable("HR_SALARY_STRUCTURE");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(s => s.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(s => s.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(s => s.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(s => s.BasicSalary).HasColumnName("BASIC_SALARY").HasPrecision(18, 3).IsRequired();
        builder.Property(s => s.CurrencyCode).HasColumnName("CURRENCY_CODE").HasMaxLength(20).IsRequired();
        builder.Property(s => s.PaymentMethod).HasColumnName("PAYMENT_METHOD").HasMaxLength(100).IsRequired();
        builder.Property(s => s.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(s => s.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(s => s.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(s => s.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(s => s.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasMany(s => s.Lines)
            .WithOne(l => l.Structure)
            .HasForeignKey(l => l.StructureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SalaryStructureLineConfiguration : IEntityTypeConfiguration<SalaryStructureLine>
{
    public void Configure(EntityTypeBuilder<SalaryStructureLine> builder)
    {
        builder.ToTable("HR_SALARY_STRUCTURE_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(l => l.StructureId).HasColumnName("STRUCTURE_ID").IsRequired();
        builder.Property(l => l.ComponentCode).HasColumnName("COMPONENT_CODE").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Amount).HasColumnName("AMOUNT").HasPrecision(18, 3).IsRequired();
        builder.Property(l => l.Percent).HasColumnName("PERCENT").HasPrecision(5, 2);
        builder.Property(l => l.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(l => l.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(l => l.CreationDate).HasColumnName("CREATION_DATE").IsRequired();

        builder.HasOne(l => l.Component)
            .WithMany()
            .HasForeignKey(l => l.ComponentCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
