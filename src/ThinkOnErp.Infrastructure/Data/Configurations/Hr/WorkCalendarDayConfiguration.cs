using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class WorkCalendarDayConfiguration : IEntityTypeConfiguration<WorkCalendarDay>
{
    public void Configure(EntityTypeBuilder<WorkCalendarDay> builder)
    {
        builder.ToTable("HR_WORK_CALENDAR_DAY");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.WorkCalendarId).HasColumnName("WORK_CALENDAR_ID").IsRequired();
        builder.Property(d => d.DayOfWeek).HasColumnName("DAY_OF_WEEK").IsRequired();
        builder.Property(d => d.IsWorkingDay).HasColumnName("IS_WORKING_DAY").IsRequired();
        builder.Property(d => d.DefaultShiftCode).HasColumnName("DEFAULT_SHIFT_CODE").HasMaxLength(50);
        builder.Property(d => d.StandardWorkingHours).HasColumnName("STANDARD_WORKING_HOURS").HasColumnType("NUMBER(5,2)").HasDefaultValue(8.0m);

        builder.HasIndex(d => new { d.WorkCalendarId, d.DayOfWeek }).IsUnique();
        builder.HasOne(d => d.DefaultShift).WithMany().HasPrincipalKey(s => s.ShiftCode).HasForeignKey(d => d.DefaultShiftCode).OnDelete(DeleteBehavior.SetNull);

    }
}
