using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("HR_ATTENDANCE_RECORD");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.AttendanceDate)
            .HasColumnName("ATTENDANCE_DATE")
            .IsRequired();

        builder.Property(a => a.ClockIn)
            .HasColumnName("CLOCK_IN");

        builder.Property(a => a.ClockOut)
            .HasColumnName("CLOCK_OUT");

        builder.Property(a => a.Source)
            .HasColumnName("SOURCE")
            .HasMaxLength(30)
            .HasDefaultValue("WEB")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("ON_TIME")
            .IsRequired();

        builder.Property(a => a.LateMinutes)
            .HasColumnName("LATE_MINUTES")
            .HasDefaultValue(0);

        builder.Property(a => a.EarlyLeaveMinutes)
            .HasColumnName("EARLY_LEAVE_MINUTES")
            .HasDefaultValue(0);

        builder.Property(a => a.TotalWorkHours)
            .HasColumnName("TOTAL_WORK_HOURS")
            .HasColumnType("NUMBER(8,2)")
            .HasDefaultValue(0);

        builder.Property(a => a.CorrectedBy)
            .HasColumnName("CORRECTED_BY")
            .HasMaxLength(100);

        builder.Property(a => a.CorrectionReason)
            .HasColumnName("CORRECTION_REASON")
            .HasMaxLength(500);

        builder.Property(a => a.IdempotencyKey)
            .HasColumnName("IDEMPOTENCY_KEY")
            .HasMaxLength(100);

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

        builder.HasIndex(a => new { a.EmployeeCode, a.AttendanceDate })
            .HasDatabaseName("IX_HR_ATT_EMP_DATE");

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
