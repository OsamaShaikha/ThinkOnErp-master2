using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("HR_LEAVE_TYPE");

        builder.HasKey(e => e.LeaveTypeCode);

        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(400).IsRequired();
        builder.Property(e => e.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(e => e.IsPaid).HasColumnName("IS_PAID").HasConversion<int>().IsRequired();
        builder.Property(e => e.IsStatutory).HasColumnName("IS_STATUTORY").HasConversion<int>().IsRequired();
        builder.Property(e => e.MaxDaysPerYear).HasColumnName("MAX_DAYS_PER_YEAR").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.CarryForwardAllowed).HasColumnName("CARRY_FORWARD_ALLOWED").HasConversion<int>().IsRequired();
        builder.Property(e => e.CarryForwardCapDays).HasColumnName("CARRY_FORWARD_CAP_DAYS").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.RequiresDocumentation).HasColumnName("REQUIRES_DOCUMENTATION").HasConversion<int>().IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<int>().IsRequired();

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("HR_LEAVE_REQUEST");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.StartDate).HasColumnName("START_DATE").IsRequired();
        builder.Property(e => e.EndDate).HasColumnName("END_DATE").IsRequired();
        builder.Property(e => e.DaysRequested).HasColumnName("DAYS_REQUESTED").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(60).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("REASON").HasMaxLength(1000);
        builder.Property(e => e.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(200);
        builder.Property(e => e.ApprovalDate).HasColumnName("APPROVAL_DATE");
        builder.Property(e => e.RejectionReason).HasColumnName("REJECTION_REASON").HasMaxLength(1000);
        builder.Property(e => e.AttachmentFileRef).HasColumnName("ATTACHMENT_FILE_REF").HasMaxLength(1000);

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.LeaveType)
            .WithMany(t => t.Requests)
            .HasForeignKey(e => e.LeaveTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("HR_LEAVE_BALANCE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.YearNo).HasColumnName("YEAR_NO").IsRequired();
        builder.Property(e => e.AccruedDays).HasColumnName("ACCRUED_DAYS").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.UsedDays).HasColumnName("USED_DAYS").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.CarriedForwardDays).HasColumnName("CARRIED_FORWARD_DAYS").HasPrecision(6, 2).IsRequired();

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.LeaveType)
            .WithMany(t => t.Balances)
            .HasForeignKey(e => e.LeaveTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
    public void Configure(EntityTypeBuilder<LeavePolicy> builder)
    {
        builder.ToTable("HR_LEAVE_POLICY");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.PolicyName).HasColumnName("POLICY_NAME").HasMaxLength(400).IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.AccrualMethod).HasColumnName("ACCRUAL_METHOD").HasMaxLength(100).IsRequired();
        builder.Property(e => e.AccrualRate).HasColumnName("ACCRUAL_RATE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.ApplicableTo).HasColumnName("APPLICABLE_TO").HasMaxLength(100).IsRequired();
        builder.Property(e => e.MinServiceMonths).HasColumnName("MIN_SERVICE_MONTHS").IsRequired();
        builder.Property(e => e.Tier1YearsThreshold).HasColumnName("TIER1_YEARS_THRESHOLD").IsRequired();
        builder.Property(e => e.Tier1Days).HasColumnName("TIER1_DAYS").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.Tier2Days).HasColumnName("TIER2_DAYS").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<int>().IsRequired();

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.LeaveType)
            .WithMany()
            .HasForeignKey(e => e.LeaveTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
