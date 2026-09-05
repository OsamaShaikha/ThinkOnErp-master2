using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class SalaryRevisionConfiguration : IEntityTypeConfiguration<SalaryRevision>
{
    public void Configure(EntityTypeBuilder<SalaryRevision> builder)
    {
        builder.ToTable("HR_SALARY_REVISION");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(r => r.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.EffectiveDate)
            .HasColumnName("EFFECTIVE_DATE")
            .IsRequired();

        builder.Property(r => r.OldBasicSalary)
            .HasColumnName("OLD_BASIC_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(r => r.NewBasicSalary)
            .HasColumnName("NEW_BASIC_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(r => r.OldGrossSalary)
            .HasColumnName("OLD_GROSS_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(r => r.NewGrossSalary)
            .HasColumnName("NEW_GROSS_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasColumnName("REASON")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.ApprovedBy)
            .HasColumnName("APPROVED_BY")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.HasIndex(r => new { r.EmployeeCode, r.EffectiveDate })
            .HasDatabaseName("IX_HR_SAL_REV_EMP_DATE");

        builder.HasOne(r => r.Employee)
            .WithMany()
            .HasForeignKey(r => r.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
