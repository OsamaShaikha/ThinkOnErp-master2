using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class RawAttendanceConfiguration : IEntityTypeConfiguration<RawAttendance>
{
    public void Configure(EntityTypeBuilder<RawAttendance> builder)
    {
        builder.ToTable("HR_RAW_ATTENDANCE");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(r => r.PunchTime).HasColumnName("PUNCH_TIME").IsRequired();
        builder.Property(r => r.PunchType).HasColumnName("PUNCH_TYPE").HasMaxLength(20).HasDefaultValue("IN").IsRequired();
        builder.Property(r => r.Source).HasColumnName("SOURCE").HasMaxLength(50).HasDefaultValue("BIOMETRIC").IsRequired();
        builder.Property(r => r.DeviceId).HasColumnName("DEVICE_ID").HasMaxLength(100);
        builder.Property(r => r.ExternalReference).HasColumnName("EXTERNAL_REFERENCE").HasMaxLength(100);
        builder.Property(r => r.IsProcessed).HasColumnName("IS_PROCESSED").HasDefaultValue(false);
        builder.Property(r => r.ProcessedDate).HasColumnName("PROCESSED_DATE");
        builder.Property(r => r.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreationDate).HasColumnName("CREATION_DATE").IsRequired();

        builder.HasIndex(r => new { r.EmployeeCode, r.PunchTime });
        builder.HasIndex(r => r.IsProcessed);
        builder.HasOne(r => r.Employee).WithMany().HasPrincipalKey(e => e.EmployeeCode).HasForeignKey(r => r.EmployeeCode).OnDelete(DeleteBehavior.Restrict);
    }
}
