using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeAdvanceConfiguration : IEntityTypeConfiguration<EmployeeAdvance>
{
    public void Configure(EntityTypeBuilder<EmployeeAdvance> builder)
    {
        builder.ToTable("HR_EMPLOYEE_ADVANCE");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(a => a.AdvanceAmount).HasColumnName("ADVANCE_AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(a => a.TargetPayPeriod).HasColumnName("TARGET_PAY_PERIOD").HasMaxLength(20).IsRequired();
        builder.Property(a => a.DeductedAmount).HasColumnName("DEDUCTED_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0);
        builder.Property(a => a.RemainingBalance).HasColumnName("REMAINING_BALANCE").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(a => a.Status).HasColumnName("STATUS").HasMaxLength(50).HasDefaultValue("PENDING").IsRequired();
        builder.Property(a => a.Reason).HasColumnName("REASON").HasMaxLength(500);
        builder.Property(a => a.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(100);
        builder.Property(a => a.ApprovalDate).HasColumnName("APPROVAL_DATE");
        builder.Property(a => a.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(a => a.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(a => a.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(a => a.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(a => new { a.EmployeeCode, a.TargetPayPeriod, a.Status });
        builder.HasOne(a => a.Employee).WithMany().HasPrincipalKey(e => e.EmployeeCode).HasForeignKey(a => a.EmployeeCode).OnDelete(DeleteBehavior.Restrict);
    }
}
