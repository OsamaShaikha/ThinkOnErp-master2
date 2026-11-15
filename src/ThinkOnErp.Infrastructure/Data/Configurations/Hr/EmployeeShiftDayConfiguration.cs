using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeShiftDayConfiguration : IEntityTypeConfiguration<EmployeeShiftDay>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftDay> b)
    {
        b.ToTable("HR_EMPLOYEE_SHIFT_DAY", t =>
        {
            t.HasCheckConstraint("CK_HR_SHIFT_DAY_DATE", "WORK_DATE = TRUNC(WORK_DATE)");
            t.HasCheckConstraint("CK_HR_SHIFT_DAY_KIND", "(IS_REST_DAY=1 AND SHIFT_CODE IS NULL) OR (IS_REST_DAY=0 AND SHIFT_CODE IS NOT NULL)");
            t.HasCheckConstraint("CK_HR_SHIFT_DAY_ACTIVE", "IS_ACTIVE IN (0,1)");
            t.HasCheckConstraint("CK_HR_SHIFT_DAY_SOURCE", "SOURCE_REF IN ('MANUAL','GENERATED')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        b.Property(e => e.WorkDate).HasColumnName("WORK_DATE").HasColumnType("DATE");
        b.Property(e => e.ShiftCode).HasColumnName("SHIFT_CODE").HasMaxLength(100);
        b.Property(e => e.IsRestDay).HasColumnName("IS_REST_DAY").HasConversion<int>();
        b.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<int>();
        b.Property(e => e.Source).HasColumnName("SOURCE_REF").HasMaxLength(20).IsRequired();
        b.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        b.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        b.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        b.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE").IsConcurrencyToken();
        b.HasIndex(e => new { e.EmployeeCode, e.WorkDate }).IsUnique().HasDatabaseName("UX_HR_SHIFT_DAY_EMP_DATE");
        b.HasOne<Employee>().WithMany().HasForeignKey(e => e.EmployeeCode).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_SHIFT_DAY_EMP");
        b.HasOne<ShiftSchedule>().WithMany().HasForeignKey(e => e.ShiftCode).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_SHIFT_DAY_SHIFT");
    }
}
