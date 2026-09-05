using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class JobRequisitionConfiguration : IEntityTypeConfiguration<JobRequisition>
{
    public void Configure(EntityTypeBuilder<JobRequisition> builder)
    {
        builder.ToTable("HR_JOB_REQUISITION");

        builder.HasKey(r => r.RequisitionCode);

        builder.Property(r => r.RequisitionCode)
            .HasColumnName("REQUISITION_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.PositionCode)
            .HasColumnName("POSITION_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.DepartmentCode)
            .HasColumnName("DEPARTMENT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.BranchId)
            .HasColumnName("BRANCH_ID");

        builder.Property(r => r.Headcount)
            .HasColumnName("HEADCOUNT")
            .HasDefaultValue(1);

        builder.Property(r => r.TargetHireDate)
            .HasColumnName("TARGET_HIRE_DATE");

        builder.Property(r => r.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("DRAFT")
            .IsRequired();

        builder.Property(r => r.RequestedBy)
            .HasColumnName("REQUESTED_BY")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.ApprovedBy)
            .HasColumnName("APPROVED_BY")
            .HasMaxLength(100);

        builder.Property(r => r.ApprovalDate)
            .HasColumnName("APPROVAL_DATE");

        builder.Property(r => r.JobDescription)
            .HasColumnName("JOB_DESCRIPTION")
            .HasMaxLength(2000);

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

        builder.HasOne(r => r.Position)
            .WithMany()
            .HasForeignKey(r => r.PositionCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Department)
            .WithMany()
            .HasForeignKey(r => r.DepartmentCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
