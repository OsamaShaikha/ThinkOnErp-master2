using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("HR_LEAVE_REQUEST");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(r => r.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.LeaveTypeCode)
            .HasColumnName("LEAVE_TYPE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.StartDate)
            .HasColumnName("START_DATE")
            .IsRequired();

        builder.Property(r => r.EndDate)
            .HasColumnName("END_DATE")
            .IsRequired();

        builder.Property(r => r.DaysRequested)
            .HasColumnName("DAYS_REQUESTED")
            .HasColumnType("NUMBER(6,2)")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("PENDING")
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasColumnName("REASON")
            .HasMaxLength(500);

        builder.Property(r => r.AttachmentFileReference)
            .HasColumnName("ATTACHMENT_FILE_REF")
            .HasMaxLength(500);

        builder.Property(r => r.ApprovedBy)
            .HasColumnName("APPROVED_BY")
            .HasMaxLength(100);

        builder.Property(r => r.ApprovalDate)
            .HasColumnName("APPROVAL_DATE");

        builder.Property(r => r.RejectionReason)
            .HasColumnName("REJECTION_REASON")
            .HasMaxLength(500);

        builder.Property(r => r.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(r => r.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(r => r.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(r => new { r.EmployeeCode, r.StartDate, r.EndDate })
            .HasDatabaseName("IX_HR_LEAVE_REQ_EMP_DATES");

        builder.HasOne(r => r.Employee)
            .WithMany()
            .HasForeignKey(r => r.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.LeaveType)
            .WithMany(t => t.Requests)
            .HasForeignKey(r => r.LeaveTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
