using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosStaffAttendanceConfiguration : IEntityTypeConfiguration<PosStaffAttendance>
{
    public void Configure(EntityTypeBuilder<PosStaffAttendance> builder)
    {
        builder.ToTable("POS_STAFF_ATTENDANCE");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(a => a.UserId).HasColumnName("USER_ID").IsRequired();
        builder.Property(a => a.ShiftId).HasColumnName("SHIFT_ID");

        builder.Property(a => a.ClockInTime).HasColumnName("CLOCK_IN_TIME").HasColumnType("TIMESTAMP").IsRequired();
        builder.Property(a => a.ClockOutTime).HasColumnName("CLOCK_OUT_TIME").HasColumnType("TIMESTAMP");
        builder.Property(a => a.TotalHoursWorked).HasColumnName("TOTAL_HOURS_WORKED").HasColumnType("NUMBER(8,2)").HasDefaultValue(0m);
        builder.Property(a => a.Notes).HasColumnName("NOTES").HasMaxLength(500);

        builder.HasOne(a => a.Branch).WithMany().HasForeignKey(a => a.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Shift).WithMany().HasForeignKey(a => a.ShiftId).OnDelete(DeleteBehavior.SetNull);
    }
}
