using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
    public void Configure(EntityTypeBuilder<LeavePolicy> builder)
    {
        builder.ToTable("HR_LEAVE_POLICY");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.LeaveTypeCode)
            .HasColumnName("LEAVE_TYPE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.PolicyName)
            .HasColumnName("POLICY_NAME")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.ApplicableTo)
            .HasColumnName("APPLICABLE_TO")
            .HasMaxLength(50)
            .HasDefaultValue("ALL")
            .IsRequired();

        builder.Property(p => p.AccrualMethod)
            .HasColumnName("ACCRUAL_METHOD")
            .HasMaxLength(50)
            .HasDefaultValue("SERVICE_TIERED")
            .IsRequired();

        builder.Property(p => p.AccrualRate)
            .HasColumnName("ACCRUAL_RATE")
            .HasColumnType("NUMBER(8,4)")
            .HasDefaultValue(1.1667m)
            .IsRequired();

        builder.Property(p => p.MinServiceMonths)
            .HasColumnName("MIN_SERVICE_MONTHS")
            .HasDefaultValue(0);

        builder.Property(p => p.Tier1YearsThreshold)
            .HasColumnName("TIER1_YEARS_THRESHOLD")
            .HasDefaultValue(5);

        builder.Property(p => p.Tier1Days)
            .HasColumnName("TIER1_DAYS")
            .HasColumnType("NUMBER(6,2)")
            .HasDefaultValue(14m);

        builder.Property(p => p.Tier2Days)
            .HasColumnName("TIER2_DAYS")
            .HasColumnType("NUMBER(6,2)")
            .HasDefaultValue(21m);

        builder.Property(p => p.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(p => p.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(p => p.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasOne(p => p.LeaveType)
            .WithMany(t => t.Policies)
            .HasForeignKey(p => p.LeaveTypeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
