using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class SalaryComponentConfiguration : IEntityTypeConfiguration<SalaryComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> builder)
    {
        builder.ToTable("HR_SALARY_COMPONENT");

        builder.HasKey(c => c.ComponentCode);

        builder.Property(c => c.ComponentCode)
            .HasColumnName("COMPONENT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.ComponentType)
            .HasColumnName("COMPONENT_TYPE")
            .HasMaxLength(30)
            .HasDefaultValue("EARNING")
            .IsRequired();

        builder.Property(c => c.IsTaxable)
            .HasColumnName("IS_TAXABLE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(c => c.IsSscApplicable)
            .HasColumnName("IS_SSC_APPLICABLE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(c => c.CalculationType)
            .HasColumnName("CALCULATION_TYPE")
            .HasMaxLength(30)
            .HasDefaultValue("FIXED_AMOUNT")
            .IsRequired();

        builder.Property(c => c.DefaultAmount)
            .HasColumnName("DEFAULT_AMOUNT")
            .HasColumnType("NUMBER(18,4)");

        builder.Property(c => c.DefaultPercent)
            .HasColumnName("DEFAULT_PERCENT")
            .HasColumnType("NUMBER(8,4)");

        builder.Property(c => c.GlAccountCode)
            .HasColumnName("GL_ACCOUNT_CODE")
            .HasMaxLength(50);

        builder.Property(c => c.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(c => c.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(c => c.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(c => c.UpdateDate)
            .HasColumnName("UPDATE_DATE");
    }
}
