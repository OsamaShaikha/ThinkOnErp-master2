using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("HR_LEAVE_BALANCE");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(b => b.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.LeaveTypeCode)
            .HasColumnName("LEAVE_TYPE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.Year)
            .HasColumnName("YEAR_NO")
            .IsRequired();

        builder.Property(b => b.AccruedDays)
            .HasColumnName("ACCRUED_DAYS")
            .HasColumnType("NUMBER(6,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(b => b.UsedDays)
            .HasColumnName("USED_DAYS")
            .HasColumnType("NUMBER(6,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(b => b.CarriedForwardDays)
            .HasColumnName("CARRIED_FORWARD_DAYS")
            .HasColumnType("NUMBER(6,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(b => b.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(b => b.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(b => b.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(b => new { b.EmployeeCode, b.LeaveTypeCode, b.Year })
            .IsUnique()
            .HasDatabaseName("IX_HR_LEAVE_BAL_EMP_YR");

        builder.HasOne(b => b.Employee)
            .WithMany()
            .HasForeignKey(b => b.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.LeaveType)
            .WithMany(t => t.Balances)
            .HasForeignKey(b => b.LeaveTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
