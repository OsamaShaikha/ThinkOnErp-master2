using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmploymentContractConfiguration : IEntityTypeConfiguration<EmploymentContract>
{
    public void Configure(EntityTypeBuilder<EmploymentContract> builder)
    {
        builder.ToTable("HR_EMPLOYMENT_CONTRACT");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ContractType)
            .HasColumnName("CONTRACT_TYPE")
            .HasMaxLength(30)
            .HasDefaultValue("UNLIMITED")
            .IsRequired();

        builder.Property(c => c.StartDate)
            .HasColumnName("START_DATE")
            .IsRequired();

        builder.Property(c => c.EndDate)
            .HasColumnName("END_DATE");

        builder.Property(c => c.FileReference)
            .HasColumnName("FILE_REFERENCE")
            .HasMaxLength(500);

        builder.Property(c => c.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("ACTIVE")
            .IsRequired();

        builder.Property(c => c.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(500);

        builder.Property(c => c.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(c => c.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(c => c.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(c => c.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasOne(c => c.Employee)
            .WithMany()
            .HasForeignKey(c => c.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
