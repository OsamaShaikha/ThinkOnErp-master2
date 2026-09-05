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
        builder.Property(p => p.Code).HasColumnName("CODE").HasMaxLength(50).IsRequired();
        builder.Property(p => p.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(p => p.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(p => p.GracePeriodMinutes).HasColumnName("GRACE_PERIOD_MINUTES").HasDefaultValue(15);
        builder.Property(p => p.EarlyLeaveToleranceMinutes).HasColumnName("EARLY_LEAVE_TOLERANCE_MINUTES").HasDefaultValue(0);
        builder.Property(p => p.MinimumMinutesForOvertime).HasColumnName("MIN_MINUTES_FOR_OVERTIME").HasDefaultValue(30);
        builder.Property(p => p.AutoDeductLateArrival).HasColumnName("AUTO_DEDUCT_LATE_ARRIVAL").HasDefaultValue(true);
        builder.Property(p => p.MissingPunchHandling).HasColumnName("MISSING_PUNCH_HANDLING").HasMaxLength(50).HasDefaultValue("FLAG_FOR_CORRECTION");
        builder.Property(p => p.IsDefault).HasColumnName("IS_DEFAULT").HasDefaultValue(true);
        builder.Property(p => p.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(p => p.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
    }
}
