using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeShiftAssignmentConfiguration : IEntityTypeConfiguration<EmployeeShiftAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftAssignment> builder)
    {
        builder.ToTable("HR_EMP_SHIFT_ASSIGNMENT");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.ShiftCode)
            .HasColumnName("SHIFT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.EffectiveFrom)
            .HasColumnName("EFFECTIVE_FROM")
            .IsRequired();

        builder.Property(a => a.EffectiveTo)
            .HasColumnName("EFFECTIVE_TO");

        builder.Property(a => a.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(a => a.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(a => a.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(a => a.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.ShiftSchedule)
            .WithMany(s => s.ShiftAssignments)
            .HasForeignKey(a => a.ShiftCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
