using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;
namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;
public sealed class HrDepartmentConfiguration : IEntityTypeConfiguration<HrDepartment>
{
    public void Configure(EntityTypeBuilder<HrDepartment> b)
    {
        b.Property(x => x.CreationDate).HasColumnName("CREATION_DATE");
        b.Property(x => x.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        b.Property(x => x.UpdateDate).HasColumnName("UPDATE_DATE");
        b.Property(x => x.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        b.ToTable("HR_DEPARTMENT", t => t.ExcludeFromMigrations()); b.HasKey(x => x.DepartmentCode);
        b.Property(x => x.DepartmentCode).HasColumnName("DEPARTMENT_CODE").HasMaxLength(50);
        b.Property(x => x.BranchId).HasColumnName("BRANCH_ID");
        b.Property(x => x.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        b.Property(x => x.ParentDepartmentCode).HasColumnName("PARENT_DEPARTMENT_CODE").HasMaxLength(50);
        b.Property(x => x.ManagerEmployeeCode).HasColumnName("MANAGER_EMPLOYEE_CODE").HasMaxLength(50);
        b.Property(x => x.CostCenterCode).HasColumnName("COST_CENTER_CODE").HasMaxLength(50);
        b.Property(x => x.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)");
    }
}
public sealed class HrPositionConfiguration : IEntityTypeConfiguration<HrPosition>
{
    public void Configure(EntityTypeBuilder<HrPosition> b)
    {
        b.Property(x => x.CreationDate).HasColumnName("CREATION_DATE");
        b.Property(x => x.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        b.Property(x => x.UpdateDate).HasColumnName("UPDATE_DATE");
        b.Property(x => x.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        b.ToTable("HR_POSITION", t => t.ExcludeFromMigrations()); b.HasKey(x => x.PositionCode);
        b.Property(x => x.Headcount).HasColumnName("HEADCOUNT");
        b.Property(x => x.JobGradeCode).HasColumnName("JOB_GRADE_CODE").HasMaxLength(50);
        b.Property(x => x.PositionCode).HasColumnName("POSITION_CODE").HasMaxLength(50);
        b.Property(x => x.DepartmentCode).HasColumnName("DEPARTMENT_CODE").HasMaxLength(50).IsRequired();
        b.Property(x => x.TitleLocal).HasColumnName("TITLE_LOCAL").HasMaxLength(200).IsRequired();
        b.Property(x => x.TitleEn).HasColumnName("TITLE_EN").HasMaxLength(200).IsRequired();
        b.Property(x => x.ReportsToPositionCode).HasColumnName("REPORTS_TO_POSITION_CODE").HasMaxLength(50);
        b.Property(x => x.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)");
    }
}
