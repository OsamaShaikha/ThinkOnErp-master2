using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("HR_LEAVE_TYPE");

        builder.HasKey(t => t.LeaveTypeCode);

        builder.Property(t => t.LeaveTypeCode)
            .HasColumnName("LEAVE_TYPE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.IsPaid)
            .HasColumnName("IS_PAID")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(t => t.IsStatutory)
            .HasColumnName("IS_STATUTORY")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(t => t.RequiresDocumentation)
            .HasColumnName("REQUIRES_DOCUMENTATION")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.MaxDaysPerYear)
            .HasColumnName("MAX_DAYS_PER_YEAR")
            .HasColumnType("NUMBER(6,2)")
            .HasDefaultValue(14m)
            .IsRequired();

        builder.Property(t => t.CarryForwardAllowed)
            .HasColumnName("CARRY_FORWARD_ALLOWED")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.CarryForwardCapDays)
            .HasColumnName("CARRY_FORWARD_CAP_DAYS")
            .HasColumnType("NUMBER(6,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(t => t.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(t => t.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(t => t.UpdateDate)
            .HasColumnName("UPDATE_DATE");
    }
}
