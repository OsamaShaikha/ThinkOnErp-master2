using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class JobGradeConfiguration : IEntityTypeConfiguration<JobGrade>
{
    public void Configure(EntityTypeBuilder<JobGrade> builder)
    {
        builder.ToTable("HR_JOB_GRADE");

        builder.HasKey(g => g.GradeCode);

        builder.Property(g => g.GradeCode)
            .HasColumnName("GRADE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(g => g.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(g => g.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(g => g.Level)
            .HasColumnName("LEVEL_NO")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(g => g.MinSalary)
            .HasColumnName("MIN_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(g => g.MidSalary)
            .HasColumnName("MID_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(g => g.MaxSalary)
            .HasColumnName("MAX_SALARY")
            .HasColumnType("NUMBER(18,4)")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(g => g.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(g => g.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(g => g.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(g => g.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(g => g.UpdateDate)
            .HasColumnName("UPDATE_DATE");
    }
}
