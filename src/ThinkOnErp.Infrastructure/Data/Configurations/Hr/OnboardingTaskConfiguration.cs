using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class OnboardingTaskConfiguration : IEntityTypeConfiguration<OnboardingTask>
{
    public void Configure(EntityTypeBuilder<OnboardingTask> builder)
    {
        builder.ToTable("HR_ONBOARDING_TASK");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(t => t.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.TaskName)
            .HasColumnName("TASK_NAME")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.AssignedTo)
            .HasColumnName("ASSIGNED_TO")
            .HasMaxLength(100);

        builder.Property(t => t.DueDate)
            .HasColumnName("DUE_DATE");

        builder.Property(t => t.IsCompleted)
            .HasColumnName("IS_COMPLETED")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.CompletedDate)
            .HasColumnName("COMPLETED_DATE");

        builder.Property(t => t.CompletedBy)
            .HasColumnName("COMPLETED_BY")
            .HasMaxLength(100);

        builder.Property(t => t.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(500);

        builder.Property(t => t.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.HasOne(t => t.Employee)
            .WithMany()
            .HasForeignKey(t => t.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
