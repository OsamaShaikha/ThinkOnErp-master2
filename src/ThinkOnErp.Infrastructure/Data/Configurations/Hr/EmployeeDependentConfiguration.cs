using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeDependentConfiguration : IEntityTypeConfiguration<EmployeeDependent>
{
    public void Configure(EntityTypeBuilder<EmployeeDependent> builder)
    {
        builder.ToTable("HR_EMPLOYEE_DEPENDENT");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(d => d.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.Relationship)
            .HasColumnName("RELATIONSHIP")
            .HasMaxLength(30)
            .HasDefaultValue("CHILD")
            .IsRequired();

        builder.Property(d => d.NationalId)
            .HasColumnName("NATIONAL_ID")
            .HasMaxLength(50);

        builder.Property(d => d.DateOfBirth)
            .HasColumnName("DATE_OF_BIRTH")
            .IsRequired();

        builder.Property(d => d.Gender)
            .HasColumnName("GENDER")
            .HasMaxLength(10)
            .HasDefaultValue("MALE")
            .IsRequired();

        builder.Property(d => d.IsTaxExemptionClaimed)
            .HasColumnName("IS_TAX_EXEMPTION_CLAIMED")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(d => d.IsMedicalCovered)
            .HasColumnName("IS_MEDICAL_COVERED")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(d => d.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(d => d.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(d => d.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(d => d.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // Relationships
        builder.HasOne(d => d.Employee)
            .WithMany(e => e.Dependents)
            .HasForeignKey(d => d.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
