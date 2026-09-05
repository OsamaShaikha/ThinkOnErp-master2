using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeSalaryStructureConfiguration : IEntityTypeConfiguration<EmployeeSalaryStructure>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryStructure> builder)
    {
        builder.ToTable("HR_EMP_SALARY_STRUCTURE");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.EffectiveFrom)
            .HasColumnName("EFFECTIVE_FROM")
            .IsRequired();

        builder.Property(s => s.EffectiveTo)
            .HasColumnName("EFFECTIVE_TO");

        builder.Property(s => s.BasicSalary)
            .HasColumnName("BASIC_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(s => s.CurrencyCode)
            .HasColumnName("CURRENCY_CODE")
            .HasMaxLength(10)
            .HasDefaultValue("JOD")
            .IsRequired();

        builder.Property(s => s.PaymentMethod)
            .HasColumnName("PAYMENT_METHOD")
            .HasMaxLength(30)
            .HasDefaultValue("BANK_TRANSFER")
            .IsRequired();

        builder.Property(s => s.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(s => s.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(s => s.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(s => s.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(s => new { s.EmployeeCode, s.EffectiveFrom })
            .HasDatabaseName("IX_HR_SAL_STRUCT_EMP_EFF");

        builder.HasOne(s => s.Employee)
            .WithMany()
            .HasForeignKey(s => s.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
