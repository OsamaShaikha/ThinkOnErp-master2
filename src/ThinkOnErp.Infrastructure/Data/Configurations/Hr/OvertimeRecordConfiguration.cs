using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class OvertimeRecordConfiguration : IEntityTypeConfiguration<OvertimeRecord>
{
    public void Configure(EntityTypeBuilder<OvertimeRecord> builder)
    {
        builder.ToTable("HR_OVERTIME_RECORD");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(o => o.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.OvertimeDate)
            .HasColumnName("OVERTIME_DATE")
            .IsRequired();

        builder.Property(o => o.Hours)
            .HasColumnName("HOURS")
            .HasColumnType("NUMBER(6,2)")
            .IsRequired();

        builder.Property(o => o.RateMultiplier)
            .HasColumnName("RATE_MULTIPLIER")
            .HasColumnType("NUMBER(4,2)")
            .HasDefaultValue(1.25m)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("PENDING")
            .IsRequired();

        builder.Property(o => o.Reason)
            .HasColumnName("REASON")
            .HasMaxLength(500);

        builder.Property(o => o.ApprovedBy)
            .HasColumnName("APPROVED_BY")
            .HasMaxLength(100);

        builder.Property(o => o.ApprovalDate)
            .HasColumnName("APPROVAL_DATE");

        builder.Property(o => o.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(o => o.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(o => o.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasOne(o => o.Employee)
            .WithMany()
            .HasForeignKey(o => o.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
