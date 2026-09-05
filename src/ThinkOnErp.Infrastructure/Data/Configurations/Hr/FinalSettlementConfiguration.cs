using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class FinalSettlementConfiguration : IEntityTypeConfiguration<FinalSettlement>
{
    public void Configure(EntityTypeBuilder<FinalSettlement> builder)
    {
        builder.ToTable("HR_FINAL_SETTLEMENT");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.TerminationDate)
            .HasColumnName("TERMINATION_DATE")
            .IsRequired();

        builder.Property(s => s.TerminationReason)
            .HasColumnName("TERMINATION_REASON")
            .HasMaxLength(50)
            .HasDefaultValue("RESIGNATION")
            .IsRequired();

        builder.Property(s => s.ServiceYears)
            .HasColumnName("SERVICE_YEARS")
            .HasColumnType("NUMBER(6,2)")
            .IsRequired();

        builder.Property(s => s.LastBasicSalary)
            .HasColumnName("LAST_BASIC_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(s => s.EndOfServiceGratuity)
            .HasColumnName("END_OF_SERVICE_GRATUITY")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(s => s.UnusedLeaveDays)
            .HasColumnName("UNUSED_LEAVE_DAYS")
            .HasColumnType("NUMBER(6,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(s => s.UnusedLeaveEncashment)
            .HasColumnName("UNUSED_LEAVE_ENCASHMENT")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(s => s.NoticePeriodPay)
            .HasColumnName("NOTICE_PERIOD_PAY")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(s => s.OtherEntitlements)
            .HasColumnName("OTHER_ENTITLEMENTS")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(s => s.LoanBalanceDeduction)
            .HasColumnName("LOAN_BALANCE_DEDUCTION")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(s => s.OtherDeductions)
            .HasColumnName("OTHER_DEDUCTIONS")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("DRAFT")
            .IsRequired();

        builder.Property(s => s.JournalVoucherId)
            .HasColumnName("JOURNAL_VOUCHER_ID");

        builder.Property(s => s.ApprovedBy)
            .HasColumnName("APPROVED_BY")
            .HasMaxLength(100);

        builder.Property(s => s.ApprovalDate)
            .HasColumnName("APPROVAL_DATE");

        builder.Property(s => s.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(1000);

        builder.Property(s => s.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(s => s.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(s => s.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(s => new { s.EmployeeCode, s.TerminationDate })
            .HasDatabaseName("IX_HR_FINAL_SETTLE_EMP");

        builder.HasOne(s => s.Employee)
            .WithMany()
            .HasForeignKey(s => s.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
