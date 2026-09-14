using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class WorkCalendarConfiguration : IEntityTypeConfiguration<WorkCalendar>
{
    public void Configure(EntityTypeBuilder<WorkCalendar> builder)
    {
        builder.ToTable("HR_WORK_CALENDAR");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(c => c.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(c => c.Code).HasColumnName("CODE").HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameAr).HasColumnName("NAME_AR").HasMaxLength(400).IsRequired();
        builder.Property(c => c.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(c => c.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(c => c.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(c => c.IsDefault).HasColumnName("IS_DEFAULT").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(c => c.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(c => c.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(c => c.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasMany(c => c.Days)
            .WithOne(d => d.WorkCalendar)
            .HasForeignKey(d => d.WorkCalendarId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

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
        builder.Property(d => d.StandardWorkingHours).HasColumnName("STANDARD_WORKING_HOURS").HasPrecision(5, 2).IsRequired();
        builder.Property(d => d.DefaultShiftCode).HasColumnName("DEFAULT_SHIFT_CODE").HasMaxLength(100);
    }
}

public sealed class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        builder.ToTable("HR_PUBLIC_HOLIDAY");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(h => h.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(h => h.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(h => h.NameAr).HasColumnName("NAME_AR").HasMaxLength(400).IsRequired();
        builder.Property(h => h.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(h => h.HolidayDate).HasColumnName("HOLIDAY_DATE").IsRequired();
        builder.Property(h => h.IsPaid).HasColumnName("IS_PAID").IsRequired();
        builder.Property(h => h.IsRecurring).HasColumnName("IS_RECURRING").IsRequired();
        builder.Property(h => h.IsActive).HasColumnName("IS_ACTIVE").IsRequired();
        builder.Property(h => h.Description).HasColumnName("DESCRIPTION").HasMaxLength(1000);

        builder.Property(h => h.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(h => h.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(h => h.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(h => h.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}
