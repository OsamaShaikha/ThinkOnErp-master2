using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class ShiftScheduleConfiguration : IEntityTypeConfiguration<ShiftSchedule>
{
    public void Configure(EntityTypeBuilder<ShiftSchedule> builder)
    {
        builder.ToTable("HR_SHIFT_SCHEDULE");

        builder.HasKey(s => s.ShiftCode);

        builder.Property(s => s.ShiftCode)
            .HasColumnName("SHIFT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.StartTime)
            .HasColumnName("START_TIME")
            .IsRequired();

        builder.Property(s => s.EndTime)
            .HasColumnName("END_TIME")
            .IsRequired();

        builder.Property(s => s.BreakMinutes)
            .HasColumnName("BREAK_MINUTES")
            .HasDefaultValue(60);

        builder.Property(s => s.WorkingDaysJson)
            .HasColumnName("WORKING_DAYS_JSON")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(s => s.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(s => s.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(s => s.UpdateDate)
            .HasColumnName("UPDATE_DATE");
    }
}
