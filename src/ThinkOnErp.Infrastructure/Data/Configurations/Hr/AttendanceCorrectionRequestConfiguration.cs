using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class AttendanceCorrectionRequestConfiguration : IEntityTypeConfiguration<AttendanceCorrectionRequest>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrectionRequest> builder)
    {
        builder.ToTable("HR_ATTENDANCE_CORRECTION");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(c => c.AttendanceDate).HasColumnName("ATTENDANCE_DATE").IsRequired();
        builder.Property(c => c.OldCheckIn).HasColumnName("OLD_CHECK_IN");
        builder.Property(c => c.OldCheckOut).HasColumnName("OLD_CHECK_OUT");
        builder.Property(c => c.RequestedCheckIn).HasColumnName("REQUESTED_CHECK_IN");
        builder.Property(c => c.RequestedCheckOut).HasColumnName("REQUESTED_CHECK_OUT");
        builder.Property(c => c.Reason).HasColumnName("REASON").HasMaxLength(500).IsRequired();
        builder.Property(c => c.Status).HasColumnName("STATUS").HasMaxLength(50).HasDefaultValue("PENDING").IsRequired();
        builder.Property(c => c.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(100);
        builder.Property(c => c.ApprovalDate).HasColumnName("APPROVAL_DATE");
        builder.Property(c => c.RejectionReason).HasColumnName("REJECTION_REASON").HasMaxLength(500);
        builder.Property(c => c.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(c => c.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(c => c.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(c => new { c.EmployeeCode, c.AttendanceDate, c.Status });
        builder.HasOne(c => c.Employee).WithMany().HasPrincipalKey(e => e.EmployeeCode).HasForeignKey(c => c.EmployeeCode).OnDelete(DeleteBehavior.Restrict);
    }
}
