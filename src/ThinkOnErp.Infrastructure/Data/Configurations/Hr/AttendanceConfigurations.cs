using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class AttendancePolicyConfiguration : IEntityTypeConfiguration<AttendancePolicy>
{
    public void Configure(EntityTypeBuilder<AttendancePolicy> builder)
    {
        builder.ToTable("HR_ATTENDANCE_POLICY");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(p => p.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(p => p.Code).HasColumnName("CODE").HasMaxLength(100).IsRequired();
        builder.Property(p => p.NameAr).HasColumnName("NAME_AR").HasMaxLength(400).IsRequired();
        builder.Property(p => p.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(p => p.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(p => p.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(p => p.GracePeriodMinutes).HasColumnName("GRACE_PERIOD_MINUTES").IsRequired();
        builder.Property(p => p.EarlyLeaveToleranceMinutes).HasColumnName("EARLY_LEAVE_TOLERANCE_MINUTES").IsRequired();
        builder.Property(p => p.MinMinutesForOvertime).HasColumnName("MIN_MINUTES_FOR_OVERTIME").IsRequired();
        builder.Property(p => p.AutoDeductLateArrival).HasColumnName("AUTO_DEDUCT_LATE_ARRIVAL").IsRequired();
        builder.Property(p => p.MissingPunchHandling).HasColumnName("MISSING_PUNCH_HANDLING").HasMaxLength(100).IsRequired();
        builder.Property(p => p.IsDefault).HasColumnName("IS_DEFAULT").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class RawAttendanceConfiguration : IEntityTypeConfiguration<RawAttendance>
{
    public void Configure(EntityTypeBuilder<RawAttendance> builder)
    {
        builder.ToTable("HR_RAW_ATTENDANCE");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(r => r.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(r => r.PunchTime).HasColumnName("PUNCH_TIME").IsRequired();
        builder.Property(r => r.PunchType).HasColumnName("PUNCH_TYPE").HasMaxLength(40).IsRequired();
        builder.Property(r => r.DeviceId).HasColumnName("DEVICE_ID").HasMaxLength(200);
        builder.Property(r => r.ExternalReference).HasColumnName("EXTERNAL_REFERENCE").HasMaxLength(200);
        builder.Property(r => r.Source).HasColumnName("SOURCE").HasMaxLength(100).IsRequired();
        builder.Property(r => r.IsProcessed).HasColumnName("IS_PROCESSED").IsRequired();
        builder.Property(r => r.ProcessedDate).HasColumnName("PROCESSED_DATE");

        builder.Property(r => r.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(r => r.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
    }
}

public sealed class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
{
    public void Configure(EntityTypeBuilder<AttendanceDay> builder)
    {
        builder.ToTable("HR_ATTENDANCE_DAY");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(a => a.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(a => a.AttendanceDate).HasColumnName("ATTENDANCE_DATE").IsRequired();
        builder.Property(a => a.WorkCalendarId).HasColumnName("WORK_CALENDAR_ID");
        builder.Property(a => a.ShiftCode).HasColumnName("SHIFT_CODE").HasMaxLength(100);
        builder.Property(a => a.ScheduledHours).HasColumnName("SCHEDULED_HOURS").HasPrecision(5, 2).IsRequired();
        builder.Property(a => a.FirstCheckIn).HasColumnName("FIRST_CHECK_IN");
        builder.Property(a => a.LastCheckOut).HasColumnName("LAST_CHECK_OUT");
        builder.Property(a => a.ActualWorkedHours).HasColumnName("ACTUAL_WORKED_HOURS").HasPrecision(5, 2).IsRequired();
        builder.Property(a => a.LateArrivalMinutes).HasColumnName("LATE_ARRIVAL_MINUTES").IsRequired();
        builder.Property(a => a.EarlyLeaveMinutes).HasColumnName("EARLY_LEAVE_MINUTES").IsRequired();
        builder.Property(a => a.OvertimeHours).HasColumnName("OVERTIME_HOURS").HasPrecision(5, 2).IsRequired();
        builder.Property(a => a.Status).HasColumnName("STATUS").HasMaxLength(100).IsRequired();
        builder.Property(a => a.HasMissingPunch).HasColumnName("HAS_MISSING_PUNCH").IsRequired();
        builder.Property(a => a.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100);
        builder.Property(a => a.Notes).HasColumnName("NOTES").HasMaxLength(1000);

        builder.Property(a => a.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(a => a.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(a => a.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(a => a.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class AttendanceCorrectionRequestConfiguration : IEntityTypeConfiguration<AttendanceCorrectionRequest>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrectionRequest> builder)
    {
        builder.ToTable("HR_ATTENDANCE_CORRECTION");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(c => c.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(c => c.AttendanceDate).HasColumnName("ATTENDANCE_DATE").IsRequired();
        builder.Property(c => c.OldCheckIn).HasColumnName("OLD_CHECK_IN");
        builder.Property(c => c.OldCheckOut).HasColumnName("OLD_CHECK_OUT");
        builder.Property(c => c.RequestedCheckIn).HasColumnName("REQUESTED_CHECK_IN");
        builder.Property(c => c.RequestedCheckOut).HasColumnName("REQUESTED_CHECK_OUT");
        builder.Property(c => c.Reason).HasColumnName("REASON").HasMaxLength(1000).IsRequired();
        builder.Property(c => c.Status).HasColumnName("STATUS").HasMaxLength(100).IsRequired();
        builder.Property(c => c.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(200);
        builder.Property(c => c.ApprovalDate).HasColumnName("APPROVAL_DATE");
        builder.Property(c => c.RejectionReason).HasColumnName("REJECTION_REASON").HasMaxLength(1000);

        builder.Property(c => c.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(c => c.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(c => c.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class ShiftScheduleConfiguration : IEntityTypeConfiguration<ShiftSchedule>
{
    public void Configure(EntityTypeBuilder<ShiftSchedule> builder)
    {
        builder.ToTable("HR_SHIFT_SCHEDULE");
        builder.HasKey(s => s.ShiftCode);
        builder.Property(s => s.ShiftCode).HasColumnName("SHIFT_CODE").HasMaxLength(100);

        builder.Property(s => s.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(400).IsRequired();
        builder.Property(s => s.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(s => s.StartTime).HasColumnName("START_TIME").IsRequired();
        builder.Property(s => s.EndTime).HasColumnName("END_TIME").IsRequired();
        builder.Property(s => s.BreakMinutes).HasColumnName("BREAK_MINUTES").IsRequired();
        builder.Property(s => s.WorkingDaysJson).HasColumnName("WORKING_DAYS_JSON").HasMaxLength(1000).IsRequired();
        builder.Property(s => s.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(s => s.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(s => s.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(s => s.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(s => s.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class EmployeeShiftAssignmentConfiguration : IEntityTypeConfiguration<EmployeeShiftAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftAssignment> builder)
    {
        builder.ToTable("HR_EMPLOYEE_SHIFT_ASSIGNMENT");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(a => a.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(a => a.ShiftCode).HasColumnName("SHIFT_CODE").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(a => a.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(a => a.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(a => a.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(a => a.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(a => a.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(a => a.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}
