using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("HR_DEPARTMENT");

        builder.HasKey(d => d.DepartmentCode);

        builder.Property(d => d.DepartmentCode)
            .HasColumnName("DEPARTMENT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.ParentDepartmentCode)
            .HasColumnName("PARENT_DEPARTMENT_CODE")
            .HasMaxLength(50);

        builder.Property(d => d.BranchId)
            .HasColumnName("BRANCH_ID");

        builder.Property(d => d.CostCenterCode)
            .HasColumnName("COST_CENTER_CODE")
            .HasMaxLength(50);

        builder.Property(d => d.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(d => d.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(d => d.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(d => d.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // Relationships
        builder.HasOne(d => d.ParentDepartment)
            .WithMany(d => d.SubDepartments)
            .HasForeignKey(d => d.ParentDepartmentCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Branch)
            .WithMany()
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.CostCenter)
            .WithMany()
            .HasForeignKey(d => d.CostCenterCode)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
