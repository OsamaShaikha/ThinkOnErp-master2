using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class ShortLeavePolicyConfiguration : IEntityTypeConfiguration<ShortLeavePolicy>
{
    public void Configure(EntityTypeBuilder<ShortLeavePolicy> builder)
    {
        builder.ToTable("HR_SHORT_LEAVE_POLICY", table => table.ExcludeFromMigrations());

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.MonthlyPaidMinutesQuota).HasColumnName("MONTHLY_PAID_MINUTES_QUOTA").IsRequired();
        builder.Property(p => p.MaxMinutesPerRequest).HasColumnName("MAX_MINUTES_PER_REQUEST").IsRequired();
        builder.Property(p => p.AllowMiddayExit).HasColumnName("ALLOW_MIDDAY_EXIT").IsRequired();
        builder.Property(p => p.ExcessAction).HasColumnName("EXCESS_ACTION").HasMaxLength(50).IsRequired();
        builder.Property(p => p.LeaveTypeCodeForDeduction).HasColumnName("LEAVE_TYPE_CODE_FOR_DEDUCTION").HasMaxLength(50).IsRequired();
        builder.Property(p => p.StandardWorkdayHours).HasColumnName("STANDARD_WORKDAY_HOURS").HasPrecision(5, 2).IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");

    }
}

public sealed class ShortLeaveRequestConfiguration : IEntityTypeConfiguration<ShortLeaveRequest>
{
    public void Configure(EntityTypeBuilder<ShortLeaveRequest> builder)
    {
        builder.ToTable("HR_SHORT_LEAVE_REQUEST", table => table.ExcludeFromMigrations());

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(r => r.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();

        builder.Property(r => r.PermissionType).HasColumnName("PERMISSION_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(r => r.PermissionDate).HasColumnName("PERMISSION_DATE").IsRequired();
        builder.Property(r => r.StartTime).HasColumnName("START_TIME").IsRequired();
        builder.Property(r => r.EndTime).HasColumnName("END_TIME").IsRequired();
        builder.Property(r => r.DurationMinutes).HasColumnName("DURATION_MINUTES").IsRequired();
        builder.Property(r => r.Reason).HasColumnName("REASON").HasMaxLength(500).IsRequired();

        builder.Property(r => r.Status).HasColumnName("STATUS").HasMaxLength(50).IsRequired();
        builder.Property(r => r.CompensationType).HasColumnName("COMPENSATION_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(r => r.DeductedSalaryAmount).HasColumnName("DEDUCTED_SALARY_AMOUNT").HasPrecision(18, 4);
        builder.Property(r => r.ConvertedLeaveDays).HasColumnName("CONVERTED_LEAVE_DAYS").HasPrecision(8, 4);

        builder.Property(r => r.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(100);
        builder.Property(r => r.ApprovedAtUtc).HasColumnName("APPROVED_AT_UTC");

        builder.Property(r => r.RejectedBy).HasColumnName("REJECTED_BY").HasMaxLength(100);
        builder.Property(r => r.RejectedAtUtc).HasColumnName("REJECTED_AT_UTC");
        builder.Property(r => r.RejectionReason).HasColumnName("REJECTION_REASON").HasMaxLength(500);

        builder.Property(r => r.CancelledBy).HasColumnName("CANCELLED_BY").HasMaxLength(100);
        builder.Property(r => r.CancelledAtUtc).HasColumnName("CANCELLED_AT_UTC");
        builder.Property(r => r.CancellationReason).HasColumnName("CANCELLATION_REASON").HasMaxLength(500);

        builder.Property(r => r.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(r => r.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(r => r.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(r => r.Employee)
            .WithMany()
            .HasForeignKey(r => r.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_SHORT_LEAVE_EMPLOYEE");

        builder.HasIndex(r => new { r.EmployeeCode, r.PermissionDate }).HasDatabaseName("IX_HR_SHORT_LEAVE_EMP_DATE");
        builder.HasIndex(r => r.Status).HasDatabaseName("IX_HR_SHORT_LEAVE_STATUS");
    }
}

