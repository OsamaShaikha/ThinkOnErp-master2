using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeLoanConfiguration : IEntityTypeConfiguration<EmployeeLoan>
{
    public void Configure(EntityTypeBuilder<EmployeeLoan> builder)
    {
        builder.ToTable("HR_EMPLOYEE_LOAN");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(l => l.LoanType).HasColumnName("LOAN_TYPE").HasMaxLength(50).HasDefaultValue("PERSONAL").IsRequired();
        builder.Property(l => l.PrincipalAmount).HasColumnName("PRINCIPAL_AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(l => l.TotalInstallments).HasColumnName("TOTAL_INSTALLMENTS").HasColumnType("NUMBER(5,0)").IsRequired();
        builder.Property(l => l.MonthlyInstallmentAmount).HasColumnName("MONTHLY_INSTALLMENT_AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(l => l.TotalPaidAmount).HasColumnName("TOTAL_PAID_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0);
        builder.Property(l => l.RemainingBalance).HasColumnName("REMAINING_BALANCE").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(l => l.StartDate).HasColumnName("START_DATE").IsRequired();
        builder.Property(l => l.EndDate).HasColumnName("END_DATE");
        builder.Property(l => l.Status).HasColumnName("STATUS").HasMaxLength(50).HasDefaultValue("PENDING").IsRequired();
        builder.Property(l => l.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(100);
        builder.Property(l => l.ApprovalDate).HasColumnName("APPROVAL_DATE");
        builder.Property(l => l.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(l => l.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(l => l.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(l => l.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(l => l.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(l => new { l.EmployeeCode, l.Status });
        builder.HasOne(l => l.Employee).WithMany().HasPrincipalKey(e => e.EmployeeCode).HasForeignKey(l => l.EmployeeCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(l => l.Schedules).WithOne(s => s.EmployeeLoan).HasForeignKey(s => s.EmployeeLoanId).OnDelete(DeleteBehavior.Cascade);
    }
}
