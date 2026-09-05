using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmploymentEventConfiguration : IEntityTypeConfiguration<EmploymentEvent>
{
    public void Configure(EntityTypeBuilder<EmploymentEvent> builder)
    {
        builder.ToTable("HR_EMPLOYMENT_EVENT");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("EVENT_TYPE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EffectiveDate)
            .HasColumnName("EFFECTIVE_DATE")
            .IsRequired();

        builder.Property(e => e.FromValue)
            .HasColumnName("FROM_VALUE")
            .HasMaxLength(500);

        builder.Property(e => e.ToValue)
            .HasColumnName("TO_VALUE")
            .HasMaxLength(500);

        builder.Property(e => e.Reason)
            .HasColumnName("REASON")
            .HasMaxLength(500);

        builder.Property(e => e.ApprovedBy)
            .HasColumnName("APPROVED_BY")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.HasIndex(e => new { e.EmployeeCode, e.EffectiveDate, e.EventType })
            .HasDatabaseName("IX_HR_EMP_EVENT_HIST");

        // Relationships
        builder.HasOne(e => e.Employee)
            .WithMany(emp => emp.EmploymentEvents)
            .HasForeignKey(e => e.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
