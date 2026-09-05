using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
{
    public void Configure(EntityTypeBuilder<AttendanceDay> builder)
    {
        builder.ToTable("HR_ATTENDANCE_DAY");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(d => d.AttendanceDate).HasColumnName("ATTENDANCE_DATE").IsRequired();
        builder.Property(d => d.ShiftCode).HasColumnName("SHIFT_CODE").HasMaxLength(50);
        builder.Property(d => d.WorkCalendarId).HasColumnName("WORK_CALENDAR_ID");
        builder.Property(d => d.FirstCheckIn).HasColumnName("FIRST_CHECK_IN");
        builder.Property(d => d.LastCheckOut).HasColumnName("LAST_CHECK_OUT");
        builder.Property(d => d.ScheduledHours).HasColumnName("SCHEDULED_HOURS").HasColumnType("NUMBER(5,2)").HasDefaultValue(0);
        builder.Property(d => d.ActualWorkedHours).HasColumnName("ACTUAL_WORKED_HOURS").HasColumnType("NUMBER(5,2)").HasDefaultValue(0);
        builder.Property(d => d.LateArrivalMinutes).HasColumnName("LATE_ARRIVAL_MINUTES").HasDefaultValue(0);
        builder.Property(d => d.EarlyLeaveMinutes).HasColumnName("EARLY_LEAVE_MINUTES").HasDefaultValue(0);
        builder.Property(d => d.OvertimeHours).HasColumnName("OVERTIME_HOURS").HasColumnType("NUMBER(5,2)").HasDefaultValue(0);
        builder.Property(d => d.HasMissingPunch).HasColumnName("HAS_MISSING_PUNCH").HasDefaultValue(false);
        builder.Property(d => d.Status).HasColumnName("STATUS").HasMaxLength(50).HasDefaultValue("PRESENT").IsRequired();
        builder.Property(d => d.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(50);
        builder.Property(d => d.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(d => d.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(d => d.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(d => d.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(d => d.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(d => new { d.EmployeeCode, d.AttendanceDate }).IsUnique();
        builder.HasOne(d => d.Employee).WithMany().HasPrincipalKey(e => e.EmployeeCode).HasForeignKey(d => d.EmployeeCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.ShiftSchedule).WithMany().HasPrincipalKey(s => s.ShiftCode).HasForeignKey(d => d.ShiftCode).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(d => d.WorkCalendar).WithMany().HasForeignKey(d => d.WorkCalendarId).OnDelete(DeleteBehavior.SetNull);

    }
}
