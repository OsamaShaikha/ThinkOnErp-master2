using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class AttendanceTimeSettingsConfiguration : IEntityTypeConfiguration<AttendanceTimeSettings>
{
    public void Configure(EntityTypeBuilder<AttendanceTimeSettings> b)
    {
        b.ToTable("HR_ATTENDANCE_TIME_SETTINGS");
        b.HasKey(s => s.Id).HasName("PK_HR_ATT_TIME_SETTINGS");
        b.Property(s => s.Id).HasColumnName("ID").HasColumnType("NUMBER(1)").ValueGeneratedNever();
        b.Property(s => s.TimeZoneId).HasColumnName("TIME_ZONE_ID").HasMaxLength(100).IsRequired();
        b.Property(s => s.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        b.Property(s => s.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
    }
}
