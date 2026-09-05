using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class ExpenseClaimConfiguration : IEntityTypeConfiguration<ExpenseClaim>
{
    public void Configure(EntityTypeBuilder<ExpenseClaim> builder)
    {
        builder.ToTable("HR_EXPENSE_CLAIM");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ClaimNumber)
            .HasColumnName("CLAIM_NUMBER")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ClaimDate)
            .HasColumnName("CLAIM_DATE")
            .IsRequired();

        builder.Property(c => c.TotalAmount)
            .HasColumnName("TOTAL_AMOUNT")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(c => c.CurrencyCode)
            .HasColumnName("CURRENCY_CODE")
            .HasMaxLength(10)
            .HasDefaultValue("JOD")
            .IsRequired();

        builder.Property(c => c.ReimbursementMethod)
            .HasColumnName("REIMBURSEMENT_METHOD")
            .HasMaxLength(50)
            .HasDefaultValue("NEXT_PAYROLL_RUN")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("SUBMITTED")
            .IsRequired();

        builder.Property(c => c.ApprovedBy)
            .HasColumnName("APPROVED_BY")
            .HasMaxLength(100);

        builder.Property(c => c.ApprovalDate)
            .HasColumnName("APPROVAL_DATE");

        builder.Property(c => c.RejectionReason)
            .HasColumnName("REJECTION_REASON")
            .HasMaxLength(500);

        builder.Property(c => c.ReimbursedInPayPeriod)
            .HasColumnName("REIMBURSED_IN_PERIOD")
            .HasMaxLength(20);

        builder.Property(c => c.ApVoucherId)
            .HasColumnName("AP_VOUCHER_ID");

        builder.Property(c => c.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(500);

        builder.Property(c => c.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(c => c.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(c => c.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(c => c.ClaimNumber)
            .IsUnique()
            .HasDatabaseName("IX_HR_EXPENSE_CLAIM_NO");

        builder.HasOne(c => c.Employee)
            .WithMany()
            .HasForeignKey(c => c.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
