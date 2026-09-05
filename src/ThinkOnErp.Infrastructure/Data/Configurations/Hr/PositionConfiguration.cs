using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("HR_POSITION");

        builder.HasKey(p => p.PositionCode);

        builder.Property(p => p.PositionCode)
            .HasColumnName("POSITION_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.TitleAr)
            .HasColumnName("TITLE_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.TitleEn)
            .HasColumnName("TITLE_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.DepartmentCode)
            .HasColumnName("DEPARTMENT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.JobGradeCode)
            .HasColumnName("JOB_GRADE_CODE")
            .HasMaxLength(50);

        builder.Property(p => p.ReportsToPositionCode)
            .HasColumnName("REPORTS_TO_POSITION_CODE")
            .HasMaxLength(50);

        builder.Property(p => p.Headcount)
            .HasColumnName("HEADCOUNT")
            .HasDefaultValue(1)
            .IsRequired();

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

        // Relationships
        builder.HasOne(p => p.Department)
            .WithMany(d => d.Positions)
            .HasForeignKey(p => p.DepartmentCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.JobGrade)
            .WithMany(g => g.Positions)
            .HasForeignKey(p => p.JobGradeCode)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.ReportsToPosition)
            .WithMany(p => p.DirectReportPositions)
            .HasForeignKey(p => p.ReportsToPositionCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
