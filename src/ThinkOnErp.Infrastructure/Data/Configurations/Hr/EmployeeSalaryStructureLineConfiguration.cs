using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeSalaryStructureLineConfiguration : IEntityTypeConfiguration<EmployeeSalaryStructureLine>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryStructureLine> builder)
    {
        builder.ToTable("HR_EMP_SALARY_STRUCT_LINE");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(l => l.StructureId)
            .HasColumnName("STRUCTURE_ID")
            .IsRequired();

        builder.Property(l => l.ComponentCode)
            .HasColumnName("COMPONENT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.Amount)
            .HasColumnName("AMOUNT")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.Percent)
            .HasColumnName("PERCENT_VALUE")
            .HasColumnType("NUMBER(8,4)");

        builder.Property(l => l.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(l => l.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.HasOne(l => l.Structure)
            .WithMany(s => s.Lines)
            .HasForeignKey(l => l.StructureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Component)
            .WithMany(c => c.StructureLines)
            .HasForeignKey(l => l.ComponentCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
